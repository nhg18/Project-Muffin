using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 반응 타이머에 값을 넣는다 (10-ui.md 5절 · 04-card.md 8절). 방장이 보낸 반응 시작 · 마감 시각(서버 시각)으로 남은 초를 매 프레임 계산한다 —
    /// 표시 전용이고 판정은 방장이 한다 (09-network.md 8절). 언제 보이고 숨는지는 GameScreenPresenter 가 연출 큐에서 부른다:
    /// 카드가 체인에 오를 때마다 새 시각으로 다시 시작하고, 체인의 처리 결과가 오기 시작하면 숨긴다.
    /// 늦게 열린 화면은 진행 중인 반응 시간을 복구하지 않는다 — 방 속성에 없다 (09-network.md 6.1).
    /// </summary>
    [RequireComponent(typeof(ReactionTimerView))]
    public class ReactionTimerPresenter : MonoBehaviour
    {
        [Tooltip("남은 초가 이 값 이하면 빨강 (시안 2초).")]
        [SerializeField] private float lowSeconds = 2f;

        private ReactionTimerView _view;
        private bool _showing;
        private double _opensAt;  // 서버 시각(PhotonNetwork.Time, 초)
        private double _deadline;

        private void Awake()
        {
            _view = GetComponent<ReactionTimerView>();
        }

        private void Update()
        {
            if (!_showing) return;

            double now = PhotonNetwork.Time;
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
            _view.ShowRemaining(Mathf.CeilToInt(remaining), total > 0f ? remaining / total : 0f, remaining <= lowSeconds);
        }

        /// <summary>반응 시간이 정해졌다 (OnReactionStarted) — opensAt 전은 카드 연출 중.</summary>
        public void Show(double opensAt, double deadline)
        {
            _opensAt = opensAt;
            _deadline = deadline;
            _showing = true;
        }

        /// <summary>체인이 처리되기 시작했다 — 숨긴다.</summary>
        public void Hide()
        {
            _showing = false;
            _view.Hide();
        }
    }
}
