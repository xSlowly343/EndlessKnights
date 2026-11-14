using System;
using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Ui
{
    public sealed class LifeSpecial
    {
        public LifeSpecial(string id, int after, int during, DayPhase? phase, long afterMs)
        {
            Id = id;
            After = after;
            During = during;
            Phase = phase;
            AfterMs = afterMs;
        }

        public string Id { get; }
        public int After { get; }
        public int During { get; }
        public DayPhase? Phase { get; }
        public long AfterMs { get; }
    }

    public sealed class BiomeLook
    {
        private readonly double[] _weights;
        private readonly double _total;

        public BiomeLook(string id, bool alwaysNight, string[] weathers, double[] weights, LifeSpecial? special,
            string[] dayBackground, string[] dayGround, string[] nightBackground, string[] nightGround, string rare)
        {
            Id = id;
            AlwaysNight = alwaysNight;
            Weathers = weathers;
            _weights = weights;
            Special = special;
            DayBackground = dayBackground;
            DayGround = dayGround;
            NightBackground = nightBackground;
            NightGround = nightGround;
            Rare = rare;
            Seed = WorldWeather.SeedOf(id);
            foreach (var weight in weights)
                _total += weight;
        }

        public string Id { get; }
        public bool AlwaysNight { get; }
        public IReadOnlyList<string> Weathers { get; }
        public LifeSpecial? Special { get; }
        public IReadOnlyList<string> DayBackground { get; }
        public IReadOnlyList<string> DayGround { get; }
        public IReadOnlyList<string> NightBackground { get; }
        public IReadOnlyList<string> NightGround { get; }
        public string Rare { get; }
        public ulong Seed { get; }

        public double WeightOf(int weather) => _weights[weather];

        public int PickWeather(double unit)
        {
            var roll = unit * _total;
            for (var i = 0; i < _weights.Length; i++)
            {
                roll -= _weights[i];
                if (roll < 0)
                    return i;
            }
            return _weights.Length - 1;
        }
    }

    public sealed class BiomeLifeRules
    {
        public const string FileName = "biome_life.json";
        public const int SupportedVersion = 1;
        public const int StreakTiers = 3;

        private const long MinuteMs = 60_000;
        private static readonly string[] PhaseNames = { "morning", "day", "evening", "night" };

        private readonly Dictionary<string, BiomeLook> _byBiome = new Dictionary<string, BiomeLook>(StringComparer.Ordinal);
        private readonly List<BiomeLook> _looks = new List<BiomeLook>();
        private readonly double[] _phaseStarts = new double[PhaseNames.Length];
        private readonly double[] _streakSeconds = new double[StreakTiers];

        private BiomeLifeRules()
        {
        }

        public IReadOnlyList<BiomeLook> Looks => _looks;
        public BiomeLook RiftLook { get; private set; } = null!;
        public long WeatherMinMs { get; private set; }
        public long WeatherMaxMs { get; private set; }
        public double WeatherFadeSeconds { get; private set; }
        public double BossDim { get; private set; }
        public double FrontCover { get; private set; }
        public double DayFadeHours { get; private set; }
        public double EnemyZoneWidthPx { get; private set; }
        public double EnemyZoneHeightPx { get; private set; }
        public double StripWidthPx { get; private set; }
        public double RestSeconds { get; private set; }
        public long RareMinMs { get; private set; }
        public long RareMaxMs { get; private set; }
        public long RareShowMs { get; private set; }

        public double FreeWidthPx => StripWidthPx - EnemyZoneWidthPx;

        public bool InEnemyZone(double xPx, double heightPx) => xPx >= FreeWidthPx && heightPx <= EnemyZoneHeightPx;

        public BiomeLook LookOf(string biome) => _byBiome.TryGetValue(biome, out var look) ? look : _looks[0];

        public bool Knows(string biome) => _byBiome.ContainsKey(biome);

        public int StreakTier(double seconds)
        {
            var tier = 0;
            while (tier < StreakTiers && seconds >= _streakSeconds[tier])
                tier++;
            return tier;
        }

        public DayLight LightAt(double hour)
        {
            var phase = PhaseNames.Length - 1;
            for (var i = 0; i < PhaseNames.Length; i++)
            {
                if (hour >= _phaseStarts[i])
                    phase = i;
            }
            var since = hour - _phaseStarts[phase];
            if (since < 0)
                since += 24;
            if (DayFadeHours <= 0 || since >= DayFadeHours)
                return DayLight.Steady((DayPhase)phase);
            var before = (phase + PhaseNames.Length - 1) % PhaseNames.Length;
            return new DayLight((DayPhase)before, (DayPhase)phase, since / DayFadeHours);
        }

        public static BiomeLifeRules Load(string json, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var rules = new BiomeLifeRules();
            var weather = root.Field("weather");
            rules.WeatherMinMs = (long)(weather.Field("minMinutes").AsNumber(1, 1440) * MinuteMs);
            rules.WeatherMaxMs = (long)(weather.Field("maxMinutes").AsNumber(1, 1440) * MinuteMs);
            if (rules.WeatherMaxMs < rules.WeatherMinMs * 2)
                throw weather.Field("maxMinutes").Error("the longest weather must be at least twice the shortest");
            rules.WeatherFadeSeconds = weather.Field("fadeSeconds").AsNumber(0, 600);
            rules.BossDim = weather.Field("bossDim").AsNumber(0, 1);
            rules.FrontCover = weather.Field("frontCover").AsNumber(0, 1);
            var day = root.Field("day");
            for (var i = 0; i < PhaseNames.Length; i++)
            {
                rules._phaseStarts[i] = day.Field(PhaseNames[i]).AsNumber(0, 24);
                if (i > 0 && rules._phaseStarts[i] <= rules._phaseStarts[i - 1])
                    throw day.Field(PhaseNames[i]).Error("phases go in order through the day");
            }
            rules.DayFadeHours = day.Field("fadeMinutes").AsNumber(0, 120) / 60;
            var zone = root.Field("enemyZone");
            rules.StripWidthPx = zone.Field("stripWidthPx").AsNumber(1, 10000);
            rules.EnemyZoneWidthPx = zone.Field("widthPx").AsNumber(0, rules.StripWidthPx - 1);
            rules.EnemyZoneHeightPx = zone.Field("heightPx").AsNumber(0, 10000);
            var reactions = root.Field("reactions");
            rules.RestSeconds = reactions.Field("restSeconds").AsNumber(1, 36000);
            var streak = reactions.Field("streakSeconds").Items();
            if (streak.Count != StreakTiers)
                throw reactions.Field("streakSeconds").Error($"expected {StreakTiers} tiers");
            for (var i = 0; i < StreakTiers; i++)
                rules._streakSeconds[i] = streak[i].AsNumber(i == 0 ? 0 : rules._streakSeconds[i - 1], 36000);
            var rare = root.Field("rare");
            rules.RareMinMs = (long)(rare.Field("minMinutes").AsNumber(1, 1440) * MinuteMs);
            rules.RareMaxMs = (long)(rare.Field("maxMinutes").AsNumber(1, 1440) * MinuteMs);
            if (rules.RareMaxMs < rules.RareMinMs * 2)
                throw rare.Field("maxMinutes").Error("the longest wait must be at least twice the shortest");
            rules.RareShowMs = (long)(rare.Field("seconds").AsNumber(1, 600) * 1000);

            var byId = new Dictionary<string, BiomeLook>(StringComparer.Ordinal);
            foreach (var node in root.Field("looks").Items())
            {
                var look = ReadLook(node);
                if (byId.ContainsKey(look.Id))
                    throw node.Field("id").Error($"look '{look.Id}' is listed twice");
                byId[look.Id] = look;
                rules._looks.Add(look);
            }
            if (rules._looks.Count == 0)
                throw root.Field("looks").Error("at least one look is needed");
            var map = root.Field("biomes");
            foreach (var biome in map.FieldNames())
                rules._byBiome[biome] = Find(byId, map.Field(biome));
            foreach (var biome in biomes)
            {
                if (!rules._byBiome.ContainsKey(biome))
                    throw map.Error($"biome '{biome}' from {StageTable.FileName} has no look");
            }
            rules.RiftLook = Find(byId, root.Field("rift"));
            return rules;
        }

        private static BiomeLook Find(Dictionary<string, BiomeLook> byId, ContentNode node)
        {
            if (!byId.TryGetValue(node.AsString(), out var look))
                throw node.Error($"unknown look '{node.AsString()}'");
            return look;
        }

        private static BiomeLook ReadLook(ContentNode node)
        {
            var ids = new List<string>();
            var weights = new List<double>();
            foreach (var weather in node.Field("weathers").Items())
            {
                var id = weather.Field("id").AsString();
                if (ids.Contains(id))
                    throw weather.Field("id").Error($"weather '{id}' is listed twice");
                ids.Add(id);
                weights.Add(weather.Field("weight").AsNumber(0.001, 1000));
            }
            if (ids.Count == 0)
                throw node.Field("weathers").Error("a look needs at least one weather");
            var special = node.HasField("special") ? ReadSpecial(node.Field("special"), ids) : null;
            var day = node.Field("day");
            var night = node.Field("night");
            return new BiomeLook(node.Field("id").AsString(), node.HasField("alwaysNight") && node.Field("alwaysNight").AsBool(), ids.ToArray(), weights.ToArray(), special,
                Names(day.Field("background")), Names(day.Field("ground")), Names(night.Field("background")), Names(night.Field("ground")),
                node.HasField("rare") ? node.Field("rare").AsString() : string.Empty);
        }

        private static LifeSpecial ReadSpecial(ContentNode node, List<string> weathers)
        {
            var after = node.HasField("after") ? WeatherIndex(node.Field("after"), weathers) : -1;
            var during = node.HasField("during") ? WeatherIndex(node.Field("during"), weathers) : -1;
            DayPhase? phase = null;
            if (node.HasField("phase"))
            {
                var index = Array.IndexOf(PhaseNames, node.Field("phase").AsString());
                if (index < 0)
                    throw node.Field("phase").Error($"'{node.Field("phase").AsString()}' is not one of {string.Join(", ", PhaseNames)}");
                phase = (DayPhase)index;
            }
            if (after < 0 && during < 0 && phase == null)
                throw node.Error("a special needs 'after', 'during' or 'phase'");
            var afterMs = after >= 0 ? (long)(node.Field("minutes").AsNumber(0.1, 1440) * MinuteMs) : 0;
            return new LifeSpecial(node.Field("id").AsString(), after, during, phase, afterMs);
        }

        private static int WeatherIndex(ContentNode node, List<string> weathers)
        {
            var index = weathers.IndexOf(node.AsString());
            if (index < 0)
                throw node.Error($"weather '{node.AsString()}' is not in this look");
            return index;
        }

        private static string[] Names(ContentNode node)
        {
            var items = node.Items();
            var names = new string[items.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = items[i].AsString();
            return names;
        }
    }
}
