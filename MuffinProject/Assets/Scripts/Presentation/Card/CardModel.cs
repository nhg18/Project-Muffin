using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{

    public class CardModel
    {
        public CardData cardData;
        /// <summary>서버가 덱 생성 때 부여한 고유 번호 (09-network.md 10절). 사용 요청 · OnCardUsed 대조에 쓴다.</summary>
        public int cardInstanceId;
        public void Setup(CardData data, int instanceId)
        {
            cardData = data;
            cardInstanceId = instanceId;
        }
    }
}
