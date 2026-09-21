using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Muffin.Game.Cards
{


    public abstract class CardEffect : ScriptableObject
    {
        public abstract void Execute(Player caster, Player target);
    }
}
