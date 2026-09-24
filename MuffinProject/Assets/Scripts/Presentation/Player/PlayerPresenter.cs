using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Core;
using Chapchu.Game;

namespace Chapchu.Presentation
{

    public class PlayerPresenter : MonoBehaviourPunCallbacks
    {
        //[SerializeField] private Playerview view;

        public PlayerModel Model { get; private set; }

        public void Init(int actorNumber, int maxHp)
        {
            Model = new PlayerModel(actorNumber, maxHp);
            Debug.Log("현재 체력 : " + Model.CurrentHp);

            ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
            hashtable[PlayerProps.Hp] = maxHp;
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
                if (changedProps.ContainsKey(PlayerProps.Hp))
                {
                    int newHp = (int)changedProps[PlayerProps.Hp];
                    Model.SetHp(newHp);
                    Debug.Log(Model.ActorNumber + "의 현재 체력 : " + Model.CurrentHp);
                }
                if (changedProps.ContainsKey(PlayerProps.HandCount))
                {
                    int newHandCount = (int)changedProps[PlayerProps.HandCount];
                    Model.SetHandCount(newHandCount);
                    Debug.Log("드로우 연동 확인");
                }
            }
        }


    }
}
