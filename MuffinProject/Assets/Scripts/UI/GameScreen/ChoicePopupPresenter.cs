using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 효과 고르기 판 (1019 목표 조준, PM 2026-11-27). 쓴 카드(체인의 카드를 판 왼쪽으로) · 고를 카드 두 장 · 남은 시간 막대.
    /// 「손패에서 1장」 고르기 (2013 · 2021 의 대가)는 같은 판의 오른쪽에 내 손패를 카드 칸으로 늘어놓고, 누른 카드의 장 번호를 보낸다 (10-ui.md 5절).
    /// 내가 고르는 사람이면 고르기가 시작되자마자 **바로** 연다 (입력이라 연출 큐를 기다리지 않는다 — 5초 안에 골라야 한다).
    /// 고르면 방장에게 요청만 보낸다 — 시간 · 고르는 사람 판정은 방장 (09-network.md 2절). 거절되면 마감 전까지 다시 누를 수 있다.
    /// 남이 고르는 중이면 GameScreenPresenter 가 연출 큐 순서대로 ShowWatching · PickRoutine 을 부른다 — 같은 카드에 고르는 사람의 숫자 (손패 고르기는 오른쪽을 비운다).
    /// </summary>
    [RequireComponent(typeof(ChoicePopupView))]
    public class ChoicePopupPresenter : MonoBehaviour
    {
        private const string ChooseHintFormat = "둘 중 하나를 눌러요 · {0}초 지나면 무작위";
        private const string CostHintFormat = "{0} — 버릴 카드를 눌러요 · {1}초 지나면 무작위";
        private const string WatchHintFormat = "{0}{1} 고르는 중";
        private const string WatchCostHintFormat = "{0}{1} 버릴 카드 고르는 중";
        private const string PickedHintFormat = "{0} — {1}!";
        private const string TagFormat = "{0} → {1}";
        private const string MeName = "나";

        [SerializeField] private PunGameServer server;
        [Tooltip("쓴 카드 — 판이 열리면 체인의 카드를 판 왼쪽 자리로 옮긴다.")]
        [SerializeField] private ChainCardsPresenter chainCards;
        [Tooltip("내 손패 — 「손패에서 1장」 고르기에 늘어놓을 카드.")]
        [SerializeField] private HandView hand;

        private ChoicePopupView _view;
        private bool _open;
        private bool _mine;       // 내가 고르는 판 — 고르기 끝 알림을 바로 받는다
        private bool _picking;    // 고른 연출 중 — 남은 시간을 더 줄이지 않는다
        private bool _requested;  // 내가 눌러 방장에게 보냈다 — 고른 연출은 방장이 확정한 값(ChoiceMade)으로
        private int _actor;       // 지금 판의 고르는 사람
        private int _kind;        // 지금 판의 고르기 종류 (ChoiceKind)
        private int _generation;  // 판을 열 때마다 올린다 — 앞 판의 연출이 끝나며 새 판을 닫지 않게
        private double _startsAt;
        private double _deadline;

        // 버림 알림은 왔지만 손패 화면(연출 큐)에서는 아직 안 빠졌을 수 있는 내 카드 — 고르기 칸에서 뺀다. 다시 손패로 들어오면 지운다.
        private readonly HashSet<int> _discarded = new HashSet<int>();

        private void Awake()
        {
            _view = GetComponent<ChoicePopupView>();
            if (server == null || chainCards == null || hand == null)
                Debug.LogError($"[{nameof(ChoicePopupPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.Picked += HandlePicked;
            _view.CardPicked += HandlePicked;
            GameEvents.OnChoiceStarted += HandleChoiceStarted;
            GameEvents.OnChoiceMade += HandleChoiceMade;
            GameEvents.OnMyRequestRejected += HandleMyRequestRejected;
        }

        private void OnDisable()
        {
            _view.Picked -= HandlePicked;
            _view.CardPicked -= HandlePicked;
            GameEvents.OnChoiceStarted -= HandleChoiceStarted;
            GameEvents.OnChoiceMade -= HandleChoiceMade;
            GameEvents.OnMyRequestRejected -= HandleMyRequestRejected;
            StopAllCoroutines();
            _open = false;
            _picking = false;
            _view.Hide();
            if (chainCards != null) // 씬을 닫을 때는 먼저 지워졌을 수 있다
                chainCards.Unfocus();
        }

        private void Update()
        {
            if (!_open || _picking)
                return;

            double total = _deadline - _startsAt;
            _view.SetFraction(total > 0 ? (float)((_deadline - GameClock.Now) / total) : 0f);
        }

        // 내가 고르는 사람 — 바로 연다. HP 는 지금 방 속성 값 (피해가 들어가기 전).
        private void HandleChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline, int kind)
        {
            if (!IsMe(actor))
                return;

            int seconds = Mathf.RoundToInt((float)(deadline - startsAt));
            if (kind == ChoiceKind.HandCard)
            {
                var ids = new List<int>();
                var cards = new List<CardData>();
                foreach (CardView card in hand.Cards)
                {
                    CardData data = server.Deck.GetCard(card.CardId);
                    if (data == null || _discarded.Contains(card.InstanceId))
                        continue;
                    ids.Add(card.InstanceId);
                    cards.Add(data);
                }

                OpenCards(actor, user, cardId, startsAt, deadline, string.Format(CostHintFormat, CardName(cardId), seconds), ids, cards, true);
                return;
            }

            int hp = BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object value) && value is int hpValue ? hpValue : GameServer.MaxHp;
            Open(actor, user, cardId, handCount, hp, damage, startsAt, deadline, string.Format(ChooseHintFormat, seconds), true);
        }

        /// <summary>남이 고르는 중 — 같은 판을 누를 수 없게 연다. hp = 화면에 재생한 고르는 사람의 HP. 연출 큐가 부른다. 내 판이 열려 있으면 덮지 않는다.</summary>
        public void ShowWatching(int actor, int user, int cardId, int handCount, int hp, int damage, double startsAt, double deadline, int kind)
        {
            if (_open && _mine)
                return;

            string name = PlayerLook.NameOf(actor);
            if (kind == ChoiceKind.HandCard) // 손패는 비공개 — 오른쪽을 비운다
            {
                OpenCards(actor, user, cardId, startsAt, deadline, string.Format(WatchCostHintFormat, name, SubjectParticle(name)), new List<int>(), new List<CardData>(), false);
                return;
            }

            Open(actor, user, cardId, handCount, hp, damage, startsAt, deadline, string.Format(WatchHintFormat, name, SubjectParticle(name)), false);
        }

        /// <summary>남이 골랐다 — 고른 카드를 튀어나오게 하고 판을 닫는다. 연출 큐가 기다릴 코루틴. 그 사람의 판이 열려 있지 않으면 바로 끝난다.</summary>
        public IEnumerator PickRoutine(int actor, int option)
        {
            if (!_open || _mine || actor != _actor)
                yield break;

            yield return Pick(actor, option);
        }

        private void Open(int actor, int user, int cardId, int handCount, int hp, int damage, double startsAt, double deadline, string hint, bool interactive)
        {
            Begin(actor, ChoiceKind.Options, startsAt, deadline, interactive);
            _view.Show(hint, Tag(user, actor), handCount, hp, damage, interactive);
            chainCards.Focus(cardId, _view.UsedCardCenter, _view.UsedCardHeight);
            Update();
        }

        private void OpenCards(int actor, int user, int cardId, double startsAt, double deadline, string hint, List<int> ids, List<CardData> cards, bool interactive)
        {
            Begin(actor, ChoiceKind.HandCard, startsAt, deadline, interactive);
            _view.ShowCards(hint, Tag(user, actor), ids, cards);
            chainCards.Focus(cardId, _view.UsedCardCenter, _view.UsedCardHeight);
            Update();
        }

        private void Begin(int actor, int kind, double startsAt, double deadline, bool interactive)
        {
            StopAllCoroutines();
            _generation++;
            _open = true;
            _mine = interactive;
            _picking = false;
            _requested = false;
            _actor = actor;
            _kind = kind;
            _startsAt = startsAt;
            _deadline = deadline;
        }

        // 시간이 지나 방장이 무작위로 골랐거나, 내가 고른 것이 확정됐다 — 아직 고른 연출 전이면 그 카드로.
        private void HandleChoiceMade(int actor, int option, bool timedOut, int kind)
        {
            if (!_open || !_mine || !IsMe(actor) || _picking)
                return;

            StartCoroutine(Pick(actor, option));
        }

        // 고르기 요청이 거절됐다 (늦게 닿음 · 없는 장 번호 등) — 고르기는 방장에서 그대로 열려 있으니 마감 전이면 다시 누를 수 있다
        private void HandleMyRequestRejected(int code)
        {
            if (!_open || !_mine || !_requested || code != RejectCode.NotChoosing)
                return;
            if (GameClock.Now < _deadline)
                _requested = false;
        }

        // 0 · 1 = 고를 카드 두 장, 손패 고르기면 누른 카드의 장 번호
        private void HandlePicked(int option)
        {
            if (!_open || !_mine || _picking || _requested)
                return;

            // 요청만 보낸다 — 고른 연출은 방장이 확정한 값으로 (HandleChoiceMade). 늦게 닿아 거절되면 방장이 무작위로 고른 쪽이 보인다
            _requested = true;
            server.RequestChoose(option, _deadline);
        }

        // 버림 알림은 연출 큐(GameScreenPresenter)가 알림을 받은 자리에서 바로 부른다 — 큐 순서를 기다리지 않는다 (CODE_CONVENTION 9절).
        /// <summary>내 손패의 카드가 버려졌다.</summary>
        public void NoteDiscarded(int[] cardInstanceIds) => _discarded.UnionWith(cardInstanceIds);

        /// <summary>카드가 버림 더미를 떠났다 (드로우 · 버림 더미에서 가져감 · 손패로 반환).</summary>
        public void NoteLeftDiscard(int cardInstanceId) => _discarded.Remove(cardInstanceId);

        /// <summary>카드들이 버림 더미를 떠나 내 손패로 들어왔다.</summary>
        public void NoteLeftDiscard(int[] cardInstanceIds) => _discarded.ExceptWith(cardInstanceIds);

        /// <summary>버림 더미가 비었다 (덱 리필 · 대폭발).</summary>
        public void ClearDiscarded() => _discarded.Clear();

        // 고른 카드를 튀어나오게 하고 판을 닫는다. option < 0 (고르는 사람이 나가 불발) 이거나 손패 고르기면 고른 연출 없이 닫는다.
        // 그 사이 새 판이 열렸으면(세대가 바뀜) 닫지 않는다 — 남의 판 연출은 연출 큐의 코루틴이라 Begin 의 StopAllCoroutines 로 멈추지 않는다.
        private IEnumerator Pick(int actor, int option)
        {
            int generation = _generation;
            if (option >= 0 && _kind == ChoiceKind.Options)
            {
                _picking = true;
                _view.SetHint(string.Format(PickedHintFormat, ShortName(actor), _view.OptionName(option)));
                yield return _view.PickRoutine(option);
                if (generation != _generation)
                    yield break;
            }

            _view.Hide();
            _open = false;
            _picking = false;
            chainCards.Unfocus();
        }

        private string CardName(int cardId)
        {
            CardData data = server.Deck.GetCard(cardId);
            return data != null ? data.cardName : string.Empty;
        }

        private static string Tag(int user, int actor) => string.Format(TagFormat, ShortName(user), ShortName(actor));

        private static bool IsMe(int actor) => actor == PhotonNetwork.LocalPlayer.ActorNumber;

        private static string ShortName(int actor) => IsMe(actor) ? MeName : PlayerLook.NameOf(actor);

        // 이름 끝 글자에 맞는 「이 · 가」 — 한글은 받침으로, 숫자는 읽는 소리로(0 영 · 1 일 · 3 삼 · 6 육 · 7 칠 · 8 팔 = 받침). 그 밖(영문 등)은 「이(가)」.
        private static string SubjectParticle(string name)
        {
            if (string.IsNullOrEmpty(name)) return "이(가)";

            char last = name[name.Length - 1];
            if (last >= '가' && last <= '힣') return (last - '가') % 28 != 0 ? "이" : "가";
            if (last >= '0' && last <= '9') return "013678".IndexOf(last) >= 0 ? "이" : "가";
            return "이(가)";
        }
    }
}
