using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Muffin.Core;
using Muffin.Game;
using Muffin.Game.Cards;

namespace Muffin.Presentation
{

    public class PlayerHandPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHandView handView;

        [SerializeField] private CardDatabase cardDatabase;

        public PlayerHand playerHand = new PlayerHand();

        private bool isPropertyUpdatePending = false;

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
            cp.Setup(data, playerHand.GetHandCount());//Count is bigger than index
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

    }
}
