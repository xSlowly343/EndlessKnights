using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Math;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponSkillRules
    {
        private static readonly int TierCount = Enum.GetValues(typeof(SkillTier)).Length;
        private static readonly int RarityCount = Enum.GetValues(typeof(ItemRarity)).Length;

        private readonly double[][] _tierWeights;
        private readonly bool[] _guaranteedMythic;

        private WeaponSkillRules(IReadOnlyList<int> unlockLevels, int maxMythic, double[][] tierWeights, bool[] guaranteedMythic)
        {
            UnlockLevels = unlockLevels;
            MaxMythic = maxMythic;
            _tierWeights = tierWeights;
            _guaranteedMythic = guaranteedMythic;
        }

        public IReadOnlyList<int> UnlockLevels { get; }
        public int Places => UnlockLevels.Count;
        public int MaxMythic { get; }

        public IReadOnlyList<double> TierWeights(ItemRarity rarity) => _tierWeights[(int)rarity];

        public bool GuaranteesMythic(ItemRarity rarity) => _guaranteedMythic[(int)rarity];

        public SkillTier RollTier(ItemRarity rarity, Rng rng)
        {
            var weights = _tierWeights[(int)rarity];
            var total = 0.0;
            foreach (var weight in weights)
                total += weight;
            var roll = rng.NextDouble() * total;
            for (var tier = 0; tier < weights.Length; tier++)
            {
                roll -= weights[tier];
                if (roll < 0)
                    return (SkillTier)tier;
            }
            return LastWeighted(weights);
        }

        public static WeaponSkillRules Read(ContentNode node)
        {
            var levels = new List<int>();
            var previous = 1;
            foreach (var item in node.Field("unlockLevels").Items())
            {
                var level = item.AsInt(1, 10000);
                if (level < previous)
                    throw item.Error("unlock levels must not go down");
                levels.Add(level);
                previous = level;
            }
            if (levels.Count < 3)
                throw node.Field("unlockLevels").Error("a weapon needs the fast place, the heavy place and at least one free place");

            var maxMythic = node.Field("maxMythic").AsInt(0, levels.Count);

            var weightsNode = node.Field("tierWeights");
            var weights = new double[RarityCount][];
            foreach (var rarityName in weightsNode.FieldNames())
            {
                if (!ItemRarityNames.ByName.TryGetValue(rarityName, out var rarity))
                    throw weightsNode.Field(rarityName).Error($"unknown rarity '{rarityName}'");
                weights[(int)rarity] = ReadWeights(weightsNode.Field(rarityName));
            }
            foreach (var pair in ItemRarityNames.ByName)
            {
                if (weights[(int)pair.Value] == null)
                    throw weightsNode.Error($"rarity '{pair.Key}' has no skill tier weights");
            }

            var guaranteed = new bool[RarityCount];
            if (node.HasField("guaranteedMythic"))
            {
                foreach (var item in node.Field("guaranteedMythic").Items())
                {
                    if (!ItemRarityNames.ByName.TryGetValue(item.AsString(), out var rarity))
                        throw item.Error($"unknown rarity '{item.AsString()}'");
                    if (maxMythic == 0 || weights[(int)rarity][(int)SkillTier.Mythic] <= 0)
                        throw item.Error("a guaranteed mythic skill needs a mythic weight and maxMythic above 0");
                    guaranteed[(int)rarity] = true;
                }
            }
            return new WeaponSkillRules(levels, maxMythic, weights, guaranteed);
        }

        private static double[] ReadWeights(ContentNode node)
        {
            var weights = new List<double>();
            var total = 0.0;
            foreach (var item in node.Items())
            {
                var weight = item.AsNumber(0, 1e6);
                weights.Add(weight);
                total += weight;
            }
            if (weights.Count != TierCount)
                throw node.Error($"expected {TierCount} weights: common, rare, epic, legendary, mythic");
            if (total <= 0)
                throw node.Error("at least one tier needs a weight above 0");
            return weights.ToArray();
        }

        private static SkillTier LastWeighted(double[] weights)
        {
            for (var tier = weights.Length - 1; tier > 0; tier--)
            {
                if (weights[tier] > 0)
                    return (SkillTier)tier;
            }
            return SkillTier.Common;
        }
    }
}
