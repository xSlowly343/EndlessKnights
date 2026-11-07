using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public readonly struct FullMount
    {
        public FullMount(int level, double damage)
        {
            Level = level;
            Damage = damage;
        }

        public int Level { get; }
        public double Damage { get; }
    }

    public sealed class MountRules
    {
        public const int Slots = 8;

        public int MaxLevel { get; private set; } = 25;
        public double Step { get; private set; } = 1;
        public long StagesPerLevel { get; private set; } = 2000;
        public double ScanBase { get; private set; }
        public double ScanGrowth { get; private set; } = 1;
        public double GoldKills { get; private set; }
        public double GoldGrowth { get; private set; } = 1;
        public IReadOnlyList<double> ScanByRarity { get; private set; } = Array.Empty<double>();
        public IReadOnlyList<FullMount> Full { get; private set; } = Array.Empty<FullMount>();
        public int CuttersPerLevel { get; private set; }

        public double Factor(int level) => System.Math.Pow(Step, System.Math.Max(0, System.Math.Min(MaxLevel, level)));

        public int Unlocked(long record) => (int)System.Math.Min(MaxLevel, System.Math.Max(0, record) / StagesPerLevel);

        public long ScanCost(int level) => (long)System.Math.Ceiling(ScanBase * System.Math.Pow(ScanGrowth, level));

        public double ScanFor(int rarity) => ScanByRarity.Count == 0 ? 0 : ScanByRarity[System.Math.Min(ScanByRarity.Count - 1, System.Math.Max(0, rarity))];

        public double FullDamage(int lowest)
        {
            var bonus = 0.0;
            for (var i = 0; i < Full.Count; i++)
            {
                if (lowest >= Full[i].Level)
                    bonus = System.Math.Max(bonus, Full[i].Damage);
            }
            return bonus;
        }

        internal static MountRules Read(ContentNode node)
        {
            var scan = new List<double>();
            foreach (var item in node.Field("scanByRarity").Items())
                scan.Add(item.AsNumber(0, 1e9));
            var full = new List<FullMount>();
            foreach (var item in node.Field("full").Items())
                full.Add(new FullMount(item.Field("level").AsInt(1, 100), item.Field("damage").AsNumber(0, 10)));
            var cost = node.Field("cost");
            return new MountRules
            {
                MaxLevel = node.Field("maxLevel").AsInt(1, 100),
                Step = node.Field("step").AsNumber(1, 2),
                StagesPerLevel = node.Field("stagesPerLevel").AsLong(1),
                ScanBase = cost.Field("scan").AsNumber(0, 1e9),
                ScanGrowth = cost.Field("scanGrowth").AsNumber(1, 10),
                GoldKills = cost.Field("goldKills").AsNumber(0, 1e9),
                GoldGrowth = cost.Field("goldGrowth").AsNumber(1, 10),
                ScanByRarity = scan,
                Full = full,
                CuttersPerLevel = node.Field("cuttersPerLevel").AsInt(0, 100)
            };
        }
    }
}
