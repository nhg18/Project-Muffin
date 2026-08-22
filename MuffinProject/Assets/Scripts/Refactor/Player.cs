using Network;
using Photon.Pun;
using UnityEngine;

namespace Refactor
{
    public class Player
    {
        public int ActorNumber { get; private set; }
        public string Nickname { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public PlayerHand PlayerHand { get; private set; }

        public Player(int actorNumber, string nickname, int maxHp)
        {
            ActorNumber = actorNumber;
            Nickname = nickname;
            MaxHp = maxHp;
            Hp = maxHp;
        }
        
        public void TakeDamage(int damage)
        {
            Hp = Mathf.Clamp(Hp - damage, 0, MaxHp);
        }
    }
}
