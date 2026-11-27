using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 일어난 일을 기록으로 쌓고, 카드 사용 · 함정 설치 · 함정 발동은 왼쪽 「낸 카드 기록」(HistoryListView)에 한 줄씩 띄운다 (통합 화면 UI v3 .hist).
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘긴다 — 화면의 다른 영역과 같은 순간에 기록된다. 판정은 하지 않는다 (10-ui.md 1절 #1).
    /// 줄의 결과(−HP 20 · 행동 금지 · 2장 뽑기 …)는 그 카드의 효과가 적용되는 동안(OnCardResolved 뒤 ~ 다음 카드 · 체인 끝 · 턴 시작) 온 결과 알림에서 붙인다. 결과가 없으면 「사용」.
    /// 무효가 된 카드는 흐리게 + 줄, 무효로 만든 카드의 결과는 「무효」. 카드 이름 · 그림은 덱 에셋에서 찾는다.
    /// 기록은 이 화면이 열린 뒤의 것만 — 늦게 열렸을 때 지난 기록은 복구하지 않는다 (스냅샷에 없다).
    /// 첫 턴이 시작되기 전의 뽑기는 초기 손패 배분(01-game-flow.md 5 · 05-deck.md 5장)이라 기록하지 않는다.
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
        private const string HostChangedText = "방장이 바뀌었어요";
        private const string BlockNextText = "다음 턴 행동 카드 금지";
        private const string BlockThisText = "이번 턴 행동 카드 금지";
        private const string WaitStartText = "기다려! — 다음 턴까지 찹츄 승리 없음";
        private const string WaitEndText = "기다려 끝";
        private const string ChapchuHeldText = "찹츄 판정 미룸 (기다려)";
        private const string RestartFormat = "대소동! 게임을 새로 시작 ({0}번째)";
        private const string NewRoundFormat = "새 판: {0} 순서, {1}부터";
        private const string OrderSeparator = " → ";
        private const string FalloutFormat = "피폭 피해: 모두 {0} ({1}턴째)";
        private const string GaepanStartFormat = "개판 오분전! {0}바퀴 뒤 반반 확률로 HP 꼴찌 또는 손패 꼴찌가 즉시 사망 (동점이면 무작위 1명)";
        private const string GaepanAgainText = "개판 오분전 다시 처음부터!";
        private const string GaepanLeftFormat = "개판 오분전: {0}바퀴 남음";
        private const string GaepanJudgeFormat = "개판 오분전 종료: 기준 {0} 꼴찌 — {1}{2} → 즉시 사망";
        private const string GaepanJudgeTiedFormat = "개판 오분전 종료: 기준 {0} 꼴찌 — {1}{2} · 동점 {3} 중 무작위 → 즉시 사망";
        private const string GaepanHpValueFormat = "(HP {0})";
        private const string GaepanCardValueFormat = "({0}장)";
        private const string GaepanByHp = "HP";
        private const string GaepanByCard = "손패";
        private const string NameSeparator = ", ";
        private const string ChoiceDiscardText = "목표 조준 — 손패 전부 버리기";
        private const string ChoiceDamageText = "목표 조준 — 피해 받기";
        private const string ChoiceLeftText = "목표 조준 — 나가서 불발";
        private const string ChoiceTimeoutSuffix = " (시간 초과 · 무작위)";
        private const string MinusSign = "−";
        private const string PlusSign = "+";
        private const string UnknownCardFormat = "카드 {0}";
        private const string CostTimeoutText = "버릴 카드를 안 골라 무작위로 버렸어요";
        // 카운터가 한 일 (10-ui.md 5절 「카운터 기록 줄」, 사용자 2026-11-24). {0} = 카드 이름 · 사람 이름 · 수치
        private const string NegatedFormat = "{0} 무효";
        private const string ReturnedFormat = "{0} — 손패로 돌아감";
        private const string TakenFormat = "{0} — 꿀꺽";
        private const string TakeTurnFormat = "새치기! 다음 턴은 {0}";
        private const string DoubledFormat = "따블! {0} 2배";
        private const string ReflectedFormat = "반사! → {0}";
        private const string RedirectedFormat = "대상 변경 → {0}";
        private const string SharedFormat = "{0}도 같은 피해";
        private const string ReducedFormat = "피해 −{0}";
        private const string HandLossBlockedText = "손패 감소 막음";
        private const string TargetSeparator = " · ";

        // 「낸 카드 기록」 줄 (통합 화면 UI v3 .hit)
        private const string MeName = "나";
        private const string TrapSetLabel = "함정";
        private const string TrapSetResult = "함정 설치";
        private const string TrapUsedResult = "함정 발동";
        private const string UsedResult = "사용";
        private const string VoidResult = "무효";
        private const string BlockResult = "행동 금지";
        private const string SkipResult = "턴 건너뜀";
        private const string DrawResultFormat = "{0}장 뽑기";
        private const string TakeResultFormat = "{0}장 가져옴";
        private const string GiveResultFormat = "{0}장 넘김";
        private const string DiscardResultFormat = "{0}장 버림";
        private const string TrapBrokenResult = "함정 파괴";
        private const string DiscardTakenResult = "버림 더미에서 1장";
        private const string DirectionResult = "방향 반대로";
        private const string WaitResult = "기다려!";
        private const string WhoFormat = "<color=#{0}>{1}</color>";
        private const string ArrowFormat = " <color=#{0}>→</color> ";
        private const string HpFormat = "{0}HP <size={3}><color=#{2}>{1}</color></size>";
        private const string ResultFormat = "<size={2}><color=#{1}>{0}</color></size>";

        [Tooltip("왼쪽 「낸 카드 기록」.")]
        [SerializeField] private HistoryListView list;
        [Tooltip("PunGameServer 와 같은 덱 에셋 — 카드 종류 ID 로 이름 · 그림을 찾는다 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;
        [Tooltip("함정 설치 줄의 그림 — 종류는 비공개라 카드 뒷면.")]
        [SerializeField] private Sprite cardBack;

        [Header("색 (통합 화면 UI v3)")]
        [SerializeField] private Color actionColor = new Color32(0x00, 0x9d, 0xff, 0xFF);
        [SerializeField] private Color counterColor = new Color32(0x6f, 0xa4, 0x34, 0xFF);
        [SerializeField] private Color trapColor = new Color32(0xf6, 0x46, 0x12, 0xFF);
        [Tooltip("「→」 · 결과 한 마디 글자색.")]
        [SerializeField] private Color softInk = new Color32(0x6e, 0x64, 0x5c, 0xFF);
        [SerializeField] private Color hpLossColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color hpGainColor = new Color32(0x3c, 0xbf, 0x72, 0xFF);
        [Tooltip("HP 수치 크기 (시안 .fx b 19 × 2).")]
        [SerializeField] private float hpNumberSize = 38f;
        [Tooltip("결과 한 마디 크기 (시안 .fx.res 15 × 2).")]
        [SerializeField] private float resultSize = 30f;

        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly HashSet<int> _waitCasters = new HashSet<int>(); // 마지막으로 본 「기다려」 를 건 사람들 — 시작 · 끝 줄을 가린다
        private readonly Dictionary<int, int> _lastHp = new Dictionary<int, int>(); // actorNumber → 마지막으로 본 HP (변화량 계산용)
        private bool _turnStarted; // 첫 턴이 시작됐는가 — 그 전의 뽑기는 초기 배분
        private int _turnActor = -1; // 지금 턴 주인 — 피폭 줄(턴 끝)을 그 사람 이름으로
        private HistoryEntry _resolving; // 효과가 적용되는 중인 카드 줄 — 뒤따르는 결과 알림을 여기에 붙인다
        private int _resolvingDraws; // 그 카드의 효과로 뽑은 장수
        // 지금 체인에 놓인 카드 (낸 순서, 끝이 맨 위) — 카운터가 처리될 때 「그 아래 카드」 의 이름 · 쓴 사람을 기록 줄에 적는다.
        // 처리는 위에서부터라 결과 알림이 올 때마다 끝에서 하나씩 뺀다. 화면만의 기억이다 (중간에 들어와 비어 있으면 그 줄은 건너뛴다)
        private readonly List<(int Actor, int CardId)> _chain = new List<(int Actor, int CardId)>();

        private void Awake()
        {
            if (list == null || deck == null || cardBack == null)
                Debug.LogError($"[{nameof(HistoryPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void Start()
        {
            if (!PhotonNetwork.InRoom) return;

            // 변화량의 기준 — 지금 HP 스냅샷 (봇의 값은 방 속성에 있다, 18-bot.md 9절)
            foreach (int actor in PlayerLook.OrderedActors())
                if (BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object hp) && hp is int hpValue)
                    _lastHp[actor] = Mathf.Clamp(hpValue, 0, GameServer.MaxHp); // 남이 잠깐 바꾼 값일 수 있다 (09-network.md 13절 #8)

            // 씬이 늦게 열려 첫 턴 이벤트를 놓쳤을 때 — 턴 주인이 이미 있으면 게임이 진행 중이라 그 뒤 뽑기는 기록한다
            _turnStarted = PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomProps.TurnActor);
        }

        /// <summary>턴이 바뀌었다 — 머리글 (낸 카드 기록에는 안 뜬다).</summary>
        public void ShowTurnStarted(int actorNumber)
        {
            _turnStarted = true;
            _turnActor = actorNumber;
            _resolving = null;
            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            _entries.Add(HistoryEntry.HeaderOf(isMe ? MyTurnHeader : string.Format(TurnHeaderFormat, PlayerLook.NameOf(actorNumber))));
        }

        /// <summary>뽑기 — 첫 턴 전(초기 손패 배분)이면 기록하지 않는다. 카드 효과로 뽑았으면 그 줄에 「n장 뽑기」.</summary>
        public void ShowCardDrawn(int actorNumber)
        {
            if (!_turnStarted) return;

            if (_resolving != null && string.IsNullOrEmpty(_resolving.Value) && (_resolving.Result == null || _resolvingDraws > 0))
            {
                _resolvingDraws++;
                _resolving.Result = string.Format(DrawResultFormat, _resolvingDraws);
                Redraw(_resolving);
            }
            Add(new HistoryEntry { Actor = actorNumber, Text = DrawText, Icon = HistoryIcon.Card });
        }

        /// <summary>카드 사용 — 「카드 이름 → 대상」 + 낸 카드 기록 한 줄. 대상이 없으면 카드 이름만.</summary>
        public void ShowCardPlayed(int actorNumber, int cardInstanceId, int cardId, int[] targets)
        {
            _resolving = null;
            CardData data = deck.GetCard(cardId);
            string cardName = NameOf(data, cardId);
            if (data == null || data.type != CardType.Counter)
                _chain.Clear(); // 카운터가 아니면 새 체인의 첫 카드다
            _chain.Add((actorNumber, cardId));
            string text = targets != null && targets.Length > 0
                ? string.Format(PlayFormat, cardName, string.Join(TargetSeparator, targets.Select(PlayerLook.NameOf)))
                : cardName;

            Add(new HistoryEntry
            {
                Actor = actorNumber, Text = text, Icon = HistoryIcon.MiniCard, InList = true, CardId = cardId, InstanceId = cardInstanceId,
                CardType = data != null ? data.type : CardType.Action, Targets = targets ?? System.Array.Empty<int>(),
            });
        }

        /// <summary>함정 설치 — 종류는 비공개라 카드 뒷면 · 「함정 설치」.</summary>
        public void ShowTrapSet(int actorNumber)
        {
            Add(new HistoryEntry
            {
                Actor = actorNumber, Text = TrapSetResult, Icon = HistoryIcon.MiniCard, InList = true, IsTrapSet = true, CardType = CardType.Trap,
                Targets = System.Array.Empty<int>(),
            });
        }

        /// <summary>함정 발동 — 공개된 함정 카드 한 줄. 결과가 없으면 「함정 발동」.</summary>
        public void ShowTrapActivated(int actorNumber, int cardInstanceId, int cardId, int[] targets)
        {
            _resolving = null;
            CardData data = deck.GetCard(cardId);
            Add(new HistoryEntry
            {
                Actor = actorNumber, Text = NameOf(data, cardId), Icon = HistoryIcon.MiniCard, InList = true, IsTrap = true, CardId = cardId, InstanceId = cardInstanceId,
                CardType = CardType.Trap, Targets = targets ?? System.Array.Empty<int>(),
            });
        }

        /// <summary>
        /// 카드 효과 적용 (OnCardResolved) — 뒤따르는 결과 알림을 그 카드 줄에 붙인다.
        /// 카운터면 그 카드가 한 일을 효과마다 한 줄씩 (10-ui.md 5절 「카운터 기록 줄」). targets = 처리 알림의 대상.
        /// </summary>
        public void ShowCardResolved(int actorNumber, int cardInstanceId, int cardId, int[] targets)
        {
            _resolving = _entries.LastOrDefault(entry => entry.InList && !entry.IsTrapSet && entry.InstanceId == cardInstanceId);
            _resolvingDraws = 0;

            if (!LeaveChain(cardId)) return;

            CardData data = deck.GetCard(cardId);
            if (data == null || data.type != CardType.Counter || _chain.Count == 0) return;

            (int belowActor, int belowCardId) = _chain[_chain.Count - 1];
            string below = NameOfCard(belowCardId);
            string picked = string.Join(TargetSeparator, (targets ?? System.Array.Empty<int>()).Select(PlayerLook.NameOf));
            foreach (CardEffect effect in data.effects)
            {
                string text = effect switch
                {
                    NegateEffect negate when !negate.selfOnly => string.Format(NegatedFormat, below),
                    ReturnToHandEffect back => string.Format(back.to == ReturnReceiver.CounterUser ? TakenFormat : ReturnedFormat, below),
                    TakeTurnEffect _ => string.Format(TakeTurnFormat, PlayerLook.NameOf(actorNumber)),
                    DoubleBelowEffect _ when _chain.Count >= 2 => string.Format(DoubledFormat, NameOfCard(_chain[_chain.Count - 2].CardId)), // 막은 카운터의 아래 카드
                    ReflectEffect _ => string.Format(ReflectedFormat, PlayerLook.NameOf(belowActor)),
                    RedirectEffect _ => string.Format(RedirectedFormat, picked),
                    ShareDamageEffect _ => string.Format(SharedFormat, picked),
                    ReduceDamageEffect reduce => string.Format(ReducedFormat, reduce.amount),
                    NegateHandLossEffect _ => HandLossBlockedText,
                    _ => null,
                };
                if (text != null)
                    Add(new HistoryEntry { Actor = actorNumber, Text = text, Icon = HistoryIcon.Card });
            }
        }

        /// <summary>카드가 무효가 됐다 — 그 줄은 흐리게 + 줄, 무효로 만든 카드(지금 적용 중)의 결과는 「무효」.</summary>
        public void ShowCardNegated(int cardInstanceId, int cardId)
        {
            LeaveChain(cardId);
            SetResult(VoidResult);

            HistoryEntry entry = _entries.LastOrDefault(e => e.InList && !e.IsTrapSet && e.InstanceId == cardInstanceId);
            if (entry == null) return;

            entry.Void = true;
            if (string.IsNullOrEmpty(entry.Value) && entry.Result == null)
                entry.Result = VoidResult;
            Redraw(entry);
        }

        /// <summary>체인의 카드가 효과 없이 손패로 돌아갔다 — 기록 줄은 막은 카운터가 적었다.</summary>
        public void ShowCardLeftChain(int cardId) => LeaveChain(cardId);

        /// <summary>체인이 끝났다 (OnChainDiscarded) — 결과를 더 붙이지 않는다.</summary>
        public void ShowChainEnded()
        {
            _resolving = null;
            _chain.Clear();
        }

        // 맨 위 카드가 cardId 면 체인 기억에서 뺀다. 아니면(체인에 끼어든 함정 · 중간에 들어와 모르는 카드) 그대로 두고 false
        private bool LeaveChain(int cardId)
        {
            if (_chain.Count == 0 || _chain[_chain.Count - 1].CardId != cardId) return false;

            _chain.RemoveAt(_chain.Count - 1);
            return true;
        }

        private string NameOfCard(int cardId) => NameOf(deck.GetCard(cardId), cardId);

        /// <summary>HP 가 바뀌었다 — 적용 중인 카드 줄에 「−HP n」, 아니면 기록에 「HP −n」 한 줄.</summary>
        public void ShowHpChanged(int actorNumber, int hp)
        {
            int delta = _lastHp.TryGetValue(actorNumber, out int last) ? hp - last : 0;
            _lastHp[actorNumber] = hp;
            if (delta == 0) return;

            string value = (delta < 0 ? MinusSign : PlusSign) + Mathf.Abs(delta);
            if (_resolving != null && string.IsNullOrEmpty(_resolving.Value))
            {
                _resolving.Value = value;
                Redraw(_resolving);
                return;
            }

            Add(new HistoryEntry { Actor = actorNumber, Text = HpText, Value = value, Icon = HistoryIcon.Card });
        }

        /// <summary>찹츄 선언 — 왕관 아이콘 한 줄. 해제는 적지 않는다.</summary>
        public void ShowChapchuDeclared(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = ChapchuText, Icon = HistoryIcon.Crown });

        /// <summary>턴이 스킵됐다 (1020 얼음!).</summary>
        public void ShowTurnSkipped(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = SkippedText, Icon = HistoryIcon.Card });

        /// <summary>다음 턴 스킵이 걸렸다 (OnTurnSkipMarked) — 적용 중인 카드 줄에 「턴 건너뜀」.</summary>
        public void ShowTurnSkipMarked(int actorNumber) => SetResult(SkipResult);

        /// <summary>행동 카드 금지 (1007 짖기) — 걸렸을 때 · 그 턴이 시작될 때. 풀리는 것은 적지 않는다. 적용 중인 카드 줄에 「행동 금지」.</summary>
        public void ShowActionBlock(int actorNumber, ActionBlock state)
        {
            if (state == ActionBlock.None) return;

            SetResult(BlockResult);
            Add(new HistoryEntry { Actor = actorNumber, Text = state == ActionBlock.ThisTurn ? BlockThisText : BlockNextText, Icon = HistoryIcon.Card });
        }

        /// <summary>손패 이동 (훔치기 · 교환) — 적용 중인 카드 줄에 「n장 가져옴 · 넘김」.</summary>
        public void ShowHandCardsMoved(int from, int to, int count)
        {
            if (_resolving == null) return;

            SetResult(string.Format(to == _resolving.Actor ? TakeResultFormat : GiveResultFormat, count));
        }

        /// <summary>강제 버림 — 적용 중인 카드 줄에 「n장 버림」.</summary>
        public void ShowCardsDiscarded(int count) => SetResult(string.Format(DiscardResultFormat, count));

        /// <summary>함정 파괴 — 적용 중인 카드 줄에 「함정 파괴」.</summary>
        public void ShowTrapDestroyed() => SetResult(TrapBrokenResult);

        /// <summary>버림 더미 맨 위를 손패로 (1009) — 적용 중인 카드 줄에.</summary>
        public void ShowDiscardTaken() => SetResult(DiscardTakenResult);

        /// <summary>턴 진행 방향이 바뀌었다 (1045) — 적용 중인 카드 줄에.</summary>
        public void ShowDirectionChanged() => SetResult(DirectionResult);

        /// <summary>「기다려」 를 건 사람들이 바뀌었다 (1023) — 새로 건 사람은 시작, 빠진 사람은 끝 줄.</summary>
        public void ShowWaitChanged(int[] casters)
        {
            var now = new HashSet<int>(casters ?? System.Array.Empty<int>());
            foreach (int ended in _waitCasters.Where(a => !now.Contains(a)).ToArray())
                Add(new HistoryEntry { Actor = ended, Text = WaitEndText, Icon = HistoryIcon.Card });
            foreach (int started in now.Where(a => !_waitCasters.Contains(a)).ToArray())
            {
                SetResult(WaitResult);
                Add(new HistoryEntry { Actor = started, Text = WaitStartText, Icon = HistoryIcon.Card });
            }

            _waitCasters.Clear();
            _waitCasters.UnionWith(now);
        }

        /// <summary>
        /// 대소동으로 새 판 (1012) — 쓴 사람 줄 + 「새 판: 순서」 머리글. 기록은 이어진다. 새 판의 HP 100 은 변화로 적지 않고, 첫 턴 전 배분 뽑기도 적지 않는다.
        /// </summary>
        public void ShowGameRestarted(int user, int count, int[] order)
        {
            _resolving = null;
            Add(new HistoryEntry { Actor = user, Text = string.Format(RestartFormat, count), Icon = HistoryIcon.Card });

            string[] names = order.Select(PlayerLook.NameOf).ToArray();
            _entries.Add(HistoryEntry.HeaderOf(string.Format(NewRoundFormat, string.Join(OrderSeparator, names), names.FirstOrDefault())));

            foreach (int actor in order)
                _lastHp[actor] = GameServer.MaxHp;
            _waitCasters.Clear();
            _turnStarted = false;
        }

        /// <summary>게임 중에 방장이 바뀌었다 — 판은 그대로 이어진다 (09-network.md 9.1). 지금 턴 아래에 한 줄.</summary>
        public void ShowHostChanged() => Add(new HistoryEntry { Actor = _turnActor, Text = HostChangedText, Icon = HistoryIcon.Card });

        /// <summary>피폭 (1012) — 턴이 끝나 모두 k^n 피해를 입었으면 한 줄. 새 판(k 0)은 적지 않는다.</summary>
        public void ShowFallout(int n, int turns)
        {
            if (turns <= 0) return;

            Add(new HistoryEntry { Actor = _turnActor, Text = string.Format(FalloutFormat, GameServer.FalloutDamage(turns, n), turns), Icon = HistoryIcon.Card });
        }

        /// <summary>개판 오분전 (1021) — 시작 · 다시 처음부터 · 남은 바퀴. 대소동으로 지워짐(caster -1)은 적지 않는다. 문구는 프로토타입 v2.</summary>
        public void ShowGaepan(int caster, int left, bool again, bool started)
        {
            if (caster < 0)
                return;
            if (again)
                Add(new HistoryEntry { Actor = caster, Text = GaepanAgainText, Icon = HistoryIcon.Card });
            if (started)
                Add(new HistoryEntry { Actor = caster, Text = string.Format(GaepanStartFormat, left), Icon = HistoryIcon.Card });
            else if (left > 0) // 0 은 판정 직전 — 판정 줄이 따로 적는다
                Add(new HistoryEntry { Actor = caster, Text = string.Format(GaepanLeftFormat, left), Icon = HistoryIcon.Card });
        }

        /// <summary>개판 오분전 판정 (1021) — 기준 · 꼴찌 · 동점. HP 0 은 뒤따르는 HP 변화가 적는다.</summary>
        public void ShowGaepanJudged(int loser, bool byHp, int value, int[] tied)
        {
            string valueText = string.Format(byHp ? GaepanHpValueFormat : GaepanCardValueFormat, value);
            string by = byHp ? GaepanByHp : GaepanByCard;
            string text = tied.Length > 1
                ? string.Format(GaepanJudgeTiedFormat, by, PlayerLook.NameOf(loser), valueText, string.Join(NameSeparator, tied.Select(PlayerLook.NameOf)))
                : string.Format(GaepanJudgeFormat, by, PlayerLook.NameOf(loser), valueText);
            Add(new HistoryEntry { Actor = loser, Text = text, Icon = HistoryIcon.Card });
        }

        /// <summary>1019 목표 조준 — 대상이 고른 것. 버림 · 피해 수치는 뒤따르는 사건이 적는다.</summary>
        public void ShowChoiceMade(int actor, int option, bool timedOut, int kind)
        {
            // 손패에서 1장 버리기(카운터의 대가)는 뒤따르는 버림 줄이 적는다 — 시간 초과로 무작위였을 때만 알린다
            if (kind == Chapchu.Game.ChoiceKind.HandCard)
            {
                if (timedOut)
                    Add(new HistoryEntry { Actor = actor, Text = CostTimeoutText, Icon = HistoryIcon.Card });
                return;
            }

            string text = option < 0 ? ChoiceLeftText : option == 0 ? ChoiceDiscardText : ChoiceDamageText;
            Add(new HistoryEntry { Actor = actor, Text = timedOut ? text + ChoiceTimeoutSuffix : text, Icon = HistoryIcon.Card });
        }

        /// <summary>찹츄 상태로 턴이 왔지만 「기다려」 라 판정을 건너뛰었다 (1023).</summary>
        public void ShowChapchuWinHeld(int actorNumber) => Add(new HistoryEntry { Actor = actorNumber, Text = ChapchuHeldText, Icon = HistoryIcon.Crown });

        private void Add(HistoryEntry entry)
        {
            _entries.Add(entry);
            if (entry.InList)
                Draw(list.Push(entry), entry);
        }

        // 적용 중인 카드 줄의 결과 한 마디 — 먼저 붙은 결과(HP 포함)가 있으면 그대로 둔다
        private void SetResult(string result)
        {
            if (_resolving == null || !string.IsNullOrEmpty(_resolving.Value) || _resolving.Result != null) return;

            _resolving.Result = result;
            Redraw(_resolving);
        }

        private void Redraw(HistoryEntry entry)
        {
            HistoryItemView item = list.Find(entry);
            if (item != null)
                Draw(item, entry);
        }

        private void Draw(HistoryItemView item, HistoryEntry entry)
        {
            CardData data = entry.IsTrapSet ? null : deck.GetCard(entry.CardId);
            string label = entry.IsTrapSet ? TrapSetLabel : NameOf(data, entry.CardId);
            Sprite picture = entry.IsTrapSet ? cardBack : data != null ? data.LoadArt() : null;
            Color kind = entry.CardType == CardType.Counter ? counterColor : entry.CardType == CardType.Trap ? trapColor : actionColor;
            item.Show(label, picture, kind, WhoOf(entry), FxOf(entry), !string.IsNullOrEmpty(entry.Value), entry.Void);
        }

        // 「누가 → 누구」 — 이름은 그 사람 글자색, 나는 「나」
        private string WhoOf(HistoryEntry entry)
        {
            string who = NameTag(entry.Actor);
            if (entry.Targets == null || entry.Targets.Length == 0)
                return who;

            return who + string.Format(ArrowFormat, ColorUtility.ToHtmlStringRGB(softInk)) + string.Join(TargetSeparator, entry.Targets.Select(NameTag));
        }

        private string FxOf(HistoryEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Value))
            {
                bool loss = entry.Value.StartsWith(MinusSign);
                return string.Format(HpFormat, loss ? MinusSign : PlusSign, entry.Value.Substring(1), ColorUtility.ToHtmlStringRGB(loss ? hpLossColor : hpGainColor), hpNumberSize);
            }

            string result = entry.Result ?? (entry.IsTrapSet ? TrapSetResult : entry.IsTrap ? TrapUsedResult : UsedResult);
            return string.Format(ResultFormat, result, ColorUtility.ToHtmlStringRGB(softInk), resultSize);
        }

        private static string NameTag(int actorNumber)
        {
            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            return string.Format(WhoFormat, ColorUtility.ToHtmlStringRGB(PlayerLook.InkOf(actorNumber)), isMe ? MeName : PlayerLook.NameOf(actorNumber));
        }

        private static string NameOf(CardData data, int cardId) => data != null ? data.cardName : string.Format(UnknownCardFormat, cardId);
    }
}
