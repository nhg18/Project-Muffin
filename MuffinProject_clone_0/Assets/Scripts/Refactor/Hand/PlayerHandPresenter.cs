using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHandPresenter : MonoBehaviour
{
    [Header("Hands Setting")]
    private int HandCount=0;

    [SerializeField] private PlayerHandView handView;

    [SerializeField] private CardDatabase cardDatabase;

    PlayerHand playerHand = new PlayerHand();

    private bool isPropertyUpdatePending = false;

    private void OnEnable()
    {
        GameEvents.OnDrawn += StartDrawEvent;
    }
    private void OnDisable()
    {
        GameEvents.OnDrawn -= StartDrawEvent;
    }

    private void StartDrawEvent(int actorNumber, int cardid)
    {

        if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

        Debug.Log("card : " + cardid);

        CardData data = cardDatabase.GetCard(cardid);

        handView.draw_A_Card(data);

        HandCount++;
        playerHand.Add(new Card(data.id));

        if (!isPropertyUpdatePending)
        {
            isPropertyUpdatePending = true;
            StartCoroutine(UpdatePropertyAtEndOfFrame());
        }
    }

    //private void RefreshMyHandCount()
    //{
    //    PhotonNetwork.LocalPlayer.SetCustomProperties(
    //        new ExitGames.Client.Photon.Hashtable
    //        {
    //            ["HandCount"] = HandCount,
    //        }
    //    );
    //}
    private IEnumerator UpdatePropertyAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame(); // 프레임 끝까지 대기

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                ["HandCount"] = HandCount
            }
        );

        isPropertyUpdatePending = false;
    }
    

    public bool getHandMod()
    {
        return playerHand.isHandMod;
    }
    public void setHandMod(bool setter)
    {
        playerHand.isHandMod = setter;
        GameEvents.RaiseHandModeChanged(setter);
    }

}
