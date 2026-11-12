using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Chapchu.UI.Title;
using Photon.Realtime;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 플레이어를 구분하는 목걸이 색 · 표시 이름 (10-ui.md 2-1 「색 · 글꼴」 HUD v2). 턴 순서 띠 · 이름표 · 내 패널 · 기록이 같은 값을 쓴다.
    /// 색은 좌석 인덱스로 정해진다 — 나 · 좌석 1 · 2 · 3 (02-player.md 3절의 좌석 계산 = OpponentSeatsPresenter.GetOpponentSeatIndices).
    /// 판정과 무관한 표시 전용 값이다.
    /// </summary>
    public static class PlayerLook
    {
        private const string NicknameFallbackFormat = "P{0}"; // 닉네임이 비었을 때 — 로그의 P{actorNumber} 와 같은 꼴

        /// <summary>내 목걸이 색 (시안 `P.me.col`).</summary>
        public static readonly Color MeColor = new Color32(0x2f, 0x7c, 0xf6, 0xFF);
        /// <summary>흰 바탕 위 글자용 내 색 (시안 `P.me.ink`).</summary>
        public static readonly Color MeInk = new Color32(0x1f, 0x5f, 0xc9, 0xFF);

        /// <summary>좌석 1 · 2 · 3 의 목걸이 색 (시안 `P.s1~s3.col`).</summary>
        public static readonly Color[] SeatColors =
        {
            new Color32(0xb5, 0x70, 0x2f, 0xFF),
            new Color32(0x9a, 0x6b, 0xd8, 0xFF),
            new Color32(0x17, 0xa5, 0x8c, 0xFF),
        };

        /// <summary>좌석 1 · 2 · 3 의 흰 바탕 위 글자색 (시안 `P.s1~s3.ink`).</summary>
        public static readonly Color[] SeatInks =
        {
            new Color32(0x8a, 0x52, 0x25, 0xFF),
            new Color32(0x74, 0x49, 0xb8, 0xFF),
            new Color32(0x0e, 0x7d, 0x6a, 0xFF),
        };

        /// <summary>닉네임(서식 태그 · 제어 문자를 거른 것, NicknameValidator.ForDisplay), 비면 P{actorNumber}.</summary>
        public static string NameOf(Player player)
        {
            string name = NicknameValidator.ForDisplay(player.NickName);
            return name.Length == 0 ? string.Format(NicknameFallbackFormat, player.ActorNumber) : name;
        }

        /// <summary>
        /// 방 안 플레이어를 좌석 순서로 — 나(좌석 0) 먼저, 상대는 actorNumber 오름차순 (02-player.md 3절). 턴 순서 띠가 이 순서로 칩을 놓는다.
        /// 방 밖이면 빈 목록.
        /// </summary>
        public static List<Player> OrderedPlayers()
        {
            var ordered = new List<Player>();
            if (!PhotonNetwork.InRoom) return ordered;

            Player me = PhotonNetwork.LocalPlayer;
            ordered.Add(me);
            ordered.AddRange(PhotonNetwork.PlayerList.Where(player => player.ActorNumber != me.ActorNumber).OrderBy(player => player.ActorNumber));
            return ordered;
        }

        /// <summary>플레이어의 목걸이 색. 나면 MeColor, 상대면 앉은 좌석 색. 좌석이 없으면(방 밖 · 나간 사람) 잉크 회색.</summary>
        public static Color ColorOf(int actorNumber) => LookOf(actorNumber, MeColor, SeatColors);

        /// <summary>플레이어의 흰 바탕 위 글자색.</summary>
        public static Color InkOf(int actorNumber) => LookOf(actorNumber, MeInk, SeatInks);

        /// <summary>actorNumber 의 표시 이름. 방에 없으면 P{actorNumber}.</summary>
        public static string NameOf(int actorNumber)
        {
            Player player = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber) : null;
            return player != null ? NameOf(player) : string.Format(NicknameFallbackFormat, actorNumber);
        }

        private static Color LookOf(int actorNumber, Color me, Color[] seats)
        {
            List<Player> ordered = OrderedPlayers();
            int index = ordered.FindIndex(player => player.ActorNumber == actorNumber);
            if (index == 0) return me;
            if (index < 0) return new Color32(0x6e, 0x64, 0x5c, 0xFF);

            int[] seatIndices = OpponentSeatsPresenter.GetOpponentSeatIndices(ordered.Count - 1);
            int seatIndex = index - 1 < seatIndices.Length ? seatIndices[index - 1] : index;
            return seats[Mathf.Clamp(seatIndex - 1, 0, seats.Length - 1)];
        }
    }
}
