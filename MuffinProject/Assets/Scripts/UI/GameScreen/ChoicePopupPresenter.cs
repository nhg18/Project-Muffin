using System.Collections;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 효과 고르기 판 (1019 목표 조준, PM 2026-11-27). 쓴 카드(체인의 카드를 판 왼쪽으로) · 고를 카드 두 장 · 남은 시간 막대.
    /// 내가 고르는 사람이면 고르기가 시작되자마자 **바로** 연다 (입력이라 연출 큐를 기다리지 않는다 — 5초 안에 골라야 한다).
    /// 고르면 방장에게 요청만 보낸다 — 시간 · 고르는 사람 판정은 방장 (09-network.md 2절).
    /// 남이 고르는 중이면 GameScreenPresenter 가 연출 큐 순서대로 ShowWatching · PickRoutine 을 부른다 — 같은 카드에 고르는 사람의 숫자.
    /// </summary>
    [RequireComponent(typeof(ChoicePopupView))]
    public class ChoicePopupPresenter : MonoBehaviour
    {
        private const string ChooseHintFormat = "둘 중 하나를 눌러요 · {0}초 지나면 무작위";
        private const string WatchHintFormat = "{0}{1} 고르는 중";
        private const string PickedHintFormat = "{0} — {1}!";
        private const string TagFormat = "{0} → {1}";
        private const string MeName = "나";

        [SerializeField] private PunGameServer server;
        [Tooltip("쓴 카드 — 판이 열리면 체인의 카드를 판 왼쪽 자리로 옮긴다.")]
        [SerializeField] private ChainCardsPresenter chainCards;

        private ChoicePopupView _view;
        private bool _open;
        private bool _mine;       // 내가 고르는 판 — 고르기 끝 알림을 바로 받는다
        private bool _picking;    // 고른 연출 중 — 남은 시간을 더 줄이지 않는다
        private double _startsAt;
        private double _deadline;

        private void Awake()
        {
            _view = GetComponent<ChoicePopupView>();
            if (server == null || chainCards == null)
                Debug.LogError($"[{nameof(ChoicePopupPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            _view.Picked += HandlePicked;
            GameEvents.OnChoiceStarted += HandleChoiceStarted;
            GameEvents.OnChoiceMade += HandleChoiceMade;
        }

        private void OnDisable()
        {
            _view.Picked -= HandlePicked;
            GameEvents.OnChoiceStarted -= HandleChoiceStarted;
            GameEvents.OnChoiceMade -= HandleChoiceMade;
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
            _view.SetFraction(total > 0 ? (float)((_deadline - PhotonNetwork.Time) / total) : 0f);
        }

        // 내가 고르는 사람 — 바로 연다. HP 는 지금 방 속성 값 (피해가 들어가기 전).
        private void HandleChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline)
        {
            if (!IsMe(actor))
                return;

            int hp = BotRoster.TryGetPublic(actor, PlayerProps.Hp, out object value) && value is int hpValue ? hpValue : GameServer.MaxHp;
            int seconds = Mathf.RoundToInt((float)(deadline - startsAt));
            Open(actor, user, handCount, hp, damage, startsAt, deadline, string.Format(ChooseHintFormat, seconds), true);
        }

        /// <summary>남이 고르는 중 — 같은 판을 누를 수 없게 연다. hp = 화면에 재생한 고르는 사람의 HP. 연출 큐가 부른다.</summary>
        public void ShowWatching(int actor, int user, int handCount, int hp, int damage, double startsAt, double deadline)
        {
            string name = PlayerLook.NameOf(actor);
            Open(actor, user, handCount, hp, damage, startsAt, deadline, string.Format(WatchHintFormat, name, SubjectParticle(name)), false);
        }

        /// <summary>남이 골랐다 — 고른 카드를 튀어나오게 하고 판을 닫는다. 연출 큐가 기다릴 코루틴. 판이 닫혀 있으면 바로 끝난다.</summary>
        public IEnumerator PickRoutine(int actor, int option)
        {
            if (!_open || _mine)
                yield break;

            yield return Pick(actor, option);
        }

        private void Open(int actor, int user, int handCount, int hp, int damage, double startsAt, double deadline, string hint, bool interactive)
        {
            StopAllCoroutines();
            _open = true;
            _mine = interactive;
            _picking = false;
            _startsAt = startsAt;
            _deadline = deadline;
            _view.Show(hint, string.Format(TagFormat, ShortName(user), ShortName(actor)), handCount, hp, damage, interactive);
            chainCards.Focus(_view.UsedCardCenter, _view.UsedCardHeight);
            Update();
        }

        // 시간이 지나 방장이 무작위로 골랐거나, 내가 고른 것이 확정됐다 — 아직 고른 연출 전이면 그 카드로.
        private void HandleChoiceMade(int actor, int option, bool timedOut)
        {
            if (!_open || !_mine || !IsMe(actor) || _picking)
                return;

            StartCoroutine(Pick(actor, option));
        }

        private void HandlePicked(int option)
        {
            if (!_open || !_mine || _picking)
                return;

            server.RequestChoose(option);
            StartCoroutine(Pick(PhotonNetwork.LocalPlayer.ActorNumber, option));
        }

        private IEnumerator Pick(int actor, int option)
        {
            _picking = true;
            _view.SetHint(string.Format(PickedHintFormat, ShortName(actor), _view.OptionName(option)));
            yield return _view.PickRoutine(option);
            _open = false;
            _picking = false;
            chainCards.Unfocus();
        }

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
