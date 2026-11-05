using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public enum AwakeningInput
    {
        Keys,
        Clicks,
        MouseMeters,
        Kills,
        Bosses
    }

    public sealed class ArtifactAwakening
    {
        public const int InputCount = 5;

        private static readonly IReadOnlyDictionary<string, AwakeningInput> Names = new Dictionary<string, AwakeningInput>(StringComparer.Ordinal)
        {
            ["keys"] = AwakeningInput.Keys,
            ["clicks"] = AwakeningInput.Clicks,
            ["mouseMeters"] = AwakeningInput.MouseMeters,
            ["kills"] = AwakeningInput.Kills,
            ["bosses"] = AwakeningInput.Bosses
        };

        private readonly double[] _first;

        private ArtifactAwakening(int slots, double effectPerLevel, int maxLevel, double growth, double[] first)
        {
            Slots = slots;
            EffectPerLevel = effectPerLevel;
            MaxLevel = maxLevel;
            Growth = growth;
            _first = first;
        }

        public static readonly ArtifactAwakening None = new ArtifactAwakening(0, 0, 0, 1, new double[InputCount]);

        public int Slots { get; }
        public double EffectPerLevel { get; }
        public int MaxLevel { get; }
        public double Growth { get; }

        public double First(AwakeningInput input) => _first[(int)input];

        public double Needed(AwakeningInput input, int level)
        {
            var total = 0.0;
            var step = First(input);
            for (var i = 0; i < level; i++)
            {
                total += step;
                step *= Growth;
            }
            return total;
        }

        public int LevelAt(AwakeningInput input, double progress)
        {
            var level = 0;
            var step = First(input);
            var left = progress;
            while (level < MaxLevel && left >= step)
            {
                left -= step;
                step *= Growth;
                level++;
            }
            return level;
        }

        public double Scale(int level) => 1 + EffectPerLevel * level;

        public static bool TryParse(string name, out AwakeningInput input) => Names.TryGetValue(name, out input);

        public static string NameOf(AwakeningInput input)
        {
            foreach (var pair in Names)
            {
                if (pair.Value == input)
                    return pair.Key;
            }
            return input.ToString();
        }

        public static ArtifactAwakening Read(ContentNode node)
        {
            var first = new double[InputCount];
            var firstNode = node.Field("first");
            foreach (var pair in Names)
                first[(int)pair.Value] = firstNode.Field(pair.Key).AsNumber(1, 1e12);
            return new ArtifactAwakening(node.Field("slots").AsInt(1, 20), node.Field("effectPerLevel").AsNumber(0, 10),
                node.Field("maxLevel").AsInt(1, 1000), node.Field("growth").AsNumber(1, 10), first);
        }

        public static AwakeningInput Derive(ArtifactDef artifact)
        {
            foreach (var modifier in artifact.PerLevel)
            {
                var stat = modifier.Stat.Name;
                if (stat.IndexOf("oss", StringComparison.Ordinal) >= 0 || stat.IndexOf("uardian", StringComparison.Ordinal) >= 0)
                    return AwakeningInput.Bosses;
            }
            foreach (var modifier in artifact.PerLevel)
            {
                if ((modifier.Tags & StatTags.Mouse) != 0)
                    return AwakeningInput.MouseMeters;
                if ((modifier.Tags & (StatTags.Skill | StatTags.Click)) != 0)
                    return AwakeningInput.Clicks;
                if ((modifier.Tags & (StatTags.Gold | StatTags.Minion)) != 0)
                    return AwakeningInput.Kills;
            }
            return artifact.Type switch
            {
                ArtifactType.Economy => AwakeningInput.Kills,
                ArtifactType.Meta => AwakeningInput.Kills,
                ArtifactType.Tempo => AwakeningInput.Bosses,
                ArtifactType.Late => AwakeningInput.Bosses,
                _ => AwakeningInput.Keys
            };
        }
    }
}
