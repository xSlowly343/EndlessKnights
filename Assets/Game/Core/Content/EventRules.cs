using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum EventGoalKind
    {
        Guests,
        BossFights,
        BossWins,
        Tokens,
        DailyQuests,
        PartyMinutes
    }

    public readonly struct EventGoal
    {
        public EventGoal(string id, EventGoalKind kind, long target)
        {
            Id = id;
            Kind = kind;
            Target = target;
        }

        public string Id { get; }
        public EventGoalKind Kind { get; }
        public long Target { get; }
    }

    public readonly struct EventShopItem
    {
        public EventShopItem(string cosmetic, long price)
        {
            Cosmetic = cosmetic;
            Price = price;
        }

        public string Cosmetic { get; }
        public long Price { get; }
    }

    public sealed class SeasonalEvent
    {
        public SeasonalEvent(string id, string nameRu, string nameEn, int month, int day, string tokenRu, string tokenEn, string bossRu, string bossEn,
            string guestBiome, IReadOnlyList<EventShopItem> shop, string clanEmblem)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Month = month;
            Day = day;
            TokenRu = tokenRu;
            TokenEn = tokenEn;
            BossRu = bossRu;
            BossEn = bossEn;
            GuestBiome = guestBiome;
            Shop = shop;
            ClanEmblem = clanEmblem;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int Month { get; }
        public int Day { get; }
        public string TokenRu { get; }
        public string TokenEn { get; }
        public string BossRu { get; }
        public string BossEn { get; }
        public string GuestBiome { get; }
        public IReadOnlyList<EventShopItem> Shop { get; }
        public string ClanEmblem { get; }
    }

    public readonly struct EventWindow
    {
        public EventWindow(SeasonalEvent seasonal, int year, long firstDay, long endDay)
        {
            Event = seasonal;
            Year = year;
            FirstDay = firstDay;
            EndDay = endDay;
        }

        public SeasonalEvent Event { get; }
        public int Year { get; }
        public long FirstDay { get; }
        public long EndDay { get; }
        public string Id => Event.Id + "_" + Year;
    }

    public sealed class EventRules
    {
        public const string FileName = "events.json";
        public const int SupportedVersion = 1;

        private static readonly IReadOnlyDictionary<string, EventGoalKind> GoalKinds = new Dictionary<string, EventGoalKind>(StringComparer.Ordinal)
        {
            ["guests"] = EventGoalKind.Guests,
            ["bossFights"] = EventGoalKind.BossFights,
            ["bossWins"] = EventGoalKind.BossWins,
            ["tokens"] = EventGoalKind.Tokens,
            ["dailyQuests"] = EventGoalKind.DailyQuests,
            ["partyMinutes"] = EventGoalKind.PartyMinutes
        };

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public int WindowDays { get; private set; }
        public int ShopGraceDays { get; private set; }
        public int GuestEvery { get; private set; }
        public long TokensPerGuest { get; private set; }
        public long DailyTokenCap { get; private set; }
        public int BossPerDay { get; private set; }
        public double BossTimerSeconds { get; private set; }
        public double BossHpMultiplier { get; private set; }
        public long BossTokens { get; private set; }
        public int ChampionTop { get; private set; }
        public string ChampionFrame { get; private set; } = string.Empty;
        public IReadOnlyList<EventGoal> Goals { get; private set; } = Array.Empty<EventGoal>();
        public long GoalTokens { get; private set; }
        public long GoalPassPoints { get; private set; }
        public long AllGoalsCrystals { get; private set; }
        public long ClanGuestsPerMember { get; private set; }
        public long ClanTarget { get; private set; }
        public IReadOnlyList<SeasonalEvent> Events { get; private set; } = Array.Empty<SeasonalEvent>();

        public static long DayOf(int year, int month, int day) => (long)(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc) - Epoch).TotalDays;

        public static int YearOf(long day) => Epoch.AddDays(day).Year;

        public EventWindow? WindowAt(long day)
        {
            var year = YearOf(day);
            foreach (var seasonal in Events)
            {
                for (var shift = -1; shift <= 1; shift++)
                {
                    var window = WindowOf(seasonal, year + shift);
                    if (day >= window.FirstDay && day < window.EndDay)
                        return window;
                }
            }
            return null;
        }

        public EventWindow? ShopWindowAt(long day)
        {
            var year = YearOf(day);
            foreach (var seasonal in Events)
            {
                for (var shift = -1; shift <= 1; shift++)
                {
                    var window = WindowOf(seasonal, year + shift);
                    if (day >= window.FirstDay && day < window.EndDay + ShopGraceDays)
                        return window;
                }
            }
            return null;
        }

        public EventWindow WindowOf(SeasonalEvent seasonal, int year)
        {
            var center = DayOf(year, seasonal.Month, seasonal.Day);
            var first = center - WindowDays / 2;
            return new EventWindow(seasonal, year, first, first + WindowDays);
        }

        public static EventRules Load(string json, CosmeticCatalog cosmetics)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var boss = root.Field("boss");
            var clan = root.Field("clanGoal");
            var rules = new EventRules
            {
                WindowDays = root.Field("windowDays").AsInt(1, 60),
                ShopGraceDays = root.Field("shopGraceDays").AsInt(0, 30),
                GuestEvery = root.Field("guestEvery").AsInt(1, 1000),
                TokensPerGuest = root.Field("tokensPerGuest").AsLong(1, 1000),
                DailyTokenCap = root.Field("dailyTokenCap").AsLong(1, 1_000_000),
                BossPerDay = boss.Field("perDay").AsInt(0, 100),
                BossTimerSeconds = boss.Field("timerSeconds").AsNumber(5, 600),
                BossHpMultiplier = boss.Field("hpMultiplier").AsNumber(0.01, 1000),
                BossTokens = boss.Field("tokens").AsLong(0, 1_000_000),
                ChampionTop = boss.Field("championTop").AsInt(1, 1000),
                ChampionFrame = boss.Field("championFrame").AsString(),
                GoalTokens = root.Field("goalTokens").AsLong(0, 1_000_000),
                GoalPassPoints = root.Field("goalPassPoints").AsLong(0, 1_000_000),
                AllGoalsCrystals = root.Field("allGoalsCrystals").AsLong(0, 1_000_000),
                ClanGuestsPerMember = clan.Field("guestsPerMember").AsLong(1, 1_000_000),
                ClanTarget = clan.Field("target").AsLong(1, 1_000_000_000)
            };
            Reward(cosmetics, rules.ChampionFrame, boss.Field("championFrame"), false);
            var goals = new List<EventGoal>();
            foreach (var node in root.Field("goals").Items())
            {
                var kindName = node.Field("kind").AsString();
                if (!GoalKinds.TryGetValue(kindName, out var kind))
                    throw node.Field("kind").Error($"unknown event goal kind '{kindName}'");
                goals.Add(new EventGoal(node.Field("id").AsString(), kind, node.Field("target").AsLong(1)));
            }
            rules.Goals = goals;
            var events = new List<SeasonalEvent>();
            foreach (var node in root.Field("events").Items())
            {
                var shop = new List<EventShopItem>();
                foreach (var item in node.Field("shop").Items())
                {
                    var id = item.Field("cosmetic").AsString();
                    if (!cosmetics.TryGet(id, out var cosmetic) || cosmetic.Source != "event")
                        throw item.Field("cosmetic").Error($"'{id}' is not an event cosmetic");
                    shop.Add(new EventShopItem(id, item.Field("price").AsLong(1, 1_000_000)));
                }
                var emblem = node.Field("clanEmblem").AsString();
                Reward(cosmetics, emblem, node.Field("clanEmblem"), true);
                var name = node.Field("name");
                var token = node.Field("token");
                var bossName = node.Field("boss");
                events.Add(new SeasonalEvent(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    node.Field("month").AsInt(1, 12), node.Field("day").AsInt(1, 31), token.Field("ru").AsString(), token.Field("en").AsString(),
                    bossName.Field("ru").AsString(), bossName.Field("en").AsString(), node.Field("guestBiome").AsString(), shop, emblem));
            }
            rules.Events = events;
            return rules;
        }

        private static void Reward(CosmeticCatalog cosmetics, string id, ContentNode node, bool clan)
        {
            if (!cosmetics.TryGet(id, out var item) || item.Source != "reward" || item.Clan != clan)
                throw node.Error($"'{id}' must be a {(clan ? "clan " : string.Empty)}reward cosmetic");
        }
    }
}
