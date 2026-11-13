using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내가 고르는 효과의 판 (119 목표 조준 — 프로토타입 v2 G.choose). 고르기가 시작되면 **바로** 연다 (입력이라 연출 큐를 기다리지 않는다 — 5초 안에 골라야 한다).
    /// 고르면 방장에게 요청만 보낸다 — 시간 · 고르는 사람 판정은 방장 (09-network.md 2절). 끝 알림이 오거나 내가 고르면 닫는다.
    /// 남의 고르기는 GameScreenPresenter 가 반응 타이머 자리에 「{이름} 고르는 중」 으로 보인다.
    /// </summary>
    [RequireComponent(typeof(ChoicePopupView))]
    public class ChoicePopupPresenter : MonoBehaviour
    {
        private const string AimTitle = "목표 조준!";
        private const string AimDescFormat = "{0}: 둘 중 하나를 고르세요. {1}초 안에 안 고르면 무작위";
        private const string DiscardFormat = "손패 전부 버리기 ({0}장)";
        private const string DamageFormat = "{0} 피해 받기";

        [SerializeField] private PunGameServer server;

        private ChoicePopupView _view;
        private double _deadline;
        private bool _open;

        private void Awake()
        {
            _view = GetComponent<ChoicePopupView>();
            if (server == null)
                Debug.LogError($"[{nameof(ChoicePopupPresenter)}] server 가 비어 있습니다.", this);
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
        }

        private void Update()
        {
            if (_open)
                _view.SetSeconds(Mathf.Max(0, Mathf.CeilToInt((float)(_deadline - PhotonNetwork.Time))));
        }

        private void HandleChoiceStarted(int actor, int user, int cardId, int handCount, int damage, double startsAt, double deadline)
        {
            if (actor != PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            _deadline = deadline;
            _open = true;
            int seconds = Mathf.RoundToInt((float)(deadline - startsAt));
            _view.Show(AimTitle, string.Format(AimDescFormat, PlayerLook.NameOf(user), seconds), string.Format(DiscardFormat, handCount), string.Format(DamageFormat, damage));
            Update();
        }

        private void HandleChoiceMade(int actor, int option, bool timedOut)
        {
            if (actor == PhotonNetwork.LocalPlayer.ActorNumber)
                Close();
        }

        private void HandlePicked(int option)
        {
            if (!_open)
                return;
            Close();
            server.RequestChoose(option);
        }

        private void Close()
        {
            _open = false;
            _view.Hide();
        }
    }
}
