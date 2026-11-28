using System;
using System.Collections;
using Chapchu.UI.Fx;
using Chapchu.UI.Popups;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.Core
{
    /// <summary>
    /// 화면(씬) 전환 연출 (15-screen.md 9절) — 각 클라이언트가 자기 화면에서 재생하는 표시 전용. 판정 · 동기화는 없다.
    ///   인게임으로 (룸 → 인게임 · 결과 → 인게임): 9-4 게임 시작 — 「게임 시작!」 띠 + 집중선 → 카드 뒷면 덮개(앞, 오른쪽에서).
    ///   그 밖 (인게임 → 결과 · 결과 → 로비 · 인게임 → 로비 · 판 중지 → 룸): 9-3 카드 뒷면 덮개(뒤, 왼쪽에서).
    /// 판이 화면을 다 덮은 뒤에 씬을 바꾸고(switchScene), 새 씬이 열리고 최소 머묾이 지나면 걷는다. 머묾이 길면 카드 뒷면 얼굴이 뜬다.
    /// 전환 중에는 입력을 막는다(판 아래 투명 막) — 손패처럼 Input 을 직접 읽는 곳은 IsPlaying 을 본다. 시작할 때 팝업 · 토스트를 닫는다.
    /// GameBootstrap 이 만든다 (Resources/Bootstrap/ScreenTransition). 없으면 연출 없이 바로 바꾼다.
    /// </summary>
    public class ScreenTransition : SingletonPersistent<ScreenTransition>
    {
        [Header("부품 (꺼 둔 채로 저장)")]
        [Tooltip("전환 캔버스 — 재생하는 동안만 켠다. 맨 위(팝업 위)에 그린다.")]
        [SerializeField] private GameObject canvasRoot;
        [Tooltip("캔버스 전체 — 덮개가 지나가는 폭을 잰다.")]
        [SerializeField] private RectTransform canvasRect;
        [Tooltip("카드 뒷면 덮개 — 화면보다 큰 평행사변형 (무늬 + 좌우 잉크 선).")]
        [SerializeField] private RectTransform cover;
        [Tooltip("머묾이 길 때 뜨는 카드 뒷면 얼굴 (원, 지름 410).")]
        [SerializeField] private RectTransform face;
        [Tooltip("「게임 시작!」 띠.")]
        [SerializeField] private RectTransform band;
        [Tooltip("띠 뒤 집중선.")]
        [SerializeField] private UISpeedLines focusLines;

        [Header("9-3 덮개 (15-screen.md — 제안 값)")]
        [SerializeField] private float coverSeconds = 0.28f;
        [Tooltip("다 덮은 뒤 최소 머묾.")]
        [SerializeField] private float minHoldSeconds = 0.2f;
        [Tooltip("머묾이 이 초를 넘으면 얼굴이 뜬다.")]
        [SerializeField] private float faceAfterSeconds = 0.3f;
        [SerializeField] private float facePopSeconds = 0.22f;
        [Tooltip("얼굴이 갸웃하는 각도 (±).")]
        [SerializeField] private float faceWagDegrees = 6f;
        [Tooltip("한 번 갸웃하는 초.")]
        [SerializeField] private float faceWagSeconds = 1.1f;
        [Tooltip("다음 화면이 준비된 뒤 얼굴을 더 보여 주는 초.")]
        [SerializeField] private float faceStaySeconds = 0.35f;
        [SerializeField] private float faceHideSeconds = 0.14f;
        [Tooltip("새 씬이 이만큼 안 열리면 그냥 걷는다 (멈춘 채 남지 않게).")]
        [SerializeField] private float loadTimeoutSeconds = 15f;

        [Header("9-4 게임 시작 (제안 값)")]
        [Tooltip("띠가 세로로 펼쳐지는 초 (0 → 125% → 100%).")]
        [SerializeField] private float bandOpenSeconds = 0.2f;
        [SerializeField] private float bandOvershoot = 1.25f;
        [Tooltip("띠 · 집중선이 뜬 뒤 덮기 전까지.")]
        [SerializeField] private float bandHoldSeconds = 0.45f;
        [Tooltip("덮기 시작 뒤 집중선이 사라지는 초.")]
        [SerializeField] private float linesOffAfterSeconds = 0.14f;
        [Tooltip("집중선을 다시 그리는 간격.")]
        [SerializeField] private float linesRedrawSeconds = 0.09f;
        [Tooltip("걷을 때 띠가 접히는 초.")]
        [SerializeField] private float bandCloseSeconds = 0.14f;

        private const float CoverSkewDegrees = 9f; // 덮개 기울기 (UISkewQuad 와 같게) — 화면 밖까지의 거리에만 쓴다

        private bool _playing;
        private bool _sceneLoaded;

        /// <summary>전환을 재생하는 중 — 입력을 받지 않는다 (15-screen.md 9-5).</summary>
        public static bool IsPlaying => Instance != null && Instance._playing;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            if (canvasRoot == null || canvasRect == null || cover == null || face == null || band == null || focusLines == null)
                Debug.LogError($"[{nameof(ScreenTransition)}] 참조 중 비어 있는 것이 있습니다.", this);
            else
                canvasRoot.SetActive(false);
        }

        /// <summary>
        /// 그 씬으로 가는 전환을 틀고, 화면을 다 덮은 순간 switchScene 을 부른다. 전환이 없거나 이미 재생 중이면 바로 부른다.
        /// </summary>
        public static void Play(string scene, Action switchScene)
        {
            if (Instance == null || Instance.canvasRoot == null || Instance._playing)
            {
                switchScene();
                return;
            }

            bool gameStart = scene == ScenePaths.Game || scene == SceneFlow.GameSceneAfterRoom;
            Instance.StartCoroutine(Instance.Run(gameStart, switchScene));
        }

        private IEnumerator Run(bool gameStart, Action switchScene)
        {
            _playing = true;
            if (PopupManager.Instance != null)
                PopupManager.Instance.CloseAll();

            canvasRoot.SetActive(true);
            face.gameObject.SetActive(false);
            band.gameObject.SetActive(gameStart);
            focusLines.gameObject.SetActive(gameStart);

            // 앞(인게임으로)은 오른쪽 밖에서 들어와 왼쪽으로 나가고, 뒤는 반대
            float side = gameStart ? 1f : -1f;
            // 덮개 가운데가 이만큼 옆이면 기운 가장자리까지 화면 밖이다
            float travel = (canvasRect.rect.width + cover.rect.width) / 2f + cover.rect.height * Mathf.Tan(CoverSkewDegrees * Mathf.Deg2Rad) / 2f;
            cover.anchoredPosition = new Vector2(side * travel, 0f);

            Coroutine shaking = null;
            if (gameStart)
            {
                shaking = StartCoroutine(RedrawLines());
                band.localScale = new Vector3(1f, 0f, 1f);
                DOTween.Sequence().SetTarget(band).SetUpdate(true)
                    .Append(band.DOScaleY(bandOvershoot, bandOpenSeconds * 0.6f).SetEase(Ease.OutQuad))
                    .Append(band.DOScaleY(1f, bandOpenSeconds * 0.4f).SetEase(Ease.InOutQuad));
                yield return new WaitForSecondsRealtime(bandOpenSeconds + bandHoldSeconds);
            }

            // 덮기 — 띠는 덮개 위에 남고 집중선은 조금 뒤 사라진다
            cover.DOKill();
            Tween coverIn = cover.DOAnchorPosX(0f, coverSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
            if (gameStart)
            {
                yield return new WaitForSecondsRealtime(linesOffAfterSeconds);
                StopCoroutine(shaking);
                focusLines.gameObject.SetActive(false);
            }
            yield return coverIn.WaitForCompletion();

            // 다 덮였다 — 씬을 바꾸고 새 씬이 열릴 때까지 머문다
            _sceneLoaded = false;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            float coveredAt = Time.unscaledTime;
            try
            {
                switchScene();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _sceneLoaded = true; // 바꾸지 못했다 — 걷고 끝낸다
            }

            bool faceShown = false;
            while (!_sceneLoaded || Time.unscaledTime - coveredAt < minHoldSeconds)
            {
                if (!faceShown && Time.unscaledTime - coveredAt >= faceAfterSeconds)
                {
                    faceShown = true;
                    ShowFace();
                }
                if (Time.unscaledTime - coveredAt >= loadTimeoutSeconds)
                {
                    Debug.LogWarning($"[{nameof(ScreenTransition)}] 새 화면이 {loadTimeoutSeconds}초 안에 열리지 않아 덮개를 걷는다", this);
                    break;
                }
                yield return null;
            }
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            yield return null; // 새 씬의 Start 가 한 번 돌아 첫 화면이 그려진 뒤에 걷는다

            if (faceShown)
            {
                yield return new WaitForSecondsRealtime(faceStaySeconds);
                face.DOKill();
                yield return face.DOScale(0f, faceHideSeconds).SetEase(Ease.InBack).SetUpdate(true).WaitForCompletion();
                face.gameObject.SetActive(false);
            }

            // 걷기 — 들어온 방향으로 계속 나아간다. 띠는 접힌다
            if (gameStart)
                band.DOScaleY(0f, bandCloseSeconds).SetEase(Ease.InQuad).SetUpdate(true);
            yield return cover.DOAnchorPosX(-side * travel, coverSeconds).SetEase(Ease.InQuad).SetUpdate(true).WaitForCompletion();

            band.DOKill();
            canvasRoot.SetActive(false);
            _playing = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                _sceneLoaded = true;
        }

        private void ShowFace()
        {
            face.gameObject.SetActive(true);
            face.DOKill();
            face.localScale = Vector3.zero;
            face.localRotation = Quaternion.Euler(0f, 0f, -faceWagDegrees);
            face.DOScale(1f, facePopSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            face.DOLocalRotate(new Vector3(0f, 0f, faceWagDegrees), faceWagSeconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        private IEnumerator RedrawLines()
        {
            while (true)
            {
                focusLines.Shuffle();
                yield return new WaitForSecondsRealtime(linesRedrawSeconds);
            }
        }
    }
}
