using System;

namespace Muffin.Game
{

    public class PlayerModel
    {
        public int ActorNumber { get; }
        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }
        public int CurrentHandCount { get; private set; }

        public PlayerModel(int actorNumber, int maxHp)
        {
            ActorNumber = actorNumber;
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void SetHp(int newHp)
        {
            CurrentHp = Math.Clamp(newHp, 0, MaxHp);
            GameEvents.RaiseHpChanged(ActorNumber, CurrentHp);
        }

        public void SetHandCount(int newHandCount)
        {
            CurrentHandCount = newHandCount;
            GameEvents.RaiseHandCountChanged(ActorNumber, CurrentHandCount);
        }
    }
}
