using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Chapchu.Game;
using Chapchu.Game.Cards;
using Chapchu.Core;

namespace Chapchu.Presentation
{

    public class CardPresenter : MonoBehaviour
    {
        private CardModel cardModel = new CardModel();
        public CardView cardView;

        /// <summary>이 카드가 속한 손패. 손패 밖 카드(체인 표시용 등)는 null.</summary>
        public PlayerHandPresenter Hand { get; private set; }

        private void Awake()
        {

        }

        public void DownIndex()
        {
            cardModel.cardIndex = cardModel.cardIndex - 1;
        }

        public void Setup(CardData data, int index = -1, PlayerHandPresenter hand = null)
        {
            if (data == null)
            {
                Debug.LogError("null CardData in CardPresenter");
                return;
            }
            cardView.Setup(data);
            cardModel.Setup(data,index);
            Hand = hand;
        }

        /// <summary>
        /// 드롭 영역에 놓였을 때 CardView 가 호출. 실제 처리는 손패가 직렬화한다 (PlayerHandPresenter.PlayCardAsync).
        /// </summary>
        public void OnCardDropped()
        {

            if (Hand == null)
            {
                Debug.LogError("[CardPresenter] 손패에 속하지 않은 카드를 드롭했다.");
                cardView.ReturnToOrigin();
                return;
            }

            _ = Hand.PlayCardAsync(this); // 예외는 PlayCardAsync 안에서 로그로 처리
        }

        /// <summary>대상 선택 → 사용 요청. 취소 · 타임아웃이면 손패로 되돌린다.</summary>
        public async Task PlayAsync()
        {
            List<int> targets = await SelectPlayer();

            if(targets.Count == 0)
            {
                cardView.ReturnToOrigin();
                return;
            }

            // 판정은 서버(RequestPlayCard)가 한다. 아직 연결 전이라 손패로 되돌린다 — 옛 CardPlayManager 경로는 지웠다.
            cardView.ReturnToOrigin();
        }

        public async Task<List<int>> SelectPlayer()
        {
            List<int> ActorNumbers = new List<int>();
        
            switch (cardModel.cardData.targetType)
            {
                case TargetType.SingleEnemy:
                    int pNum = 0;
                    pNum = await CardTargetPlayerSelector();
                    if (pNum != 0)
                    {
                        ActorNumbers.Add(pNum);
                    }
                    break;
                case TargetType.TwoEnemy:
                    int p1Num = 0;
                    p1Num = await CardTargetPlayerSelector();
                    if (p1Num != 0)
                    {
                        ActorNumbers.Add(p1Num);
                    }
                    int p2Num = 0;
                    p2Num = await CardTargetPlayerSelector();
                    if (p2Num != 0)
                    {
                        ActorNumbers.Add(p2Num);
                    }
                    break;
                case TargetType.AllEnemies:
                    foreach (var player in PhotonNetwork.PlayerList)
                    {
                        if (player.IsLocal) continue; // 나 자신 제외
                        ActorNumbers.Add(player.ActorNumber);
                    }
                    break;
                case TargetType.AllPlayers:
                    foreach (var player in PhotonNetwork.PlayerList)
                    {
                        ActorNumbers.Add(player.ActorNumber);
                    }
                    break;
                case TargetType.Me:
                    ActorNumbers.Add(PhotonNetwork.LocalPlayer.ActorNumber);
                    break;
                case TargetType.None:
                    break;

            }
            return ActorNumbers;
        }

        public async Task<int> CardTargetPlayerSelector()
        {
            int targetNumber = 0;
            targetNumber = await TargetSelectionManager.Instance.SelectPlayer(5.0f);
            if (targetNumber != 0)
            {
                return targetNumber;
            }
            else
            {
                return 0;
            }
        }
    }
}
