using Photon.Pun;
using UnityEngine;
using Chapchu.Core;
using Chapchu.Game;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 상대 손패의 뒷면 개수만 보여준다. 카드 내용은 절대 모른다 — 알 필요도 없다.
    /// "몇 장인지"는 공개 정보(PlayerProps.HandCount)이므로 GameEvents.OnHandCountChanged 로만 받는다.
    /// (OnMyDrawn 은 카드 내용을 담은 비공개 이벤트라 카드 주인에게만 간다. 여기선 쓰지 않는다.)
    /// </summary>
    public class OtherPlayerHandPresenter : MonoBehaviour
    {
        public int OtherPlayerNumber = 0;
        [SerializeField] private OtherPlayerHandView handView;

        private int _handCount = 0;

        private void OnEnable()
        {
            GameEvents.OnHandCountChanged += HandleHandCountChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnHandCountChanged -= HandleHandCountChanged;
        }

        // 늦게 켜졌을 때(관전 시작 시점)를 대비해 현재 장수로 한 번 맞춘다.
        private void Start()
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(OtherPlayerNumber);
            int current = player != null && player.CustomProperties.TryGetValue(PlayerProps.HandCount, out object value) ? (int)value : 0;
            Reconcile(current);
        }

        private void HandleHandCountChanged(int actorNumber, int handCount)
        {
            if (actorNumber != OtherPlayerNumber) return;
            Reconcile(handCount);
        }

        private void Reconcile(int targetCount)
        {
            while (_handCount < targetCount)
            {
                handView.DrawCard();
                _handCount++;
            }

            while (_handCount > targetCount)
            {
                handView.RemoveCard();
                _handCount--;
            }
        }
    }
}
