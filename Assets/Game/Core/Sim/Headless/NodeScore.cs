using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class NodeScore
    {
        private const double MoreWeight = 3;
        private const double AttributeWeight = 0.005;

        private static readonly Dictionary<string, double> Weights = new Dictionary<string, double>(System.StringComparer.Ordinal)
        {
            [CombatStats.Damage.Name] = 1,
            [CombatStats.CritChance.Name] = 0.3,
            [CombatStats.CritMultiplier.Name] = 0.3,
            [CombatStats.FocusGain.Name] = 0.4,
            [CombatStats.FocusMax.Name] = 0.4,
            [CombatStats.FocusResetSeconds.Name] = 0.05,
            [CombatStats.ComboStrikeMultiplier.Name] = 0.2,
            [CombatStats.ComboTimeoutSeconds.Name] = 0.1,
            [CombatStats.KeyStrikeMultiplier.Name] = 0.05,
            [CombatStats.EnterDamage.Name] = 0.1,
            [CombatStats.ResourceGain.Name] = 0.1,
            [CombatStats.Gold.Name] = 0.15,
            [CombatStats.OfflineGold.Name] = 0.1,
            [CombatStats.OfflineCapHours.Name] = 0.05,
            [CombatStats.AuraEffect.Name] = 0.5,
            [CombatStats.AuraDuration.Name] = 0.2,
            ["concurrentAuras"] = 0.3
        };

        private readonly StatTags _playedTags;
        private readonly IReadOnlyCollection<string> _attributes;

        public NodeScore(StatTags playedTags, IReadOnlyCollection<string> attributes)
        {
            _playedTags = playedTags;
            _attributes = attributes;
        }

        public double Of(IReadOnlyList<Modifier> modifiers)
        {
            var score = 0.0;
            foreach (var modifier in modifiers)
                score += Of(modifier);
            return score;
        }

        public double Attribute(double amount) => amount * AttributeWeight;

        private double Of(Modifier modifier)
        {
            if ((modifier.Tags & ~(_playedTags | StatTags.Gold | StatTags.Crit | StatTags.Skill | StatTags.Aura)) != 0)
                return 0;
            foreach (var attribute in _attributes)
            {
                if (modifier.Stat.Name == attribute)
                    return Attribute(modifier.Value);
            }
            if (!Weights.TryGetValue(modifier.Stat.Name, out var weight))
                return 0;
            return modifier.Kind == ModifierKind.More ? modifier.Value * weight * MoreWeight : modifier.Value * weight;
        }
    }
}
