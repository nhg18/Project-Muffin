using System;
using System.Collections.Generic;

namespace Chapchu.UI.Lobby
{
    public enum HubCurrency { Bone, Churu }

    public enum HubSlot { Character, Enter, Death, Card, Aura }

    public enum HubGrade { Common, Uncommon, Rare, Legend }

    public enum HubFriendStatus { Online, InGame, Offline }

    /// <summary>검색 결과 행의 관계 — 「요청」 버튼 / 「요청됨」 / 「친구」</summary>
    public enum HubRelation { None, Sent, Friend }

    /// <summary>잠긴 아이템을 얻는 곳</summary>
    public enum HubSource { Shop, Gacha }

    public sealed class HubItem
    {
        public string Id { get; }
        public string Name { get; }
        public HubSlot Slot { get; }
        public HubGrade Grade { get; }

        /// <summary>슬롯의 기본 아이템 — 뽑기에 나오지 않는다</summary>
        public bool IsDefault { get; }

        public bool IsOwned { get; set; }

        public HubItem(string id, string name, HubSlot slot, HubGrade grade, bool isDefault, bool isOwned)
        {
            Id = id; Name = name; Slot = slot; Grade = grade; IsDefault = isDefault; IsOwned = isOwned;
        }
    }

    public sealed class HubPerson
    {
        public string Id { get; }
        public string Nickname { get; }
        public HubFriendStatus Status { get; set; }

        public HubPerson(string id, string nickname, HubFriendStatus status = HubFriendStatus.Online)
        {
            Id = id; Nickname = nickname; Status = status;
        }
    }

    public sealed class HubSearchResult
    {
        public string Nickname { get; }
        public string Code { get; }
        public HubRelation Relation { get; set; }

        /// <summary>이번 검색에서 방금 보낸 요청 — 꺼진 「요청됨」 버튼으로 보인다</summary>
        public bool IsJustSent { get; set; }

        public HubSearchResult(string nickname, string code, HubRelation relation)
        {
            Nickname = nickname; Code = code; Relation = relation;
        }
    }

    public sealed class HubQuest
    {
        public string Id { get; }
        public string Title { get; }
        public int Goal { get; }
        public HubCurrency RewardCurrency { get; }
        public int RewardAmount { get; }
        public int Progress { get; set; }
        public bool IsClaimed { get; set; }

        public bool IsReady => !IsClaimed && Progress >= Goal;

        public HubQuest(string id, string title, int goal, HubCurrency rewardCurrency, int rewardAmount, int progress)
        {
            Id = id; Title = title; Goal = goal; RewardCurrency = rewardCurrency; RewardAmount = rewardAmount; Progress = progress;
        }
    }

    public sealed class HubPack
    {
        public string Id { get; }
        public int Churu { get; }
        public int Bonus { get; }

        public HubPack(string id, int churu, int bonus)
        {
            Id = id; Churu = churu; Bonus = bonus;
        }
    }

    public readonly struct HubGachaResult
    {
        public readonly HubItem Item;
        public readonly bool IsDuplicate;
        public readonly int Refund;

        public HubGachaResult(HubItem item, bool isDuplicate, int refund)
        {
            Item = item; IsDuplicate = isDuplicate; Refund = refund;
        }
    }

    /// <summary>
    /// 로비 허브의 임시 데이터 — 「통합 화면 UI v2」 로비(게시본 1794316285-7080)의 시안 값 그대로.
    /// 친구 · 재화 · 퀘스트 · 상점 · 뽑기 서버가 아직 없어 이 기기 메모리에만 있고, 로비에 들어올 때마다 처음 값으로 돌아간다.
    /// 값(가격 · 확률 · 보상)은 기획 「제안」 상태 — 서버가 붙으면 이 파일을 서버 응답으로 바꾼다 (docs/systems/13 · 19 · 24).
    /// </summary>
    public sealed class LobbyHubData
    {
        public const string UnnamedItem = "이름 미정";
        public const string GachaName = "개판 상자";
        public const string GachaSeason = "시즌 1 · 기간 미정";
        public const int GachaCostOne = 3;
        public const int GachaCostTen = 27;
        public const int GachaPityMax = 50;
        public const int DailyBonusChuru = 5;

        /// <summary>높은 등급부터 — 확률 표 · 뽑기 판정 순서</summary>
        public static readonly HubGrade[] GradesHighToLow = { HubGrade.Legend, HubGrade.Rare, HubGrade.Uncommon, HubGrade.Common };

        /// <summary>이번 상자 대표(개판) 아이템 — 캐릭터 · 입장 · 사망 · 카드 · 오라 순</summary>
        public static readonly string[] FeaturedIds = { "c6", "e5", "d5", "k6", "a6" };

        private static readonly string[] ShopCharacterIds = { "c2", "c3", "c4", "c5", "c6" };
        private static readonly string[] ShopEffectIds = { "e3", "e5", "d2", "d4", "k2", "k4", "k6", "a2", "a4", "a6" };
        private static readonly string[] RequestCandidates = { "꼬마젤리", "발바닥도장", "카드한장", "별사탕", "고양이손", "구름빵" };

        // 닉네임 · 친구 코드 · 나를 차단했는지 (차단한 사람은 검색에 나오지 않는다)
        private static readonly (string nickname, string code, bool blockedMe)[] SearchPool =
        {
            ("콩떡", "KD10-TT44", false), ("콩떡볶이", "BT88-LL12", false), ("콩콩이", "GR20-BB06", false), ("말랑콩", "PS61-BB77", false),
            ("초코칩", "CC07-PP31", false), ("광고왕", "AD00-XX11", false), ("콩깍지", "HD99-ZZ00", true),
        };

        private readonly List<HubItem> _items = new List<HubItem>();
        private readonly Dictionary<HubSlot, HubItem> _equipped = new Dictionary<HubSlot, HubItem>();
        private readonly Random _random = new Random();
        private int _bone = 1240;
        private int _churu = 12;
        private int _nextId;

        public string MyCode => "ML01-JY22";
        public int Wins => 12;
        public int Losses => 9;
        public int KillWins => 7;
        public int ChapchuWins => 5;

        public List<HubPerson> Friends { get; } = new List<HubPerson>();
        public List<HubPerson> RequestsIn { get; } = new List<HubPerson>();
        public List<HubPerson> RequestsOut { get; } = new List<HubPerson>();
        public List<HubPerson> Blocked { get; } = new List<HubPerson>();
        public List<HubQuest> Daily { get; } = new List<HubQuest>();
        public List<HubQuest> Weekly { get; } = new List<HubQuest>();
        public List<HubPack> Packs { get; } = new List<HubPack>();
        public bool IsDailyBonusClaimed { get; private set; }

        /// <summary>마지막 개판 뒤로 뽑은 횟수 — GachaPityMax 번째는 개판</summary>
        public int GachaPity { get; private set; } = 13;

        public LobbyHubData()
        {
            SeedItems();
            SeedFriends();
            SeedQuests();
            Packs.Add(new HubPack("p1", 30, 0));
            Packs.Add(new HubPack("p2", 80, 8));
            Packs.Add(new HubPack("p3", 200, 30));
        }

        #region 이름 · 값

        public static string CurrencyName(HubCurrency currency) => currency == HubCurrency.Bone ? "뼈다귀" : "츄르";

        public static string SlotName(HubSlot slot) => slot switch
        {
            HubSlot.Character => "캐릭터",
            HubSlot.Enter     => "입장",
            HubSlot.Death     => "사망",
            HubSlot.Card      => "카드",
            _                 => "오라",
        };

        public static string GradeName(HubGrade grade) => grade switch
        {
            HubGrade.Common   => "일반",
            HubGrade.Uncommon => "고급",
            HubGrade.Rare     => "희귀",
            _                 => "개판",
        };

        public static string StatusName(HubFriendStatus status) => status switch
        {
            HubFriendStatus.Online => "온라인",
            HubFriendStatus.InGame => "게임 중",
            _                      => "오프라인",
        };

        /// <summary>등급 확률 (%)</summary>
        public static int GachaRate(HubGrade grade) => grade switch
        {
            HubGrade.Common   => 60,
            HubGrade.Uncommon => 28,
            HubGrade.Rare     => 10,
            _                 => 2,
        };

        /// <summary>뽑기 중복 — 뼈다귀로 돌려받는 값</summary>
        public static int DuplicateRefund(HubGrade grade) => grade switch
        {
            HubGrade.Common   => 30,
            HubGrade.Uncommon => 80,
            HubGrade.Rare     => 200,
            _                 => 500,
        };

        /// <summary>상점 값. 개판은 뽑기에서만 나와 false.</summary>
        public static bool TryGetPrice(HubItem item, out HubCurrency currency, out int amount)
        {
            switch (item.Grade)
            {
                case HubGrade.Common:   currency = HubCurrency.Bone;  amount = 300; return true;
                case HubGrade.Uncommon: currency = HubCurrency.Bone;  amount = 800; return true;
                case HubGrade.Rare:     currency = HubCurrency.Churu; amount = 25;  return true;
                default:                currency = HubCurrency.Churu; amount = 0;   return false;
            }
        }

        #endregion

        #region 아이템 · 재화

        public HubItem FindItem(string id) => _items.Find(item => item.Id == id);

        public List<HubItem> ItemsOf(HubSlot slot) => _items.FindAll(item => item.Slot == slot);

        public List<HubItem> ShopItems(bool effects)
        {
            var list = new List<HubItem>();
            foreach (string id in effects ? ShopEffectIds : ShopCharacterIds)
                list.Add(FindItem(id));
            return list;
        }

        public HubItem Equipped(HubSlot slot) => _equipped[slot];

        public void Equip(HubItem item) => _equipped[item.Slot] = item;

        /// <summary>잠긴 아이템을 얻는 곳 — 상점에 있고 값이 있으면 상점, 아니면 뽑기</summary>
        public HubSource SourceOf(HubItem item)
        {
            bool inShop = Array.IndexOf(ShopCharacterIds, item.Id) >= 0 || Array.IndexOf(ShopEffectIds, item.Id) >= 0;
            return inShop && TryGetPrice(item, out _, out _) ? HubSource.Shop : HubSource.Gacha;
        }

        public int Balance(HubCurrency currency) => currency == HubCurrency.Bone ? _bone : _churu;

        public void Add(HubCurrency currency, int amount)
        {
            if (currency == HubCurrency.Bone)
                _bone = Math.Max(0, _bone + amount);
            else
                _churu = Math.Max(0, _churu + amount);
        }

        /// <summary>값을 치르고 보유로 바꾼다. 살 수 없으면(보유 · 값 없음 · 재화 부족) false.</summary>
        public bool Buy(HubItem item)
        {
            if (item.IsOwned || !TryGetPrice(item, out HubCurrency currency, out int amount) || Balance(currency) < amount)
                return false;

            Add(currency, -amount);
            item.IsOwned = true;
            return true;
        }

        #endregion

        #region 퀘스트

        public bool IsDailyBonusReady => !IsDailyBonusClaimed && Daily.TrueForAll(quest => quest.IsClaimed);

        public int DailyClaimedCount => Daily.FindAll(quest => quest.IsClaimed).Count;

        /// <summary>받을 수 있는 보상 수 — 일일은 「다 끝내기」 보너스를 더한다</summary>
        public int ReadyCount(bool daily)
        {
            int count = (daily ? Daily : Weekly).FindAll(quest => quest.IsReady).Count;
            return daily && IsDailyBonusReady ? count + 1 : count;
        }

        public bool Claim(HubQuest quest)
        {
            if (!quest.IsReady)
                return false;

            quest.IsClaimed = true;
            Add(quest.RewardCurrency, quest.RewardAmount);
            return true;
        }

        public bool ClaimDailyBonus()
        {
            if (!IsDailyBonusReady)
                return false;

            IsDailyBonusClaimed = true;
            Add(HubCurrency.Churu, DailyBonusChuru);
            return true;
        }

        /// <summary>그 탭에서 받을 수 있는 것을 모두 받는다. 받은 양을 돌려준다.</summary>
        public (int bone, int churu) ClaimAll(bool daily)
        {
            int bone = 0;
            int churu = 0;
            foreach (HubQuest quest in daily ? Daily : Weekly)
            {
                if (!Claim(quest))
                    continue;

                if (quest.RewardCurrency == HubCurrency.Bone)
                    bone += quest.RewardAmount;
                else
                    churu += quest.RewardAmount;
            }

            if (daily && ClaimDailyBonus())
                churu += DailyBonusChuru;

            return (bone, churu);
        }

        #endregion

        #region 뽑기

        public int GachaPoolCount(HubGrade grade) => GachaPool(grade).Count;

        /// <summary>
        /// count 번 뽑는다 (값은 부르는 쪽이 먼저 치른다). GachaPityMax 번째까지 개판이 없으면 그 회차는 개판, 10회는 희귀 이상 1개 보장.
        /// 이미 있는 아이템은 뼈다귀로 돌려준다 — 지갑에 바로 더한다.
        /// </summary>
        public List<HubGachaResult> Roll(int count)
        {
            var grades = new List<HubGrade>();
            for (int i = 0; i < count; i++)
            {
                GachaPity++;
                HubGrade grade = GachaPity >= GachaPityMax ? HubGrade.Legend : PickGrade();
                if (grade == HubGrade.Legend)
                    GachaPity = 0;

                grades.Add(grade);
            }

            if (count == 10 && !grades.Exists(grade => grade >= HubGrade.Rare))
                grades[9] = HubGrade.Rare;

            var results = new List<HubGachaResult>();
            foreach (HubGrade grade in grades)
            {
                List<HubItem> pool = GachaPool(grade);
                HubItem item = pool[_random.Next(pool.Count)];
                bool duplicate = item.IsOwned;
                int refund = duplicate ? DuplicateRefund(grade) : 0;
                item.IsOwned = true;
                Add(HubCurrency.Bone, refund);
                results.Add(new HubGachaResult(item, duplicate, refund));
            }

            return results;
        }

        private List<HubItem> GachaPool(HubGrade grade) => _items.FindAll(item => item.Grade == grade && !item.IsDefault);

        private HubGrade PickGrade()
        {
            double x = _random.NextDouble() * 100d;
            foreach (HubGrade grade in GradesHighToLow)
            {
                x -= GachaRate(grade);
                if (x < 0d)
                    return grade;
            }

            return HubGrade.Common;
        }

        #endregion

        #region 친구

        /// <summary>온라인 → 게임 중 → 오프라인, 같은 상태 안에서는 가나다순</summary>
        public List<HubPerson> SortedFriends()
        {
            var sorted = new List<HubPerson>(Friends);
            sorted.Sort((a, b) => a.Status != b.Status ? a.Status.CompareTo(b.Status) : string.CompareOrdinal(a.Nickname, b.Nickname));
            return sorted;
        }

        public void Accept(HubPerson request)
        {
            if (!RequestsIn.Remove(request))
                return;

            Friends.Add(new HubPerson(NewId(), request.Nickname));
        }

        public void Block(HubPerson friend)
        {
            if (Friends.Remove(friend))
                Blocked.Add(friend);
        }

        /// <summary>
        /// 닉네임 일부 또는 친구 코드 일부(- 없이)로 찾는다. 차단한 사람 · 나를 차단한 사람은 나오지 않는다. 빈 검색어는 null.
        /// </summary>
        public List<HubSearchResult> Search(string query)
        {
            string text = query?.Trim();
            if (string.IsNullOrEmpty(text))
                return null;

            string key = text.ToUpperInvariant().Replace("-", string.Empty);
            var results = new List<HubSearchResult>();
            foreach ((string nickname, string code, bool blockedMe) in SearchPool)
            {
                if (blockedMe || Blocked.Exists(person => person.Nickname == nickname))
                    continue;

                if (!nickname.Contains(text) && !code.Replace("-", string.Empty).Contains(key))
                    continue;

                HubRelation relation = Friends.Exists(person => person.Nickname == nickname) ? HubRelation.Friend
                    : RequestsOut.Exists(person => person.Nickname == nickname) ? HubRelation.Sent
                    : HubRelation.None;
                results.Add(new HubSearchResult(nickname, code, relation));
            }

            return results;
        }

        /// <summary>친구 요청을 보낸다. 상대도 나에게 요청한 상태면 바로 친구가 된다 (13-friend 2절).</summary>
        public void Request(HubSearchResult result)
        {
            HubPerson incoming = RequestsIn.Find(person => person.Nickname == result.Nickname);
            if (incoming != null)
            {
                Accept(incoming);
                result.Relation = HubRelation.Friend;
                return;
            }

            result.Relation = HubRelation.Sent;
            result.IsJustSent = true;
            RequestsOut.Add(new HubPerson(NewId(), result.Nickname));
        }

        /// <summary>확인용 — 아직 관계가 없는 사람 한 명이 친구 요청을 보낸다. 보낼 사람이 없으면 null.</summary>
        public HubPerson ReceiveRequest()
        {
            var candidates = new List<string>(RequestCandidates);
            candidates.RemoveAll(nickname => Friends.Exists(person => person.Nickname == nickname) || RequestsIn.Exists(person => person.Nickname == nickname)
                || RequestsOut.Exists(person => person.Nickname == nickname) || Blocked.Exists(person => person.Nickname == nickname));
            if (candidates.Count == 0)
                return null;

            var request = new HubPerson(NewId(), candidates[_random.Next(candidates.Count)]);
            RequestsIn.Add(request);
            return request;
        }

        /// <summary>확인용 — 초대를 보낼 온라인 친구 한 명. 없으면 null.</summary>
        public HubPerson PickInviter()
        {
            List<HubPerson> online = Friends.FindAll(person => person.Status == HubFriendStatus.Online);
            return online.Count == 0 ? null : online[_random.Next(online.Count)];
        }

        private string NewId() => "n" + _nextId++;

        #endregion

        #region 처음 값

        private void SeedItems()
        {
            AddItems(HubSlot.Character, "c", "치와와 기본형",
                HubGrade.Common, HubGrade.Common, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Rare, HubGrade.Legend);
            AddItems(HubSlot.Enter, "e", "기본 입장",
                HubGrade.Common, HubGrade.Uncommon, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Legend);
            AddItems(HubSlot.Death, "d", "기본 사망",
                HubGrade.Common, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Rare, HubGrade.Legend);
            AddItems(HubSlot.Card, "k", "기본 뒷면",
                HubGrade.Common, HubGrade.Common, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Rare, HubGrade.Legend);
            AddItems(HubSlot.Aura, "a", "없음",
                HubGrade.Common, HubGrade.Common, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Rare, HubGrade.Legend);
            FindItem("e2").IsOwned = true;
        }

        // 슬롯의 첫 아이템이 기본(보유 · 장착). 나머지는 이름이 아직 없다
        private void AddItems(HubSlot slot, string prefix, string defaultName, params HubGrade[] grades)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                bool isDefault = i == 0;
                var item = new HubItem(prefix + (i + 1), isDefault ? defaultName : UnnamedItem, slot, grades[i], isDefault, isDefault);
                _items.Add(item);
                if (isDefault)
                    _equipped[slot] = item;
            }
        }

        private void SeedFriends()
        {
            Friends.Add(new HubPerson(NewId(), "치와와대장", HubFriendStatus.Online));
            Friends.Add(new HubPerson(NewId(), "카드요정", HubFriendStatus.InGame));
            Friends.Add(new HubPerson(NewId(), "콩떡", HubFriendStatus.Online));
            Friends.Add(new HubPerson(NewId(), "새벽세시", HubFriendStatus.Offline));
            Friends.Add(new HubPerson(NewId(), "보리차", HubFriendStatus.Offline));
            Friends.Add(new HubPerson(NewId(), "멍뭉이", HubFriendStatus.InGame));
            Friends.Add(new HubPerson(NewId(), "꼬리콥터", HubFriendStatus.Online));
            RequestsIn.Add(new HubPerson(NewId(), "초코칩"));
            RequestsIn.Add(new HubPerson(NewId(), "젤리빈"));
            RequestsOut.Add(new HubPerson(NewId(), "꼬마발바닥"));
            Blocked.Add(new HubPerson(NewId(), "광고왕"));
        }

        private void SeedQuests()
        {
            Daily.Add(new HubQuest("d1", "판 3번 하기", 3, HubCurrency.Bone, 100, 2));
            Daily.Add(new HubQuest("d2", "1번 이기기", 1, HubCurrency.Bone, 150, 1));
            Daily.Add(new HubQuest("d3", "찹츄 1번 외치기", 1, HubCurrency.Churu, 3, 0));
            Daily.Add(new HubQuest("d4", "친구와 1판 하기", 1, HubCurrency.Bone, 80, 0));
            Weekly.Add(new HubQuest("w1", "판 15번 하기", 15, HubCurrency.Churu, 10, 9));
            Weekly.Add(new HubQuest("w2", "5번 이기기", 5, HubCurrency.Bone, 600, 2));
            Weekly.Add(new HubQuest("w3", "함정 카드 10번 쓰기", 10, HubCurrency.Bone, 300, 6));
            Weekly.Add(new HubQuest("w4", "찹츄 3번 외치기", 3, HubCurrency.Churu, 8, 1));
        }

        #endregion
    }
}
