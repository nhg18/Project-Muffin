using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 처리 카드 아래 알약 (10-ui.md 5절 · 04-card.md 8절). 반응 시간의 남은 초는 보이지 않는다 — 마감은 방장만 안다.
    /// 반응 시간에 내가 낼 수 있는 카운터가 있으면 「스킵」 버튼이 된다.
    /// 표시 전용이고 판정은 방장이 한다 (09-network.md 8절). 언제 보이고 숨는지는 GameScreenPresenter 가 부른다.
    /// </summary>
    [RequireComponent(typeof(ReactionTimerView))]
    public class ReactionTimerPresenter : MonoBehaviour
    {
        [Tooltip("남은 초가 이 값 이하면 빨강 (시안 2초).")]
        [SerializeField] private float lowSeconds = 2f;

        /// <summary>스킵 버튼을 눌렀다.</summary>
        public event System.Action OnSkipClicked;

        private ReactionTimerView _view;
        private bool _showing;
        private bool _skipShown;
        private double _opensAt;  // 게임 시각(GameClock, 초)
        private double _deadline;

        /// <summary>반응 시간 알약 · 스킵이 떠 있다 — 카드를 처리하는 중이다.</summary>
        public bool IsOpen => _showing || _skipShown;

        /// <summary>지금 내가 낼 수 있는 카운터가 있다 (스킵 버튼이 떠 있다).</summary>
        public bool CountersOffered => _skipShown;

        private void Awake()
        {
            _view = GetComponent<ReactionTimerView>();
        }

        private void OnEnable() => _view.SkipClicked += HandleSkipClicked;

        private void OnDisable() => _view.SkipClicked -= HandleSkipClicked;

        private void HandleSkipClicked() => OnSkipClicked?.Invoke();

        /// <summary>반응 시간이 열려 있고 내가 낼 수 있는 카운터가 있다 · 없다 — 「스킵」 버튼을 켜고 끈다.</summary>
        public void SetSkipVisible(bool visible)
        {
            if (visible)
            {
                _showing = false;
                _skipShown = true;
                _view.ShowSkip();
            }
            else if (_skipShown)
            {
                _skipShown = false;
                _view.Hide();
            }
        }

        private void Update()
        {
            if (!_showing) return;

            double now = Chapchu.Network.GameClock.Now;
            if (now < _opensAt)
            {
                _view.ShowSoon();
                return;
            }

            float remaining = (float)(_deadline - now);
            if (remaining <= 0f)
            {
                _view.ShowResolving();
                return;
            }

            float total = (float)(_deadline - _opensAt);
            _view.ShowRemaining(Mathf.CeilToInt(remaining), total > 0f ? remaining / total : 0f, remaining <= lowSeconds, null);
        }

        /// <summary>내 손패가 올라왔다 · 내려갔다 — 올라와 있으면 손패 바로 위로 옮기고 카드 이름을 붙인다.</summary>
        public void SetHandRaised(bool raised) => _view.SetDocked(raised);

        /// <summary>체인 카드의 아래 끝 (캔버스 y, 화면 가운데가 0) — 평소 자리가 따라간다.</summary>
        public void SetCardsBottom(float canvasY) => _view.SetCardsBottom(canvasY);

        /// <summary>반응을 기다리는 카드(체인 맨 위)의 이름.</summary>
        public void SetCardName(string cardName) => _view.SetCardName(cardName);

        /// <summary>체인이 처리되기 시작했다 — 숨긴다.</summary>
        public void Hide()
        {
            _showing = false;
            _skipShown = false;
            _view.Hide();
        }
    }
}
