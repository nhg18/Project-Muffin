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
    /// 내가 고르는 효과의 판 (1019 목표 조준 — 프로토타입 v2 G.choose). 고르기가 시작되면 **바로** 연다 (입력이라 연출 큐를 기다리지 않는다 — 5초 안에 골라야 한다).
    /// 고르면 방장에게 요청만 보낸다 — 시간 · 고르는 사람 판정은 방장 (09-network.md 2절). 끝 알림이 오거나 내가 고르면 닫는다.
    /// 「손패에서 1장」 고르기 (2013 · 2021 의 대가)는 같은 판에 내 손패를 카드 칸으로 늘어놓고, 누른 카드의 장 번호를 보낸다 (10-ui.md 5절).
    /// 남의 고르기는 GameScreenPresenter 가 반응 타이머 자리에 「{이름} 고르는 중」 으로 보인다.
    /// </summary>
    [RequireComponent(typeof(ChoicePopupView))]
    public class ChoicePopupPresenter : MonoBehaviour
    {
        private const string AimTitle = "목표 조준!";
        private const string AimDescFormat = "{0}: 둘 중 하나를 골라 주세요. {1}초 안에 안 고르면 무작위로 정해요.";
        private const string DiscardFormat = "손패 전부 버리기 ({0}장)";
        private const string DamageFormat = "{0} 피해 받기";
        private const string CostTitleFormat = "{0} — 버릴 카드";
        private const string CostDescFormat = "버릴 카드 1장을 골라 주세요. {0}초 안에 안 고르면 무작위로 버려요.";

        [SerializeField] private PunGameServer server;
        [Tooltip("내 손패 — 「손패에서 1장」 고르기에 늘어놓을 카드.")]
        [SerializeField] private HandView hand;

        private ChoicePopupView _view;
        private double _deadline;
        private bool _open;

        // 버림 알림은 왔지만 손패 화면(연출 큐)에서는 아직 안 빠졌을 수 있는 내 카드 — 고르기 칸에서 뺀다. 다시 손패로 들어오면 지운다.
        private readonly HashSet<int> _discarded = new HashSet<int>();

        // 고른 뒤 방장의 답(끝 알림 · 거절)을 기다리는 중 — 거절되면 마감 전까지 같은 고르기를 다시 연다
        private bool _awaiting;
        private int _user;
        private int _cardId;
        private int _handCount;
        private int _damage;
        private double _startsAt;
        private int _kind;

        private void Awake()
        {
            _view = GetComponent<ChoicePopupView>();
            if (server == null || hand == null)
                Debug.LogError($"[{nameof(ChoicePopupPresenter)}] server · hand 중 비어 있는 것이 있습니다.", this);
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
        }

        private void Update()
        {
            if (_open)
                _view.SetSeconds(Mathf.Max(0, Mathf.CeilToInt((float)(_deadline - PhotonNetwork.Time))));
        }

        private void HandleChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline, int kind)
        {
            if (actor != PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            _user = user;
            _cardId = cardId;
            _handCount = handCount;
            _damage = damage;
            _startsAt = startsAt;
            _kind = kind;
            _awaiting = false;
            _deadline = deadline;
            _open = true;
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

                CardData cost = server.Deck.GetCard(cardId);
                _view.ShowCards(string.Format(CostTitleFormat, cost != null ? cost.cardName : string.Empty), string.Format(CostDescFormat, seconds), ids, cards);
                Update();
                return;
            }

            _view.Show(AimTitle, string.Format(AimDescFormat, PlayerLook.NameOf(user), seconds), string.Format(DiscardFormat, handCount), string.Format(DamageFormat, damage));
            Update();
        }

        private void HandleChoiceMade(int actor, int option, bool timedOut, int kind)
        {
            if (actor != PhotonNetwork.LocalPlayer.ActorNumber)
                return;
            _awaiting = false;
            Close();
        }

        // 아래 넷은 연출 큐(GameScreenPresenter)가 알림을 받은 자리에서 바로 부른다 — 큐 순서를 기다리지 않는다 (CODE_CONVENTION 9절).
        /// <summary>내 손패의 카드가 버려졌다.</summary>
        public void NoteDiscarded(int[] cardInstanceIds) => _discarded.UnionWith(cardInstanceIds);

        /// <summary>카드가 버림 더미를 떠났다 (드로우 · 버림 더미에서 가져감 · 손패로 반환).</summary>
        public void NoteLeftDiscard(int cardInstanceId) => _discarded.Remove(cardInstanceId);

        /// <summary>카드들이 버림 더미를 떠나 내 손패로 들어왔다.</summary>
        public void NoteLeftDiscard(int[] cardInstanceIds) => _discarded.ExceptWith(cardInstanceIds);

        /// <summary>버림 더미가 비었다 (덱 리필 · 대소동).</summary>
        public void ClearDiscarded() => _discarded.Clear();

        // 고르기 요청이 거절됐다 (없는 장 번호 등) — 고르기는 방장에서 그대로 열려 있으니 마감 전이면 다시 연다
        private void HandleMyRequestRejected(int code)
        {
            if (!_awaiting || code != RejectCode.NotChoosing)
                return;
            _awaiting = false;
            if (PhotonNetwork.Time < _deadline)
                HandleChoiceStarted(PhotonNetwork.LocalPlayer.ActorNumber, _user, _cardId, _handCount, _damage, _startsAt, _deadline, _kind);
        }

        private void HandlePicked(int option)
        {
            if (!_open)
                return;
            Close();
            _awaiting = true;
            server.RequestChoose(option, _deadline);
        }

        private void Close()
        {
            _open = false;
            _view.Hide();
        }
    }
}
