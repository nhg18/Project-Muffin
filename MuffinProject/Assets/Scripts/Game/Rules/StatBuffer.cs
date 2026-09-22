using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Muffin.Game
{

    public class StatBuffer
    {
        private static readonly Dictionary<(int actor, string key), float> working = new();

        public static float Get(Player player, string key)
        {
            if (working.TryGetValue((player.ActorNumber, key), out float v))
                return v;

            if (player.CustomProperties.TryGetValue(key, out object o) && o != null)
            {
                if (o is float f) return f;

                Debug.LogWarning($"[StatBuffer] {player.NickName}의 '{key}' 타입이 float가 아님: {o.GetType()} (값: {o})");
                return System.Convert.ToSingle(o);
            }

            Debug.LogWarning($"[StatBuffer] {player.NickName}에게 '{key}' 프로퍼티가 없음");
            return 0f;
        }

        public static void Set(Player player, string key, float value)
        {
            working[(player.ActorNumber, key)] = value;
        }

        public static void Commit()
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient || working.Count == 0) return;
                var perPlayer = new Dictionary<int, Hashtable>();
                foreach(var kv in working)
                {
                    if (!perPlayer.TryGetValue(kv.Key.actor, out var props))
                        perPlayer[kv.Key.actor] = props = new Hashtable();
                    props[kv.Key.key] = kv.Value;
                }

                foreach (var kv in perPlayer)
                    PhotonNetwork.CurrentRoom.GetPlayer(kv.Key)?.SetCustomProperties(kv.Value);
            }
            finally
            {
                working.Clear();
            }
        }
    }
}
