using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.GameScreen;
using Chapchu.UI.Title;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using AnalyticsRecorder = Chapchu.Account.AnalyticsRecorder;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면의 뷰들과 네트워크 · 씬 전환을 잇는다. Scripts/UI/ResultScreen 에서 네트워크를 아는 유일한 파일이다.
    /// 순위 · HP · 탈락은 게임 종료 요약(NetworkManager.LastResult)에서 한 번 그리고, 준비 · 나감 · 카운트다운은 방 상태가 바뀔 때마다 다시 그린다.
    /// 판정은 하지 않는다 — 순위는 방장이 계산해 보낸 값이다.
    /// 기준: docs/systems/07-win-condition.md 11절 · 시안 「결과화면 UI v3」
    /// </summary>
    public class ResultPresenter : MonoBehaviour
    {
        // 시안 문구 (07-win-condition.md 11-2)
        private const string WinTitleFormat = "{0} 승리!";
        private const string DrawTitle = "무승부!";
        private const string ChapchuLabel = "찹츄 승리";
        private const string ChapchuReasonFormat = "손패 {0}장으로 찹츄를 외쳤어요!";
        private const string EliminationLabel = "처치 승리";
        private const string EliminationReason = "마지막까지 살아남았어요!";
        private const string DrawLabel = "무승부";
        private const string DrawReason = "모두 쓰러졌어요";
        private const string OutStatFormat = "탈락 · {0}턴";
        private const string LeftStatFormat = "이탈 · {0}턴";
        private const string DownedByFormat = "{0}에게 당함";
        private const string RankFormat = "{0}위";
        private const string TiedRankFormat = "공동 {0}위";
        private const string HpFormat = "HP {0}";
        private const string NameSeparator = ", ";

        // 순위 순서(0 = 1위) → 왼쪽부터 몇 번째 칸에 서는지 · 칸의 가운데 x (1920 무대의 왼쪽 끝 기준). 시안 LAYOUT — 1위가 가운데에 선다
        private static readonly Dictionary<int, (float[] x, int[] slot)> Layouts = new Dictionary<int, (float[] x, int[] slot)>
        {
            [2] = (new[] { 782f, 1138f }, new[] { 0, 1 }),
            [3] = (new[] { 604f, 960f, 1316f }, new[] { 1, 0, 2 }),
            [4] = (new[] { 426f, 782f, 1138f, 1494f }, new[] { 1, 0, 2, 3 }),
        };

        // 시상대에 보이는 배지 수 — 더 받은 업적은 「판 기록」 에서 본다 (시안 MAX_BADGES)
        private const int MaxBadges = 3;
        private const string ReadyBubble = "준비!";

        [SerializeField] private PunResultServer server;
        [SerializeField] private ResultTitleView title;
        [SerializeField] private ResultButtonsView buttons;
        [SerializeField] private ResultCountdownView countdown;
        [SerializeField] private ResultBoardView board;
        [SerializeField] private ResultEmoteView emotes;

        [Header("배경 · 연출")]
        [Tooltip("노란 빛 — 승자 칸 뒤로 옮긴다 (x 만).")]
        [SerializeField] private RectTransform glow;
        [SerializeField] private ResultConfettiView confetti;

        [Header("업적")]
        [Tooltip("지표 아이콘 — ResultAwards.Metrics 와 같은 순서.")]
        [SerializeField] private Sprite[] metricIcons;
        [Tooltip("말풍선이 그려지는 층 — 시상대 칸들보다 위.")]
        [SerializeField] private RectTransform bubbleLayer;

        [Header("시상대")]
        [Tooltip("시상대 칸이 놓이는 자리 — 1920 무대의 왼쪽 아래가 원점, 바닥선 높이에 있다.")]
        [SerializeField] private RectTransform podiumArea;
        [SerializeField] private ResultPodiumView podiumPrefab;

        [Header("등장 연출 시각 (초, 시안 .stage.intro) — 화면이 열린 뒤부터")]
        [Tooltip("첫 시상대가 솟는 때 — 꼴찌 자리부터 솟고 1위가 마지막.")]
        [SerializeField] private float podiumIntroStart = 0.5f;
        [Tooltip("시상대가 하나씩 솟는 간격.")]
        [SerializeField] private float podiumIntroStep = 0.24f;
        [Tooltip("왕관이 떨어지는 때.")]
        [SerializeField] private float crownIntroAt = 2f;
        [Tooltip("아래 버튼 줄 · 이모트가 떠오르는 때.")]
        [SerializeField] private float buttonsIntroAt = 2.1f;
        [Tooltip("찹츄 승리 마크가 찍히는 때.")]
        [SerializeField] private float winMarkIntroAt = 2.3f;
        [Tooltip("승자가 깡충 뛰기 시작하는 때.")]
        [SerializeField] private float hopStartAt = 2.6f;

        private readonly List<ResultPodiumView> _podiums = new List<ResultPodiumView>(); // 순위 순 — _result.Actors 와 같은 자리
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<string> _names = new List<string>();
        private readonly HashSet<int> _readyShown = new HashSet<int>(); // 「준비!」 말풍선을 이미 띄운 사람 — 준비를 풀면 빠진다
        private GameResult _result;
        private bool _isLeaving;
        private bool _resultActionRecorded; // resultAction 은 결과 화면마다 한 번 (27 5-3)
        private bool _hasRefreshed;

        private void Awake()
        {
            if (server == null || title == null || buttons == null || countdown == null || podiumArea == null || podiumPrefab == null || board == null || emotes == null
                || glow == null || confetti == null || bubbleLayer == null || metricIcons == null || metricIcons.Length != ResultAwards.Metrics.Length)
                Debug.LogError($"[{nameof(ResultPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            server.StateChanged += Refresh;
            server.EmoteShown += HandleEmoteShown;
            buttons.AgainClicked += HandleAgainClicked;
            buttons.LobbyClicked += HandleLobbyClicked;
            buttons.BoardClicked += HandleBoardClicked;
            board.CloseClicked += HandleBoardCloseClicked;
            emotes.EmoteClicked += HandleEmoteClicked;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            server.StateChanged -= Refresh;
            server.EmoteShown -= HandleEmoteShown;
            buttons.AgainClicked -= HandleAgainClicked;
            buttons.LobbyClicked -= HandleLobbyClicked;
            buttons.BoardClicked -= HandleBoardClicked;
            board.CloseClicked -= HandleBoardCloseClicked;
            emotes.EmoteClicked -= HandleEmoteClicked;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
        }

        // 결과 씬은 방장이 방 전원을 옮겨서 열린다 — 요약은 게임 씬에서 이미 받았다
        private void Start()
        {
            AnalyticsRecorder.RecordScreenViewed(AnalyticsRecorder.ScreenName.Result);
            _result = NetworkManager.Instance != null ? NetworkManager.Instance.LastResult : null;
            if (_result == null || !PhotonNetwork.InRoom || !Layouts.ContainsKey(_result.Actors.Count)
                || (_result.Reason != GameEndReason.Draw && !_result.Actors.Contains(_result.Winner)))
            {
                Debug.LogWarning($"[{nameof(ResultPresenter)}] 그릴 결과가 없다 (방 밖이거나 종료 요약을 받지 못했다)");
                _result = null;
                return;
            }

            ShowResult();
            Refresh();
            PlayIntro();
        }

        private void Update()
        {
            if (_result == null)
                return;

            double now = PhotonNetwork.Time;
            double startAt = server.StartAt;
            countdown.Show(startAt > 0, startAt - now);

            // 「판 기록」 — 다음 판 카운트다운이 시작되면 닫는다. Esc · Android 뒤로가기로도 닫는다 (22-popup-ui.md 1절 #4)
            if (board.IsOpen && (startAt > 0 || Input.GetKeyDown(KeyCode.Escape)))
                board.SetOpen(false);
            buttons.ShowSeconds(Mathf.CeilToInt((float)(server.Deadline - now)));

            // 자동 이동 — 방장이 보내는 것이 아니라 각자 방을 나간다 (07 11-1). 카운트다운 중이면 다음 판으로 가는 중이다
            if (startAt <= 0 && now >= server.Deadline)
                Leave();
        }

        #region 요약 그리기 (한 번)

        private void ShowResult()
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            int count = _result.Actors.Count;
            bool isDraw = _result.Reason == GameEndReason.Draw;
            (float[] x, int[] slot) layout = Layouts[count];

            for (int i = 0; i < count; i++)
            {
                _names.Add(PlayerLook.NameOf(_result.Names[i], _result.Actors[i])); // 게임을 시작할 때의 닉네임 — 걸러서 쓴다
                _colors.Add(PlayerLook.ColorOf(_result.Actors[i], _result.Actors));
            }

            ResultAwards.Compute(_result, out bool[][] tops, out List<ResultAwards.Award>[] awards);

            for (int i = 0; i < count; i++)
            {
                int actor = _result.Actors[i];
                bool isOut = _result.OutTurns[i] > 0;
                int downedBy = IndexOf(_result.DownedBy[i]);

                ResultPodiumView podium = Instantiate(podiumPrefab, podiumArea);
                ((RectTransform)podium.transform).anchoredPosition = new Vector2(layout.x[layout.slot[i]], 0f);
                podium.Show(new ResultPodiumInfo(
                    _names[i], _colors[i], _result.Ranks[i], IsTied(i),
                    isWinner: actor == _result.Winner, isMe: actor == me, isOut: isOut,
                    hp: _result.Hps[i], maxHp: GameServer.MaxHp,
                    stat: isOut ? string.Format(_result.Left[i] ? LeftStatFormat : OutStatFormat, _result.OutTurns[i]) : null,
                    sub: isOut && downedBy >= 0 ? string.Format(DownedByFormat, _names[downedBy]) : null,
                    hasWinMark: actor == _result.Winner && _result.Reason == GameEndReason.Chapchu));
                podium.ShowBadges(awards[i].Take(MaxBadges).Select(award => new ResultBadgeInfo(
                    metricIcons[award.Metric], ResultAwards.Metrics[award.Metric].Award, string.Format(ResultAwards.Metrics[award.Metric].ValueFormat, award.Value))).ToList());
                podium.MoveBubbleTo(bubbleLayer);
                _podiums.Add(podium);
            }

            // 「판 기록」 — 지표 줄마다 사람(순위 순)의 값 · 1등 · 업적
            board.ShowPlayers(_names, _colors, IndexOf(me));
            for (int m = 0; m < ResultAwards.Metrics.Length; m++)
            {
                ResultAwards.Metric metric = ResultAwards.Metrics[m];
                board.AddRow(metric.Name, Enumerable.Range(0, count).Select(i => _result.Stat(i, metric.Kind)).ToList(), tops[m], metricIcons[m], metric.Award);
            }

            int winner = IndexOf(_result.Winner);
            if (TryGetWinnerX(out float winnerX))
                glow.anchoredPosition = new Vector2(winnerX, glow.anchoredPosition.y);

            switch (_result.Reason)
            {
                case GameEndReason.Chapchu:
                    title.Show(string.Format(WinTitleFormat, _names[winner]), ChapchuLabel, string.Format(ChapchuReasonFormat, GameServer.ChapchuHandCount));
                    break;
                case GameEndReason.Elimination:
                    title.Show(string.Format(WinTitleFormat, _names[winner]), EliminationLabel, EliminationReason);
                    break;
                default:
                    title.Show(DrawTitle, DrawLabel, DrawReason);
                    break;
            }

            // 내 순위 알약 — 내가 승자가 아닐 때 (무승부는 모두 1위라 띄우지 않는다)
            int mine = IndexOf(me);
            if (mine < 0 || isDraw || me == _result.Winner)
            {
                title.ShowMe(null, null);
            }
            else
            {
                bool isOut = _result.OutTurns[mine] > 0;
                int downedBy = IndexOf(_result.DownedBy[mine]);
                string detail = !isOut ? string.Format(HpFormat, _result.Hps[mine])
                    : downedBy >= 0 ? string.Format(DownedByFormat, _names[downedBy])
                    : string.Format(OutStatFormat, _result.OutTurns[mine]);
                title.ShowMe(string.Format(IsTied(mine) ? TiedRankFormat : RankFormat, _result.Ranks[mine]), detail);
            }

            buttons.BuildDots(count);
        }

        // 같은 순위가 또 있으면 공동 순위 (07 11-3)
        private bool IsTied(int index) => _result.Ranks.Count(rank => rank == _result.Ranks[index]) > 1;

        // actorNumber → 순위 자리 (_result.Actors 의 몇 번째인지). 요약에 없으면 -1
        private int IndexOf(int actor)
        {
            for (int i = 0; i < _result.Actors.Count; i++)
            {
                if (_result.Actors[i] == actor)
                    return i;
            }

            return -1;
        }

        // 승자 칸의 가운데 x (1920 무대의 왼쪽 끝 기준). 승자가 없으면(무승부) false
        private bool TryGetWinnerX(out float x)
        {
            int winner = IndexOf(_result.Winner);
            (float[] x, int[] slot) layout = Layouts[_result.Actors.Count];
            x = winner >= 0 ? layout.x[layout.slot[winner]] : 0f;
            return winner >= 0;
        }

        private void PlayIntro()
        {
            title.PlayIntro();
            for (int i = 0; i < _podiums.Count; i++)
                _podiums[i].PlayIntro(podiumIntroStart + (_podiums.Count - 1 - i) * podiumIntroStep, crownIntroAt, winMarkIntroAt, hopStartAt);
            buttons.PlayIntro(buttonsIntroAt);
            emotes.PlayIntro(buttonsIntroAt);

            // 색종이는 승자 칸 위에서 — 무승부면 터뜨리지 않는다
            if (TryGetWinnerX(out float winnerX))
                confetti.Play(winnerX);

            // 내가 이겼거나 무승부(모두 1위)면 밝게 올라가는 소리, 졌으면 낮게 내려가는 소리 (임시 효과음)
            bool won = _result.Reason == GameEndReason.Draw || PhotonNetwork.LocalPlayer.ActorNumber == _result.Winner;
            SoundManager.Play(won ? Sfx.Win : Sfx.Lose);
        }

        #endregion

        #region 준비 · 나감 (바뀔 때마다)

        private void Refresh()
        {
            if (_result == null)
                return;

            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            int[] members = PunResultServer.Members;
            int[] ready = server.Ready;
            var gone = new List<string>();

            for (int i = 0; i < _podiums.Count; i++)
            {
                int actor = _result.Actors[i];
                bool isGone = Array.IndexOf(members, actor) < 0;
                bool isReady = Array.IndexOf(ready, actor) >= 0;
                _podiums[i].SetReady(isReady);
                _podiums[i].SetGone(isGone);

                // 다른 사람이 준비하면 말풍선 「준비!」 (시안) — 화면이 열릴 때 이미 준비돼 있던 사람은 띄우지 않는다
                if (!isReady)
                    _readyShown.Remove(actor);
                else if (_readyShown.Add(actor) && _hasRefreshed && actor != me)
                    _podiums[i].Say(ReadyBubble);

                buttons.ShowDot(i, _colors[i], isReady, isGone);
                if (isGone)
                    gone.Add(_names[i]);
            }

            // 남은 사람끼리 이어서 한다 — 방에 있는 사람 + 봇이 2명 이상이면 (07 11-1)
            buttons.ShowAgain(members.Length >= NetworkManager.MinPlayers, Array.IndexOf(ready, me) >= 0, ready.Length, members.Length, string.Join(NameSeparator, gone));
            _hasRefreshed = true;
        }

        private void HandleAgainClicked()
        {
            if (_result == null || _isLeaving)
                return;

            bool ready = Array.IndexOf(server.Ready, PhotonNetwork.LocalPlayer.ActorNumber) < 0;
            // 「다시 하기」 를 처음 눌렀을 때 한 번 — 준비를 풀었다 다시 눌러도 더 보내지 않는다 (27 5-3)
            if (ready && !_resultActionRecorded)
            {
                _resultActionRecorded = true;
                AnalyticsRecorder.RecordResultAction(AnalyticsRecorder.ResultAction.ToRoom);
            }

            server.RequestSetReady(ready);
        }

        #endregion

        #region 판 기록 · 이모트

        private void HandleBoardClicked()
        {
            if (_result != null && server.StartAt <= 0)
                board.SetOpen(!board.IsOpen);
        }

        private void HandleBoardCloseClicked() => board.SetOpen(false);

        private void HandleEmoteClicked(int index)
        {
            if (_result == null || _isLeaving)
                return;

            server.RequestEmote(index);
        }

        // 누가 이모트를 보냈다 (방장이 알림) — 그 사람 캐릭터 옆에 말풍선
        private void HandleEmoteShown(int actor, int index)
        {
            int at = _result == null ? -1 : IndexOf(actor);
            string text = emotes.TextOf(index);
            if (at >= 0 && text != null)
                _podiums[at].Say(text);
        }

        #endregion

        #region 로비로

        // 「로비로」 버튼 — 시간이 다 돼 저절로 나가는 것은 버튼이 아니라 남기지 않는다 (27 5-3)
        private void HandleLobbyClicked()
        {
            if (!_isLeaving && !_resultActionRecorded)
            {
                _resultActionRecorded = true;
                AnalyticsRecorder.RecordResultAction(AnalyticsRecorder.ResultAction.Exit);
            }

            Leave();
        }

        // 「로비로」 · 자동 이동 — 방을 나간다. 씬은 나간 것이 확인된 뒤(HandleLeftRoom)에 연다
        private void Leave()
        {
            if (_isLeaving)
                return;

            _isLeaving = true;
            NetworkManager.Instance.LeaveRoom();
        }

        // 내가 나갔거나 접속이 끊겼다 — 어느 쪽이든 로비로 (08-room.md 6절)
        private void HandleLeftRoom()
        {
            _isLeaving = true;
            SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);
        }

        #endregion
    }
}
