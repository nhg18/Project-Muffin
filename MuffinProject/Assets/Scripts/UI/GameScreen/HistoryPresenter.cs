using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 일어난 일을 기록으로 쌓아 「방금 알림 칩」(FeedView, 최신 2개)과 「전체 기록 서랍」(LogSheetView)에 넣는다 (10-ui.md 2-1 HUD v2 「기록」).
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘긴다 — 화면의 다른 영역과 같은 순간에 기록된다. 판정은 하지 않는다 (1절 #1).
    /// HP 변화는 바로 앞 카드 사용의 대상이면 그 줄의 수치(−20)로 붙이고, 아니면 따로 한 줄. 카드 이름은 덱 에셋에서 찾는다.
    /// 기록은 이 화면이 열린 뒤의 것만 — 늦게 열렸을 때 지난 기록은 복구하지 않는다 (스냅샷에 없다).
    /// 첫 턴이 시작되기 전의 뽑기는 초기 손패 배분(01-game-flow.md 5 · 05-deck.md 5장)이라 기록하지 않는다 (10-ui.md 「기록 서랍」, 2026-11-08).
    /// </summary>
    public class HistoryPresenter : MonoBehaviour
    {
        private const string MyTurnHeader = "나의 턴";
        private const string TurnHeaderFormat = "{0}의 턴";
        private const string DrawText = "카드 1장 뽑기";
        private const string PlayFormat = "{0} → {1}";
        private const string HpText = "HP";
        private const string ChapchuText = "찹츄!";
        private const string SkippedText = "턴 건너뜀";
        private const string BlockNextText = "다음 턴 행동 카드 금지";
        private const string BlockThisText = "이번 턴 행동 카드 금지";
        private const string WaitStartText = "기다려! — 다음 차례까지 찹츄 승리 없음";
        private const string WaitEndText = "기다려 끝";
        private const string ChapchuHeldText = "찹츄 판정 미룸 (기다려)";
        private const string MinusFormat = "−{0}";
        private const string PlusFormat = "+{0}";
        private const string TargetSeparator = " · ";

        [SerializeField] private FeedView feed;
        [SerializeField] private LogSheetView log;
        [Tooltip("PunGameServer 와 같은 덱 에셋 — 카드 종류 ID 로 이름을 찾는다 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;

        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly HashSet<int> _waitCasters = new HashSet<int>(); // 마지막으로 본 「기다려」 를 건 사람들 — 시작 · 끝 줄을 가린다
        private readonly Dictionary<int, int> _lastHp = new Dictionary<int, int>(); // actorNumber → 마지막으로 본 HP (변화량 계산용)
        private bool _turnStarted; // 첫 턴이 시작됐는가 — 그 전의 뽑기는 초기 배분

        private void Awake()
        {
            if (feed == null || log == null || deck == null)
                Debug.LogError($"[{nameof(HistoryPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            feed.Clicked += HandleFeedClicked;
            log.CloseClicked += HandleLogCloseClicked;
        }

        private void OnDisable()
        {
            feed.Clicked -= HandleFeedClicked;
            log.CloseClicked -= HandleLogCloseClicked;
        }

        private void Start()
        {
            log.Hide();
            if (!PhotonNetwork.InRoom) return;

            // 변화량의 기준 — 지금 HP 스냅샷
            foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
                if (player.CustomProperties.TryGetValue(PlayerProps.Hp, out object hp) && hp is int hpValue)
                    _lastHp[player.ActorNumber] = hpValue;

            // 씬이 늦게 열려 첫 턴 이벤트를 놓쳤을 때 — 턴 주인이 이미 있으면 게임이 진행 중이라 그 뒤 뽑기는 기록한다
            _turnStarted = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomProps.TurnActor);
        }

        /// <summary>턴이 바뀌었다 — 머리글 (칩에는 안 뜬다).</summary>
        public void ShowTurnStarted(int actorNumber)
        {
            _turnStarted = true;
            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            _entries.Add(HistoryEntry.HeaderOf(isMe ? MyTurnHeader : string.Format(TurnHeaderFormat, PlayerLook.NameOf(actorNumber))));
            RefreshLogIfOpen();
        }

        /// <summary>뽑기 — 첫 턴 전(초기 손패 배분)이면 기록하지 않는다.</summary>
        public void ShowCardDrawn(int actorNumber)
        {
            if (!_turnStarted) return;

            Add(new HistoryEntry { Actor = actorNumber, Text = DrawText, Icon = HistoryIcon.Card });
        }

        /// <summary>카드 사용 — 「카드 이름 → 대상」. 대상이 없으면 카드 이름만.</summary>
        public void ShowCardPlayed(int actorNumber, int cardId, int[] targets)
        {
            CardData data = deck.GetCard(cardId);
            string cardName = data != null ? data.cardName : $"카드 {cardId}";
            string text = targets != null && targets.Length > 0
                ? string.Format(PlayFormat, cardName, string.Join(TargetSeparator, targets.Select(PlayerLook.NameOf)))
                : cardName;

            Add(new HistoryEntry
            {
                Actor = actorNumber, Text = text, Icon = HistoryIcon.MiniCard,
                CardType = data != null ? data.type : CardType.Action, Targets = targets ?? System.Array.Empty<int>(),
            });
        }

        /// <summary>HP 가 바뀌었다 — 바로 앞 카드 사용의 대상이면 그 줄에 수치로, 아니면 「HP −n」 한 줄.</summary>
        public void ShowHpChanged(int actorNumber, int hp)
        {
            int delta = _lastHp.TryGetValue(actorNumber, out int last) ? hp - last : 0;
            _lastHp[actorNumber] = hp;
            if (delta == 0) return;

            string value = delta < 0 ? string.Format(MinusFormat, -delta) : string.Format(PlusFormat, delta);
            HistoryEntry latest = _entries.LastOrDefault();
            if (latest != null && !latest.IsHeader && latest.Icon == HistoryIcon.MiniCard && string.IsNullOrEmpty(latest.Value)
                && latest.Targets != null && latest.Targets.Contains(actorNumber))
            {
                latest.Value = value;
                feed.Push(latest, PlayerLook.NameOf(latest.Actor), PlayerLook.InkOf(latest.Actor)); // 칩도 수치가 붙은 모양으로 — 앞 칩은 밀려난다
                RefreshLogIfOpen();
                return;
            }

            Add(new HistoryEntry { Actor = actorNumber, Text = HpText, Value = value, Icon = HistoryIcon.Card });
        }

        /// <summary>찹츄 선언 — 왕관 아이콘 한 줄. 해제는 적지 않는다.</summary>
        public void ShowChapchuDeclared(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = ChapchuText, Icon = HistoryIcon.Crown });

        /// <summary>턴이 스킵됐다 (120 얼음!).</summary>
        public void ShowTurnSkipped(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = SkippedText, Icon = HistoryIcon.Card });

        /// <summary>행동 카드 금지 (107 짖기) — 걸렸을 때 · 그 턴이 시작될 때. 풀리는 것은 적지 않는다.</summary>
        public void ShowActionBlock(int actorNumber, ActionBlock state)
        {
            if (state == ActionBlock.None) return;

            Add(new HistoryEntry { Actor = actorNumber, Text = state == ActionBlock.ThisTurn ? BlockThisText : BlockNextText, Icon = HistoryIcon.Card });
        }

        /// <summary>「기다려」 를 건 사람들이 바뀌었다 (123) — 새로 건 사람은 시작, 빠진 사람은 끝 줄.</summary>
        public void ShowWaitChanged(int[] casters)
        {
            var now = new HashSet<int>(casters ?? System.Array.Empty<int>());
            foreach (int ended in _waitCasters.Where(a => !now.Contains(a)).ToArray())
                Add(new HistoryEntry { Actor = ended, Text = WaitEndText, Icon = HistoryIcon.Card });
            foreach (int started in now.Where(a => !_waitCasters.Contains(a)).ToArray())
                Add(new HistoryEntry { Actor = started, Text = WaitStartText, Icon = HistoryIcon.Card });

            _waitCasters.Clear();
            _waitCasters.UnionWith(now);
        }

        /// <summary>찹츄 상태로 턴이 왔지만 「기다려」 라 판정을 건너뛰었다 (123).</summary>
        public void ShowChapchuWinHeld(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = ChapchuHeldText, Icon = HistoryIcon.Crown });

        private void Add(HistoryEntry entry)
        {
            _entries.Add(entry);
            feed.Push(entry, PlayerLook.NameOf(entry.Actor), PlayerLook.InkOf(entry.Actor));
            RefreshLogIfOpen();
        }

        private void RefreshLogIfOpen()
        {
            if (log.gameObject.activeSelf)
                log.Show(_entries, PlayerLook.NameOf, PlayerLook.InkOf);
        }

        private void HandleFeedClicked()
        {
            log.Show(_entries, PlayerLook.NameOf, PlayerLook.InkOf);
            feed.SetVisible(false);
        }

        private void HandleLogCloseClicked()
        {
            log.Hide();
            feed.SetVisible(true);
        }
    }
}
