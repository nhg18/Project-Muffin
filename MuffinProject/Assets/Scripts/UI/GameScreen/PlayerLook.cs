using System.Collections.Generic;
using System.Linq;
using Chapchu.Network;
using Chapchu.UI.Lobby;
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

        // 좌석이 없을 때(방 밖 · 나간 사람)의 잉크 회색 — LobbyStyle.Ink2 와 같은 값을 쓴다
        private static readonly Color UnseatedColor = LobbyStyle.Ink2;

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
        public static string NameOf(Player player) => NameOf(player.NickName, player.ActorNumber);

        /// <summary>닉네임 원본을 걸러 표시 이름으로, 비면 P{actorNumber} — 방에 없는 사람(결과 화면의 나간 사람)의 이름도 같은 규칙으로 만든다.</summary>
        public static string NameOf(string nickname, int actorNumber)
        {
            string name = NicknameValidator.ForDisplay(nickname);
            return name.Length == 0 ? string.Format(NicknameFallbackFormat, actorNumber) : name;
        }

        /// <summary>
        /// 방 안 플레이어(봇 포함)의 actorNumber 를 좌석 순서로 — 나(좌석 0) 먼저, 상대는 actorNumber 오름차순, 봇은 사람 뒤에 봇 번호 순
        /// (02-player.md 3절 · 18-bot.md 8-3). 상대 좌석 · 턴 순서 띠가 이 순서로 놓는다. 방 밖이면 빈 목록.
        /// </summary>
        public static List<int> OrderedActors()
        {
            var ordered = new List<int>();
            if (!PhotonNetwork.InRoom) return ordered;

            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            ordered.Add(me);
            ordered.AddRange(PhotonNetwork.PlayerList.Select(player => player.ActorNumber).Where(actor => actor != me).OrderBy(actor => actor));
            ordered.AddRange(BotRoster.Actors);
            return ordered;
        }

        /// <summary>플레이어의 목걸이 색. 나면 MeColor, 상대면 앉은 좌석 색. 좌석이 없으면(방 밖 · 나간 사람) 잉크 회색.</summary>
        public static Color ColorOf(int actorNumber) => LookOf(actorNumber, MeColor, SeatColors);

        /// <summary>플레이어의 흰 바탕 위 글자색.</summary>
        public static Color InkOf(int actorNumber) => LookOf(actorNumber, MeInk, SeatInks);

        /// <summary>
        /// 게임을 시작할 때의 명단으로 본 목걸이 색 — 결과 화면이 쓴다. 누가 나가도 게임에서 보던 색 그대로다
        /// (ColorOf 는 지금 방에 있는 사람으로 좌석을 세어, 나간 사람은 회색이 되고 남은 사람의 색이 밀린다).
        /// </summary>
        public static Color ColorOf(int actorNumber, IEnumerable<int> startActors)
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            var ordered = new List<int> { me };
            ordered.AddRange(startActors.Where(actor => actor != me).OrderBy(actor => actor)); // 봇 식별자는 사람보다 크다 — 사람 뒤에 봇 번호 순
            return LookOf(actorNumber, ordered, MeColor, SeatColors);
        }

        /// <summary>actorNumber 의 표시 이름. 봇이면 「봇 N」 (18-bot.md 4절), 방에 없으면 P{actorNumber}.</summary>
        public static string NameOf(int actorNumber)
        {
            if (BotRoster.IsBot(actorNumber))
                return BotRoster.NicknameOf(actorNumber);

            Player player = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber) : null;
            return player != null ? NameOf(player) : string.Format(NicknameFallbackFormat, actorNumber);
        }

        private static Color LookOf(int actorNumber, Color me, Color[] seats) => LookOf(actorNumber, OrderedActors(), me, seats);

        private static Color LookOf(int actorNumber, List<int> ordered, Color me, Color[] seats)
        {
            int index = ordered.IndexOf(actorNumber);
            if (index == 0) return me;
            if (index < 0) return UnseatedColor;

            int[] seatIndices = OpponentSeatsPresenter.GetOpponentSeatIndices(ordered.Count - 1);
            int seatIndex = index - 1 < seatIndices.Length ? seatIndices[index - 1] : index;
            return seats[Mathf.Clamp(seatIndex - 1, 0, seats.Length - 1)];
        }
    }
}
