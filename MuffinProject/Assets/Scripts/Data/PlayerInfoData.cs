using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInfoData
{
    public string Nickname;
    public int    HP;
    public int    CardsCount;
    public bool   IsChapChu;   // 함정 카드 보유 여부
    /// <summary>
    /// Photon CustomProperties에서 직접 파싱합니다.
    /// </summary>
    public static PlayerInfoData FromPhoton(
        Photon.Realtime.Player player,
        ExitGames.Client.Photon.Hashtable props)
    {
        var data = new PlayerInfoData
        {
            Nickname   = player.NickName,
            HP         = props.TryGet("PlayerHP",    out int hp)    ? hp    : 0,
            CardsCount = props.TryGet("CardsCount",  out int cards) ? cards : 0,
            IsChapChu  = props.TryGet("isChapChu",   out bool trap) && trap,
        };
        return data;
    }
}


/// <summary>
/// Hashtable 확장 — 타입 안전한 TryGet 헬퍼
/// </summary>
public static class HashtableExtensions
{
    public static bool TryGet<T>(
        this ExitGames.Client.Photon.Hashtable table,
        string key,
        out T value)
    {
        if (table.ContainsKey(key) && table[key] is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }
}