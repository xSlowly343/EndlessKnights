using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class NodeScore
    {
        private const double MoreWeight = 3;
        private const double AttributeWeight = 0.005;
        private const double ConditionalWeight = 0.5;
        private const double BossOpeningWeight = 0.1;
        private const double PartyLineWeight = 0.5;
        private const double PartyUnlockWeight = 0.3;
        private const string ReactionPrefix = "reaction";

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
            [CombatStats.ResourceGain.Name] = 0.1,
            [CombatStats.Gold.Name] = 0.15,
            [CombatStats.OfflineGold.Name] = 0.1,
            [CombatStats.OfflineCapHours.Name] = 0.05,
            [CombatStats.AuraEffect.Name] = 0.5,
            [CombatStats.AuraDuration.Name] = 0.2,
            ["concurrentAuras"] = 0.3,
            [CombatStats.SkillPower.Name] = 0.3,
            [CombatStats.SkillDurationSeconds.Name] = 0.02,
            [CombatStats.ResourcePerCrit.Name] = 0.02,
            [CombatStats.ResourcePerFastSkill.Name] = 0.02,
            [CombatStats.ResourcePerCircle.Name] = 0.02,
            [CombatStats.ResourcePerFlick.Name] = 0.02,
            [CombatStats.PartyDamage.Name] = 1,
            [CombatStats.PartyCritChance.Name] = 0.3,
            [CombatStats.PartyGold.Name] = 0.15,
            [CombatStats.PartyResource.Name] = 0.1
        };

        private readonly StatTags _playedTags;
        private readonly bool _inParty;
        private readonly double _perAttributePoint;

        public NodeScore(StatTags playedTags, ClassAttributeDef attribute, bool inParty = false)
        {
            _playedTags = playedTags;
            _inParty = inParty;
            _perAttributePoint = AttributeWeight + 0 * Of(attribute.PerPoint);
        }

        public double Of(IReadOnlyList<Modifier> modifiers)
        {
            var score = 0.0;
            foreach (var modifier in modifiers)
                score += Of(modifier);
            return score;
        }

        public double Attribute(double amount) => amount * _perAttributePoint;

        private double Of(Modifier modifier)
        {
            if ((modifier.Tags & ~(_playedTags | StatTags.Gold | StatTags.Crit | StatTags.Skill | StatTags.Aura)) != 0)
                return 0;
            if (modifier.Stat == ClassAttributeDef.Stat)
                return Attribute(modifier.Value);
            if (!Weights.TryGetValue(modifier.Stat.BaseName, out var weight))
                return _inParty && IsPartyUnlock(modifier.Stat) ? modifier.Value * PartyUnlockWeight : 0;
            var value = modifier.Kind == ModifierKind.More ? modifier.Value * weight * MoreWeight : modifier.Value * weight;
            return value * Uptime(modifier.When);
        }

        private double Uptime(ModifierCondition condition)
        {
            if (condition == ModifierCondition.Always)
                return 1;
            if (condition == ModifierCondition.BossOpening)
                return BossOpeningWeight;
            if (ModifierConditions.IsAlly(condition))
                return _inParty ? PartyLineWeight : 0;
            return ConditionalWeight;
        }

        private static bool IsPartyUnlock(StatId stat) =>
            stat.BaseName.StartsWith(ReactionPrefix, System.StringComparison.Ordinal) || stat.Equals(CombatStats.SecondResponse);
    }
}
