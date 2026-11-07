using System;
using System.Collections.Generic;
using Game.Core.Content.Rift;

namespace Game.Core.Content
{
    public enum TournamentScoreKind
    {
        Climb,
        Bosses,
        Kills
    }

    public sealed class TournamentFormat
    {
        public TournamentFormat(string id, TournamentScoreKind kind, string nameRu, string nameEn, long maxScore, bool prestige)
        {
            Id = id;
            Kind = kind;
            NameRu = nameRu;
            NameEn = nameEn;
            MaxScore = maxScore;
            Prestige = prestige;
        }

        public string Id { get; }
        public TournamentScoreKind Kind { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long MaxScore { get; }
        public bool Prestige { get; }
    }

    public readonly struct TournamentBand
    {
        public TournamentBand(long from, double scale)
        {
            From = from;
            Scale = scale;
        }

        public long From { get; }
        public double Scale { get; }
    }

    public readonly struct TournamentPlaceReward
    {
        public TournamentPlaceReward(int upTo, long ingots, long essence)
        {
            UpTo = upTo;
            Ingots = ingots;
            Essence = essence;
        }

        public int UpTo { get; }
        public long Ingots { get; }
        public long Essence { get; }
    }

    public sealed class TournamentRules
    {
        public const string FileName = "tournaments.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, TournamentScoreKind> Kinds = new Dictionary<string, TournamentScoreKind>(StringComparer.Ordinal)
        {
            ["climb"] = TournamentScoreKind.Climb,
            ["bosses"] = TournamentScoreKind.Bosses,
            ["kills"] = TournamentScoreKind.Kills
        };

        private TournamentRules(int groupSize, IReadOnlyList<int> startDays, int joinClosesHours, IReadOnlyList<TournamentFormat> formats,
            IReadOnlyList<TournamentBand> bands, IReadOnlyList<TournamentPlaceReward> rewards)
        {
            GroupSize = groupSize;
            StartDays = startDays;
            JoinClosesHours = joinClosesHours;
            Formats = formats;
            Bands = bands;
            Rewards = rewards;
        }

        public int GroupSize { get; }
        public IReadOnlyList<int> StartDays { get; }
        public int JoinClosesHours { get; }
        public IReadOnlyList<TournamentFormat> Formats { get; }
        public IReadOnlyList<TournamentBand> Bands { get; }
        public IReadOnlyList<TournamentPlaceReward> Rewards { get; }

        public TournamentFormat? Format(string id)
        {
            foreach (var format in Formats)
            {
                if (format.Id == id)
                    return format;
            }
            return null;
        }

        public int BandOf(long recordStage)
        {
            var band = 0;
            for (var i = 0; i < Bands.Count; i++)
            {
                if (recordStage >= Bands[i].From)
                    band = i;
            }
            return band;
        }

        public RiftReward RewardFor(int place, int band)
        {
            if (place < 1 || band < 0 || band >= Bands.Count)
                return default;
            var scale = Bands[band].Scale;
            foreach (var reward in Rewards)
            {
                if (place <= reward.UpTo)
                    return new RiftReward(0, Scaled(reward.Ingots, scale), 0, Scaled(reward.Essence, scale));
            }
            return default;
        }

        public static long Scaled(long amount, double scale) => (long)System.Math.Round(amount * scale, MidpointRounding.AwayFromZero);

        public static TournamentRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var groupSize = root.Field("groupSize").AsInt(2, 1000);
            return new TournamentRules(groupSize, StartDaysOf(root.Field("startDays")), root.Field("joinClosesHours").AsInt(0, 72),
                FormatsOf(root.Field("formats")), BandsOf(root.Field("bands")), RewardsOf(root.Field("rewards"), groupSize));
        }

        private static IReadOnlyList<int> StartDaysOf(ContentNode node)
        {
            var days = new List<int>();
            foreach (var item in node.Items())
            {
                var day = item.AsInt(0, 6);
                if (days.Count > 0 && day <= days[days.Count - 1])
                    throw item.Error("start days go in order from Monday (0) to Sunday (6) without repeats");
                days.Add(day);
            }
            if (days.Count == 0)
                throw node.Error("tournaments need at least one start day");
            return days;
        }

        private static IReadOnlyList<TournamentFormat> FormatsOf(ContentNode node)
        {
            var formats = new List<TournamentFormat>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!Kinds.TryGetValue(id, out var kind))
                    throw item.Field("id").Error($"unknown tournament format '{id}', expected climb, bosses or kills");
                if (!ids.Add(id))
                    throw item.Field("id").Error($"format '{id}' is listed twice");
                var name = item.Field("name");
                formats.Add(new TournamentFormat(id, kind, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("maxScore").AsLong(1),
                    item.HasField("prestige") && item.Field("prestige").AsBool()));
            }
            if (formats.Count == 0)
                throw node.Error("tournaments need at least one format");
            return formats;
        }

        private static IReadOnlyList<TournamentBand> BandsOf(ContentNode node)
        {
            var bands = new List<TournamentBand>();
            foreach (var item in node.Items())
            {
                var from = item.Field("from").AsLong(1);
                if (bands.Count == 0 && from != 1)
                    throw item.Field("from").Error("the first band starts at stage 1");
                if (bands.Count > 0 && from <= bands[bands.Count - 1].From)
                    throw item.Field("from").Error("bands go from lower to higher stages");
                bands.Add(new TournamentBand(from, item.Field("scale").AsNumber(0.1, 100)));
            }
            if (bands.Count == 0)
                throw node.Error("tournaments need at least one band");
            return bands;
        }

        private static IReadOnlyList<TournamentPlaceReward> RewardsOf(ContentNode node, int groupSize)
        {
            var rewards = new List<TournamentPlaceReward>();
            foreach (var item in node.Items())
            {
                var upTo = item.Field("upTo").AsInt(1, groupSize);
                if (rewards.Count > 0 && upTo <= rewards[rewards.Count - 1].UpTo)
                    throw item.Field("upTo").Error("reward places go from the first place down");
                var ingots = item.HasField("ingots") ? item.Field("ingots").AsLong(0, 1_000_000) : 0;
                var essence = item.HasField("essence") ? item.Field("essence").AsLong(0, 1_000_000) : 0;
                if (ingots == 0 && essence == 0)
                    throw item.Error("a reward place gives ingots or essence");
                rewards.Add(new TournamentPlaceReward(upTo, ingots, essence));
            }
            if (rewards.Count == 0)
                throw node.Error("tournaments need rewards");
            return rewards;
        }
    }
}
