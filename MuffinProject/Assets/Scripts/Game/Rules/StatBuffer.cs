using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Game
{

    public class StatBuffer
    {
        private static readonly Dictionary<(int actor, string key), int> working = new();

        public static int Get(Player player, string key)
        {
            if (working.TryGetValue((player.ActorNumber, key), out int v))
                return v;

            if (player.CustomProperties.TryGetValue(key, out object o) && o != null)
            {
                if (o is int i) return i;

                Debug.LogWarning($"[StatBuffer] {player.NickName}의 '{key}' 타입이 int가 아님: {o.GetType()} (값: {o})");
                return System.Convert.ToInt32(o);
            }

            Debug.LogWarning($"[StatBuffer] {player.NickName}에게 '{key}' 프로퍼티가 없음");
            return 0;
        }

        public static void Set(Player player, string key, int value)
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
