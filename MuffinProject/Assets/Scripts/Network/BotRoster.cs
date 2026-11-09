using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// 방에 든 봇 목록 읽기 (18-bot.md 9절). 봇은 접속자가 아니라 방 속성(RoomProps.Bots)의 번호로만 있다.
    /// 봇 식별자 = 1000 + 번호 — 게임 규칙 · 이벤트의 actorNumber 자리에 사람과 같이 쓰인다. 추가 · 제거는 NetworkManager.
    /// </summary>
    public static class BotRoster
    {
        // 확정 (18-bot.md 4절 · 9절).
        public const int MaxBots = 3;
        public const int ActorBase = 1000;

        /// <summary>
        /// 지금 방의 봇 번호 (오름차순). 방 밖이면 빈 배열.
        /// 원본은 방장 메모리다 (09-network.md 2절 #2 · 3절) — 방장은 자기가 정한 목록(서버 확인 전 포함)을, 다른 클라는 방 속성 사본을 읽는다.
        /// </summary>
        public static int[] Numbers
        {
            get
            {
                Room room = PhotonNetwork.CurrentRoom;
                if (room == null)
                    return Array.Empty<int>();

                int[] numbers = (PhotonNetwork.IsMasterClient && NetworkManager.Instance != null ? NetworkManager.Instance.HostBots : null) ?? RoomNumbers;

                // 방 속성은 아무 클라나 쓸 수 있다 — 번호 1 ~ 3 · 중복 없이 · 사람 + 봇 ≤ 4 만 (18-bot.md 4절 · 08-room.md 2절). 넘치면 큰 번호를 뺀다
                return numbers.Where(n => n >= 1 && n <= MaxBots).Distinct().OrderBy(n => n).Take(NetworkManager.MaxPlayers - room.PlayerCount).ToArray();
            }
        }

        // 방 속성에 적힌 그대로 (오름차순) — 방장이 자기가 쓴 목록과 견줄 때 쓴다
        internal static int[] RoomNumbers
        {
            get
            {
                Room room = PhotonNetwork.CurrentRoom;
                if (room == null || !room.CustomProperties.TryGetValue(RoomProps.Bots, out object value) || !(value is int[] numbers))
                    return Array.Empty<int>();

                return numbers.OrderBy(n => n).ToArray();
            }
        }

        /// <summary>봇의 식별자 (actorNumber 자리). 턴 순서 · 좌석은 사람 뒤에 이 순서로 (02-player.md 3절).</summary>
        public static int[] Actors => Numbers.Select(ActorOf).ToArray();

        /// <summary>사람 + 봇. 시작 최소 인원 · 정원 계산은 봇을 포함한다 (08-room.md 2절).</summary>
        public static int MemberCount => (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0) + Numbers.Length;

        /// <summary>「혼자 하기」 로 만든 방 — 다시 열어도 랜덤 매칭에서 뺀다 (18-bot.md 10절).</summary>
        public static bool IsSoloRoom => PhotonNetwork.CurrentRoom != null
                                         && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.Solo, out object solo) && solo is bool b && b;

        public static int ActorOf(int number) => ActorBase + number;
        public static bool IsBot(int actorNumber) => actorNumber > ActorBase;

        /// <summary>「봇 N」 (18-bot.md 4절). 「(봇)」 표기는 화면이 붙인다.</summary>
        public static string NicknameOf(int actorNumber) => $"봇 {actorNumber - ActorBase}";

        /// <summary>봇의 공개 값이 들어 있는 방 속성 키 — 사람과 같은 키(PlayerProps) + #식별자 (09-network.md 3절).</summary>
        public static string PropKey(string playerPropKey, int actorNumber) => $"{playerPropKey}#{actorNumber}";

        /// <summary>사람이면 Player 속성, 봇이면 방 속성에서 공개 값을 읽는다 — 화면이 사람 · 봇을 같은 식으로 복구하게.</summary>
        public static bool TryGetPublic(int actorNumber, string playerPropKey, out object value)
        {
            value = null;
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
                return false;

            if (IsBot(actorNumber))
                return room.CustomProperties.TryGetValue(PropKey(playerPropKey, actorNumber), out value);

            Player player = room.GetPlayer(actorNumber);
            return player != null && player.CustomProperties.TryGetValue(playerPropKey, out value);
        }

        internal static Hashtable BotsTable(int[] numbers) => new Hashtable { [RoomProps.Bots] = numbers };
    }
}
