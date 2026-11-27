using System;
using System.Linq;
using Chapchu.Game.Bots;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// 방에 든 봇 목록 읽기 (18-bot.md 9절). 봇은 접속자가 아니라 방 속성(RoomProps.Bots)의 번호로만 있다.
    /// 봇 식별자 = 1,000,000,000 + 번호 — 게임 규칙 · 이벤트의 actorNumber 자리에 사람과 같이 쓰인다. 추가 · 제거는 NetworkManager.
    /// 사람 actorNumber 는 방에 들고 날 때마다 1씩 늘어 1000 쯤은 닿을 수 있다 — 봇 번호와 겹치지 않게 크게 둔다.
    /// </summary>
    public static class BotRoster
    {
        // 확정 (18-bot.md 4절 · 9절).
        public const int MaxBots = 3;
        public const int ActorBase = 1_000_000_000;

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

                // 원본(방장 메모리)은 믿는 방장만 읽는다 — 방장 자리를 뺏긴 사이에도 아무 클라나 쓰는 방 속성을 원본으로 읽지 않게 (09-network.md 13절 #1)
                int[] numbers = (NetworkManager.IsTrustedMaster ? NetworkManager.Instance.HostBots : null) ?? RoomNumbers;

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

        /// <summary>
        /// 봇의 난이도 (18-bot.md 7-7 · 9절) — 봇 번호(1 ~ 3)를 받는다. 방 속성에 없거나 틀린 값이면 보통.
        /// 원본은 봇 목록과 같이 방장 메모리다 — 방장은 자기가 정한 값을, 다른 클라는 방 속성 사본을 읽는다.
        /// </summary>
        public static BotLevel LevelOf(int number)
        {
            if (number < 1 || number > MaxBots)
                return BotLevel.Normal;

            int[] levels = (NetworkManager.IsTrustedMaster ? NetworkManager.Instance.HostBotLevels : null) ?? RoomLevels;
            return (BotLevel)levels[number - 1];
        }

        // 방 속성에 적힌 난이도 — [봇 번호 − 1] 자리. 방 속성은 아무 클라나 쓸 수 있다 — 키가 없거나 모양 · 값이 틀리면 그 자리는 보통
        internal static int[] RoomLevels
        {
            get
            {
                int[] levels = Enumerable.Repeat((int)BotLevel.Normal, MaxBots).ToArray();
                Room room = PhotonNetwork.CurrentRoom;
                if (room == null || !room.CustomProperties.TryGetValue(RoomProps.BotLevels, out object value) || !(value is int[] written) || written.Length != MaxBots)
                    return levels;

                for (int i = 0; i < MaxBots; i++)
                {
                    if (Enum.IsDefined(typeof(BotLevel), written[i]))
                        levels[i] = written[i];
                }

                return levels;
            }
        }

        /// <summary>봇의 식별자 (actorNumber 자리). 턴 순서 · 좌석은 사람 뒤에 이 순서로 (02-player.md 3절).</summary>
        public static int[] Actors => Numbers.Select(ActorOf).ToArray();

        /// <summary>사람 + 봇. 시작 최소 인원 · 정원 계산은 봇을 포함한다 (08-room.md 2절).</summary>
        public static int MemberCount => (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0) + Numbers.Length;

        /// <summary>「혼자 하기」 로 만든 방 — 다시 열어도 랜덤 매칭에서 뺀다 (18-bot.md 10절).</summary>
        public static bool IsSoloRoom => PhotonNetwork.CurrentRoom != null
                                         && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.Solo, out object solo) && solo is bool b && b;

        /// <summary>랜덤 매칭이 만든 매치 방 — 시각이 지나면 빈자리를 봇으로 채우고 시작한다 (08-room.md 4절 「매치」).</summary>
        public static bool IsMatchRoom => PhotonNetwork.CurrentRoom != null
                                          && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.Match, out object match) && match is bool b && b;

        public static int ActorOf(int number) => ActorBase + number;
        public static bool IsBot(int actorNumber) => actorNumber > ActorBase;

        // 봇 닉네임 (18-bot.md 4절) — 사람 닉네임처럼 보이는 이름. 「봇」 표시는 매치 방이 아닐 때만 화면이 붙인다
        private static readonly string[] BotNames =
        {
            "모찌", "호두", "두부", "보리", "깨비", "감자", "꿀떡", "말랑이",
            "누룽지", "단팥", "인절미", "꼬미", "뭉치", "송편", "쫀득이", "도토리",
        };

        /// <summary>
        /// 봇의 닉네임 (18-bot.md 4절). 방 코드 · 봇 번호로 정해 모든 기기에서 같고, 한 방의 봇 셋은 서로 다르다. 「봇」 표기는 매치 방이 아닐 때만 화면이 붙인다.
        /// string.GetHashCode 는 실행기마다 달라질 수 있어 직접 센다.
        /// </summary>
        public static string NicknameOf(int actorNumber)
        {
            int number = actorNumber - ActorBase;
            uint hash = 2166136261;
            foreach (char c in PhotonNetwork.CurrentRoom?.Name ?? string.Empty)
                hash = (hash ^ c) * 16777619;

            return BotNames[(int)((hash + (uint)number) % (uint)BotNames.Length)];
        }

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

        // 봇 목록과 난이도는 늘 한 번에 쓴다 — 둘이 엇갈린 순간이 없게
        internal static Hashtable BotsTable(int[] numbers, int[] levels) => new Hashtable { [RoomProps.Bots] = numbers, [RoomProps.BotLevels] = levels };
    }
}
