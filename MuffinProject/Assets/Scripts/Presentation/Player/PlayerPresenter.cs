using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Muffin.Game;

namespace Muffin.Presentation
{

    public class PlayerPresenter : MonoBehaviourPunCallbacks
    {
        //[SerializeField] private Playerview view;

        public PlayerModel Model { get; private set; }

        public void Init(int actorNumber, float maxHP)
        {
            Model = new PlayerModel(actorNumber, maxHP);
            Debug.Log("현재 체력 : " + Model.CurrentHP);

            ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
            hashtable[PropKey.HP] = maxHP;
            PhotonNetwork.CurrentRoom.GetPlayer(actorNumber).SetCustomProperties(hashtable);
        }

        //private void Start()
        //{
        //    Model = new PlayerModel(PhotonNetwork.LocalPlayer.ActorNumber,100);
        //}

        //private void OnDestroy()
        //{
        //    if(Model != null)
        //    {
            
        //    }
        //}

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (Model == null) return;

            if(targetPlayer.ActorNumber == Model.ActorNumber)
            {
                if (changedProps.ContainsKey(PropKey.HP))
                {
                    float newHP = (float)changedProps[PropKey.HP];
                    Model.SetHP(newHP);
                    Debug.Log(Model.ActorNumber + "의 현재 체력 : " + Model.CurrentHP);
                }
                if (changedProps.ContainsKey(PropKey.HandCount))
                {
                    int newHandCount = (int)changedProps[PropKey.HandCount];
                    Model.SetHandCount(newHandCount);
                    Debug.Log("드로우 연동 확인");
                }
            }
        }


    }
}
