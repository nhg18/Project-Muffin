using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{

    public class PlayerHandPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHandView handView;

        [SerializeField] private CardDatabase cardDatabase;

        public PlayerHand playerHand = new PlayerHand();

        private bool isPropertyUpdatePending = false;

        /// <summary>
        /// 드롭한 카드가 대상 선택 → 사용 요청까지 처리 중인가.
        /// 한 손패에서 한 번에 한 장만 처리한다 (10-ui.md §6). 처리 중에는 CardView · ClickManager 가 입력을 무시한다.
        /// 마스터의 반응 시간(5초)은 포함하지 않는다. 그 동안 카운터 카드를 내야 하기 때문.
        /// </summary>
        public bool IsCardPlayInProgress { get; private set; }

        private void OnEnable()
        {
            GameEvents.OnDrawn += StartDrawEvent;
            GameEvents.OnCardPlayed += DiscardCard;
        }
        private void OnDisable()
        {
            GameEvents.OnDrawn -= StartDrawEvent;
            GameEvents.OnCardPlayed -= DiscardCard;
        }

        private void StartDrawEvent(int actorNumber, int cardid)
        {

            if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

            //Debug.Log("card : " + cardid);

            CardData data = cardDatabase.GetCard(cardid);

            CardPresenter cp = handView.DrawCard(data);
            cp.Setup(data, playerHand.GetHandCount(), this);
            playerHand.Add(new Card(data.id));

            if (!isPropertyUpdatePending)
            {
                isPropertyUpdatePending = true;
                StartCoroutine(UpdatePropertyAtEndOfFrame());
            }
        }

        private void DiscardCard(int cardID, int index)
        {
            playerHand.DiscardCard(index);
            handView.DiscardCard(index);
            if (!isPropertyUpdatePending)
            {
                isPropertyUpdatePending = true;
                StartCoroutine(UpdatePropertyAtEndOfFrame());
            }
        }

        private IEnumerator UpdatePropertyAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame(); // 프레임 끝까지 대기

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable
                {
                    [PlayerProps.HandCount] = playerHand.GetHandCount()
                }
            );

            isPropertyUpdatePending = false;
        }
    

        public bool IsHandMode()
        {
            return playerHand.isHandMode;
        }
        public void SetHandMode(bool setter)
        {
            playerHand.isHandMode = setter;
            GameEvents.RaiseHandModeChanged(setter);
        }

        /// <summary>
        /// 드롭된 카드의 처리(대상 선택 → 사용 요청)를 손패 단위로 직렬화한다.
        /// 이미 처리 중이면 두 번째 카드는 손패로 되돌린다.
        /// 카드는 처리 끝에 Destroy 되므로 잠금 해제는 카드가 아니라 손패가 책임진다.
        /// </summary>
        public async Task PlayCardAsync(CardPresenter card)
        {
            if (IsCardPlayInProgress)
            {
                Debug.LogWarning("[PlayerHandPresenter] 이미 카드 처리 중. 두 번째 카드는 손패로 되돌린다.");
                card.cardView.ReturnToOrigin();
                return;
            }

            IsCardPlayInProgress = true;
            handView.CancelAllInteractions(card.cardView);

            try
            {
                await card.PlayAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsCardPlayInProgress = false;
            }
        }

    }
}
