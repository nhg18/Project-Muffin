using UnityEngine;

namespace Chapchu.Game.Cards
{
    /// <summary>카드 효과 수치를 담는 에셋의 기반. 수치는 Card 로 옮겨지고, 실행은 서버(GameServer)가 한다.</summary>
    public abstract class CardEffect : ScriptableObject
    {
    }
}
