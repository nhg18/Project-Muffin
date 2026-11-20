using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Chapchu.Cards
{
    /// <summary>
    /// 카드 데이터 맞추기 — 서버가 정한 카드 데이터(Remote Config 의 확인값 + 주소)를 받아 게임 덱에 얹는다. 기준: docs/systems/26-card-data.md 6-1 · 6-5 · 7-5 · 8절
    /// 순서: 이미 그 데이터면 끝 → 빌드 기본본이 그 데이터면 빌드 기본본 → 기기 저장본 → 받기. 못 맞추면 저장본, 없으면 빌드 기본본으로 논다.
    /// 타이틀이 접속 전에(SyncRoutine), 로비가 방에 가기 전에(ReadRoutine → SyncRoutine) 부른다. 방 · 판 안에서는 부르지 않는다 — 그 방의 데이터로 끝까지 간다.
    /// </summary>
    public class CardDataService : SingletonPersistent<CardDataService>
    {
        // 받기 안전장치 (26 4절 · 7-8)
        private const int MaxAttempts = 3;
        private const float RequestTimeoutSeconds = 10f;
        private const float TotalBudgetSeconds = 15f;
        private const float RetryJitter = 0.3f;
        private const int MaxFileBytes = 2 * 1024 * 1024;
        private const float RefreshIntervalSeconds = 30f;
        private const long TooManyRequests = 429;
        private const long RequestTimeout = 408;

        private const string FolderName = "CardData";
        private const string FileExtension = ".json";

        // 다시 시도 전 기다림(초) — [시도 번호 − 1]. 서버가 기다릴 시간을 주면 그 값을 쓴다
        private static readonly float[] RetryWaitSeconds = { 1f, 2f };

        [Tooltip("게임 덱 (Deck_Main) — 받은 카드 데이터를 여기에 얹는다. 게임 화면이 쓰는 덱과 같은 에셋이어야 한다")]
        [SerializeField] private DeckData deck;

        private float _lastSyncAt = float.NegativeInfinity;
        private string _lastTargetHash; // 마지막으로 맞추려 한 서버 값 (원격 끔이면 null)
        private readonly HashSet<string> _rejectedHashes = new HashSet<string>(); // 이번 실행에 받았지만 쓸 수 없던 데이터 전부 — 앱을 끌 때까지 다시 받지 않는다

        /// <summary>지금 쓰는 카드 데이터의 확인값 — 방 속성에 쓰고, 같은 값의 방만 찾는다 (26 7-6).</summary>
        public string ActiveHash => deck != null ? deck.ActiveHash : null;

        /// <summary>카드 데이터 파일을 받는 중 — 로비가 로딩 글을 「받는 중…」 으로 바꾼다 (26 6-5).</summary>
        public bool IsDownloading { get; private set; }

        /// <summary>이번 실행에 카드 데이터 파일을 받아서 얹었다 — 분석 lobbyReached 의 cardDataDownloaded (27).</summary>
        public bool DownloadedThisRun { get; private set; }

        /// <summary>서버가 정한 카드 데이터를 쓰지 못하고 있다 (받기 실패 · 앱이 모르는 데이터) — 방 버전이 다를 때의 안내를 가른다 (26 7-6).</summary>
        public bool IsBehind
        {
            get
            {
                if (!AccountService.IsSignedIn)
                    return false;

                CardDataTarget target = ServerConfig.CardData;
                return target.IsSet && target.Hash != ActiveHash;
            }
        }

        private static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
                return;

            if (deck == null)
            {
                Debug.LogError($"[{nameof(CardDataService)}] {nameof(deck)} 참조가 비어 있습니다.", this);
                return;
            }

            // 에디터는 지난 플레이의 사본이 에셋에 남아 있을 수 있다 — 늘 빌드 기본본에서 시작한다
            deck.ClearPatch();
        }

        /// <summary>
        /// 서버가 정한 카드 데이터로 맞춘다 (26 6-1). 설정 값(Remote Config)을 읽은 뒤에 부른다 — 읽지 못했으면 지난 실행에 받아 둔 값으로 맞춘다.
        /// 실패해도 끝난다 — 그때 쓰는 데이터는 ActiveHash.
        /// </summary>
        public IEnumerator SyncRoutine() => SyncToRoutine(ServerConfig.CardData);

        private IEnumerator SyncToRoutine(CardDataTarget target)
        {
            _lastSyncAt = Time.realtimeSinceStartup;
            _lastTargetHash = target.IsSet ? target.Hash : null;
            if (deck == null)
                yield break;

            if (!target.IsSet || target.Hash == deck.dataHash)
            {
                // 원격을 쓰지 않거나, 이 빌드에 든 것이 바로 그 데이터다
                if (deck.IsPatched)
                    Debug.Log($"[{nameof(CardDataService)}] 빌드 기본본으로 — {Short(deck.dataHash)}");
                deck.ClearPatch();
                yield break;
            }

            if (target.Hash == deck.ActiveHash || TryUseStored(target.Hash))
                yield break;

            if (!_rejectedHashes.Contains(target.Hash))
            {
                yield return DownloadRoutine(target);
                if (target.Hash == deck.ActiveHash)
                    yield break;
            }

            FallBack();
        }

        /// <summary>
        /// 로비에서 방에 가려 할 때 (26 6-5) — 마지막으로 확인한 지 30초가 지났으면 설정 값을 다시 읽는다.
        /// onDone(true) = 맞출 것이 있다 — 부른 쪽이 업데이트 · 점검을 먼저 보고, 둘 다 아닐 때만 SyncRoutine 으로 맞춘다.
        /// onDone(false) = 맞추지 않고 지금 데이터로 간다 — 읽기 실패 · 로그인하지 않은 실행(디버그 로비) · 30초 안이고 서버 값이 그대로.
        /// </summary>
        public IEnumerator ReadRoutine(Action<bool> onDone)
        {
            if (!AccountService.IsSignedIn)
            {
                onDone(false);
                yield break;
            }

            if (Time.realtimeSinceStartup - _lastSyncAt < RefreshIntervalSeconds)
            {
                // 다시 읽지는 않는다. 시간 초과 뒤 늦게 끝난 읽기가 새 값을 가져다 놓았으면 그 값으로는 맞춘다 — 이미 맞추려 한 값이면 다시 하지 않는다
                CardDataTarget target = ServerConfig.CardData;
                onDone((target.IsSet ? target.Hash : null) != _lastTargetHash);
                yield break;
            }

            // 읽기 실패도 30초 간격에 넣는다 — 안 그러면 누를 때마다 다시 읽고 다시 기다린다
            _lastSyncAt = Time.realtimeSinceStartup;
            Task fetch = ServerConfig.FetchAsync();
            float waited = 0f;
            while (!fetch.IsCompleted && waited < ServerConfig.FetchTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            bool read = fetch.IsCompleted && !fetch.IsFaulted;
            if (!read)
                Debug.LogWarning($"[{nameof(CardDataService)}] 설정 값 읽기 실패 — 맞추지 않고 지금 데이터로 간다: {fetch.Exception?.GetBaseException().Message ?? "시간 초과"}");

            onDone(read);
        }

        #region 받기

        // 최대 3번 · 전체 15초 · 2MB 까지 (26 4절). 성공하면 얹고 저장한다
        private IEnumerator DownloadRoutine(CardDataTarget target)
        {
            if (!IsAllowed(target.Url))
            {
                Debug.LogError($"[{nameof(CardDataService)}] 카드 데이터 주소가 https 가 아니다 — 받지 않는다: {target.Url}");
                _rejectedHashes.Add(target.Hash);
                yield break;
            }

            IsDownloading = true;
            try
            {
                float deadline = Time.realtimeSinceStartup + TotalBudgetSeconds;
                float serverWait = 0f;
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    if (attempt > 0)
                    {
                        float wait = serverWait > 0f ? serverWait : RetryWaitSeconds[attempt - 1] * UnityEngine.Random.Range(1f - RetryJitter, 1f + RetryJitter);
                        if (Time.realtimeSinceStartup + wait >= deadline)
                            break;

                        yield return new WaitForSecondsRealtime(wait);
                    }

                    float left = deadline - Time.realtimeSinceStartup;
                    if (left <= 0f)
                        break;

                    using (UnityWebRequest request = UnityWebRequest.Get(target.Url))
                    {
                        request.timeout = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(RequestTimeoutSeconds, left)));
                        UnityWebRequestAsyncOperation sending = request.SendWebRequest();
                        bool tooBig = false;
                        while (!sending.isDone)
                        {
                            if (request.downloadedBytes > MaxFileBytes)
                            {
                                tooBig = true;
                                request.Abort();
                                break;
                            }

                            yield return null;
                        }

                        // 프레임 사이에 다 받았으면 루프 안에서 크기를 못 봤다 — 한 번 더 본다
                        if (tooBig || request.downloadedBytes > MaxFileBytes)
                        {
                            Reject(target, $"파일이 {MaxFileBytes / 1024}KB 를 넘는다");
                            break;
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            Accept(target, request.downloadHandler.text);
                            break;
                        }

                        Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터 받기 실패 ({attempt + 1}/{MaxAttempts}) — {request.responseCode} {request.error}");

                        // 없는 파일 · 거절(4xx)은 다시 해도 같다. 시간 초과 · 너무 많음은 다시 시도한다
                        bool clientError = request.responseCode >= 400 && request.responseCode < 500;
                        if (clientError && request.responseCode != TooManyRequests && request.responseCode != RequestTimeout)
                        {
                            _rejectedHashes.Add(target.Hash);
                            break;
                        }

                        serverWait = request.responseCode == TooManyRequests && float.TryParse(request.GetResponseHeader("Retry-After"), out float seconds) ? seconds : 0f;
                    }
                }
            }
            finally
            {
                IsDownloading = false;
            }
        }

        // 받은 내용이 확인값과 맞고 이 빌드가 쓸 수 있으면 얹고 저장한다
        private void Accept(CardDataTarget target, string json)
        {
            if (CardSheet.HashOf(json) != target.Hash)
            {
                Reject(target, "받은 파일의 확인값이 다르다");
                return;
            }

            if (!CardSheet.TryParse(json, out CardSheet sheet, out string error) || !deck.TryPatch(sheet, target.Hash, out error))
            {
                Reject(target, error);
                return;
            }

            DownloadedThisRun = true;
            Debug.Log($"[{nameof(CardDataService)}] 카드 데이터 받음 — {target.Version}번 {Short(target.Hash)}");
            Save(target.Hash, json);
        }

        private void Reject(CardDataTarget target, string reason)
        {
            _rejectedHashes.Add(target.Hash);
            Debug.LogError($"[{nameof(CardDataService)}] 카드 데이터 {Short(target.Hash)} 를 쓸 수 없다 — {reason}");
        }

        // 배포는 https 만. 개발 빌드 · 에디터는 로컬 서버(http)로 시험할 수 있다
        private static bool IsAllowed(string url)
            => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || (Debug.isDebugBuild && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        #endregion

        #region 저장본

        // 임시 파일에 다 쓴 뒤 이름을 바꾼다 — 쓰다가 꺼져도 반쯤 쓴 파일이 저장본이 되지 않는다. 옛 저장본은 그 뒤에 지운다
        private static void Save(string hash, string json)
        {
            string path = PathOf(hash);
            try
            {
                Directory.CreateDirectory(Folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);

                foreach (string other in Directory.GetFiles(Folder))
                {
                    if (other != path)
                        File.Delete(other);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // 이번 실행은 얹은 값으로 논다. 다음 실행에 다시 받는다
                Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터 저장 실패: {exception.Message}");
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKeys.CardDataHash, hash);
            PlayerPrefs.SetString(PlayerPrefsKeys.CardDataAppVersion, Application.version);
            PlayerPrefs.Save();
        }

        // 기기 저장본을 얹는다. 파일 이름이 곧 확인값이라 서버 값 없이도 깨진 파일을 가려낸다 — 틀리면 지운다
        private bool TryUseStored(string hash)
        {
            string path = PathOf(hash);
            string json;
            try
            {
                if (!File.Exists(path))
                    return false;

                json = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터 저장본 읽기 실패: {exception.Message}");
                return false;
            }

            string error = "확인값이 다르다";
            if (CardSheet.HashOf(json) == hash && CardSheet.TryParse(json, out CardSheet sheet, out error) && deck.TryPatch(sheet, hash, out error))
            {
                Debug.Log($"[{nameof(CardDataService)}] 카드 데이터 저장본 — {Short(hash)}");
                return true;
            }

            Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터 저장본 {Short(hash)} 을 버린다 — {error}");
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터 저장본 지우기 실패: {exception.Message}");
            }

            return false;
        }

        // 서버가 정한 데이터로 못 맞췄다 (26 8절). 쓰던 원격 데이터가 있으면 그대로, 없으면 마지막 저장본, 그것도 없으면 빌드 기본본.
        // 앱이 업데이트된 뒤에는 옛 저장본으로 물러나지 않는다 — 새 빌드에 든 것이 그보다 새 데이터다
        private void FallBack()
        {
            if (deck.IsPatched)
                return;

            string stored = PlayerPrefs.GetString(PlayerPrefsKeys.CardDataHash, "");
            bool sameApp = PlayerPrefs.GetString(PlayerPrefsKeys.CardDataAppVersion, "") == Application.version;
            if (stored.Length > 0 && sameApp && TryUseStored(stored))
                return;

            Debug.LogWarning($"[{nameof(CardDataService)}] 카드 데이터를 맞추지 못했다 — 빌드 기본본 {Short(deck.dataHash)} 으로");
        }

        private static string PathOf(string hash) => Path.Combine(Folder, hash + FileExtension);

        private static string Short(string hash) => string.IsNullOrEmpty(hash) ? "(없음)" : hash.Substring(0, Mathf.Min(8, hash.Length));

        #endregion
    }
}
