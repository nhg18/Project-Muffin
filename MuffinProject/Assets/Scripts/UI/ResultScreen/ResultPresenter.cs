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

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면의 뷰들과 네트워크 · 씬 전환을 잇는다. Scripts/UI/ResultScreen 에서 네트워크를 아는 유일한 파일이다.
    /// 순위 · HP · 탈락은 게임 종료 요약(GameResult.Last)에서 한 번 그리고, 준비 · 나감 · 카운트다운은 방 상태가 바뀔 때마다 다시 그린다.
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
        private const string NicknameFallbackFormat = "P{0}"; // 닉네임이 비었을 때 — PlayerLook 과 같은 꼴

        // 순위 순서(0 = 1위) → 왼쪽부터 몇 번째 칸에 서는지 · 칸의 가운데 x (1920 무대의 왼쪽 끝 기준). 시안 LAYOUT — 1위가 가운데에 선다
        private static readonly Dictionary<int, (float[] x, int[] slot)> Layouts = new Dictionary<int, (float[] x, int[] slot)>
        {
            [2] = (new[] { 782f, 1138f }, new[] { 0, 1 }),
            [3] = (new[] { 604f, 960f, 1316f }, new[] { 1, 0, 2 }),
            [4] = (new[] { 426f, 782f, 1138f, 1494f }, new[] { 1, 0, 2, 3 }),
        };

        // 등장 연출 시각 (초, 시안 .stage.intro) — 꼴찌 시상대부터 솟고 1위가 마지막
        private const float PodiumIntroStart = 0.5f;
        private const float PodiumIntroStep = 0.24f;
        private const float CrownIntroAt = 2f;
        private const float ButtonsIntroAt = 2.1f;
        private const float WinMarkIntroAt = 2.3f;
        private const float HopStartAt = 2.6f;

        [SerializeField] private PunResultServer server;
        [SerializeField] private ResultTitleView title;
        [SerializeField] private ResultButtonsView buttons;
        [SerializeField] private ResultCountdownView countdown;

        [Header("시상대")]
        [Tooltip("시상대 칸이 놓이는 자리 — 1920 무대의 왼쪽 아래가 원점, 바닥선 높이에 있다.")]
        [SerializeField] private RectTransform podiumArea;
        [SerializeField] private ResultPodiumView podiumPrefab;

        private readonly List<ResultPodiumView> _podiums = new List<ResultPodiumView>(); // 순위 순 — _result.Actors 와 같은 자리
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<string> _names = new List<string>();
        private GameResult _result;
        private bool _isLeaving;

        private void Awake()
        {
            if (server == null || title == null || buttons == null || countdown == null || podiumArea == null || podiumPrefab == null)
                Debug.LogError($"[{nameof(ResultPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            server.StateChanged += Refresh;
            buttons.AgainClicked += HandleAgainClicked;
            buttons.LobbyClicked += Leave;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            server.StateChanged -= Refresh;
            buttons.AgainClicked -= HandleAgainClicked;
            buttons.LobbyClicked -= Leave;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
        }

        // 결과 씬은 방장이 방 전원을 옮겨서 열린다 — 요약은 게임 씬에서 이미 받았다
        private void Start()
        {
            _result = GameResult.Last;
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
                _names.Add(NameOf(i));
                _colors.Add(PlayerLook.ColorOf(_result.Actors[i], _result.Actors));
            }

            for (int i = 0; i < count; i++)
            {
                int actor = _result.Actors[i];
                bool isOut = _result.OutTurns[i] > 0;
                int downedBy = _result.Actors.ToList().IndexOf(_result.DownedBy[i]);

                ResultPodiumView podium = Instantiate(podiumPrefab, podiumArea);
                ((RectTransform)podium.transform).anchoredPosition = new Vector2(layout.x[layout.slot[i]], 0f);
                podium.Show(new ResultPodiumInfo(
                    _names[i], _colors[i], _result.Ranks[i], IsTied(i),
                    isWinner: actor == _result.Winner, isMe: actor == me, isOut: isOut,
                    hp: _result.Hps[i], maxHp: GameServer.MaxHp,
                    stat: isOut ? string.Format(_result.Left[i] ? LeftStatFormat : OutStatFormat, _result.OutTurns[i]) : null,
                    sub: isOut && downedBy >= 0 ? string.Format(DownedByFormat, _names[downedBy]) : null,
                    hasWinMark: actor == _result.Winner && _result.Reason == GameEndReason.Chapchu));
                _podiums.Add(podium);
            }

            int winner = _result.Actors.ToList().IndexOf(_result.Winner);
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
            int mine = _result.Actors.ToList().IndexOf(me);
            if (mine < 0 || isDraw || me == _result.Winner)
            {
                title.ShowMe(null, null);
            }
            else
            {
                bool isOut = _result.OutTurns[mine] > 0;
                int downedBy = _result.Actors.ToList().IndexOf(_result.DownedBy[mine]);
                string detail = !isOut ? string.Format(HpFormat, _result.Hps[mine])
                    : downedBy >= 0 ? string.Format(DownedByFormat, _names[downedBy])
                    : string.Format(OutStatFormat, _result.OutTurns[mine]);
                title.ShowMe(string.Format(IsTied(mine) ? TiedRankFormat : RankFormat, _result.Ranks[mine]), detail);
            }

            buttons.BuildDots(count);
        }

        // 같은 순위가 또 있으면 공동 순위 (07 11-3)
        private bool IsTied(int index) => _result.Ranks.Count(rank => rank == _result.Ranks[index]) > 1;

        // 게임을 시작할 때의 닉네임 — 남의 이름은 걸러서 쓴다. 비면 P{actorNumber}
        private string NameOf(int index)
        {
            string name = NicknameValidator.ForDisplay(_result.Names[index]);
            return name.Length > 0 ? name : string.Format(NicknameFallbackFormat, _result.Actors[index]);
        }

        private void PlayIntro()
        {
            title.PlayIntro();
            for (int i = 0; i < _podiums.Count; i++)
                _podiums[i].PlayIntro(PodiumIntroStart + (3 - i) * PodiumIntroStep, CrownIntroAt, WinMarkIntroAt, HopStartAt);
            buttons.PlayIntro(ButtonsIntroAt);
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
                buttons.ShowDot(i, _colors[i], isReady, isGone);
                if (isGone)
                    gone.Add(_names[i]);
            }

            // 남은 사람끼리 이어서 한다 — 방에 있는 사람 + 봇이 2명 이상이면 (07 11-1)
            buttons.ShowAgain(members.Length >= NetworkManager.MinPlayers, Array.IndexOf(ready, me) >= 0, ready.Length, members.Length, string.Join(NameSeparator, gone));
        }

        private void HandleAgainClicked()
        {
            if (_result == null || _isLeaving)
                return;

            server.RequestSetReady(Array.IndexOf(server.Ready, PhotonNetwork.LocalPlayer.ActorNumber) < 0);
        }

        #endregion

        #region 로비로

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
