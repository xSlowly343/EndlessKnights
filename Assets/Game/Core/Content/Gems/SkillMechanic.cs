using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    [Flags]
    public enum SkillMechanic : ushort
    {
        None = 0,
        Strike = 1 << 0,
        Aura = 1 << 1,
        Cooldown = 1 << 2,
        Duration = 1 << 3,
        Cost = 1 << 4,
        Queue = 1 << 5,
        Mouse = 1 << 6,
        Idle = 1 << 7,
        Minion = 1 << 8,
        Dot = 1 << 9
    }

    public static class SkillMechanics
    {
        public static readonly IReadOnlyDictionary<string, SkillMechanic> ByName = new Dictionary<string, SkillMechanic>(StringComparer.Ordinal)
        {
            ["strike"] = SkillMechanic.Strike,
            ["aura"] = SkillMechanic.Aura,
            ["cooldown"] = SkillMechanic.Cooldown,
            ["duration"] = SkillMechanic.Duration,
            ["cost"] = SkillMechanic.Cost,
            ["queue"] = SkillMechanic.Queue,
            ["mouse"] = SkillMechanic.Mouse,
            ["idle"] = SkillMechanic.Idle,
            ["minion"] = SkillMechanic.Minion,
            ["dot"] = SkillMechanic.Dot
        };

        public static SkillMechanic Of(SkillDef? skill, StatTags tags)
        {
            if (skill == null)
                return SkillMechanic.None;
            var mechanics = SkillMechanic.None;
            switch (skill.Effect)
            {
                case AuraEffect:
                    mechanics |= SkillMechanic.Aura;
                    break;
                case StrikeEffect strike:
                    mechanics |= SkillMechanic.Strike;
                    if (strike.QueueTargets > 1)
                        mechanics |= SkillMechanic.Queue;
                    if (strike.BurnStacks > 0)
                        mechanics |= SkillMechanic.Dot;
                    if (strike.ConsumeMinion)
                        mechanics |= SkillMechanic.Minion;
                    break;
                case IdleStrikeEffect:
                    mechanics |= SkillMechanic.Strike | SkillMechanic.Idle;
                    break;
                case SummonEffect:
                    mechanics |= SkillMechanic.Minion;
                    break;
                case FlurryEffect:
                case ChainHitsEffect:
                case StreakStrikeEffect:
                case InputHitsEffect:
                case TrailEffect:
                case BossHuntEffect:
                    mechanics |= SkillMechanic.Strike;
                    break;
            }
            if (skill.CooldownSeconds > 0)
                mechanics |= SkillMechanic.Cooldown;
            if (skill.DurationSeconds > 0)
                mechanics |= SkillMechanic.Duration;
            if (skill.ResourceCost > 0)
                mechanics |= SkillMechanic.Cost;
            if ((tags & StatTags.Mouse) != 0)
                mechanics |= SkillMechanic.Mouse;
            if ((tags & StatTags.Idle) != 0)
                mechanics |= SkillMechanic.Idle;
            return mechanics;
        }

        public static SkillMechanic Read(ContentNode node)
        {
            var mechanics = SkillMechanic.None;
            foreach (var item in node.Items())
            {
                if (!ByName.TryGetValue(item.AsString(), out var mechanic))
                    throw item.Error($"unknown skill mechanic '{item.AsString()}', expected one of {string.Join(", ", ByName.Keys)}");
                mechanics |= mechanic;
            }
            if (mechanics == SkillMechanic.None)
                throw node.Error("a support fits at least one skill mechanic");
            return mechanics;
        }
    }
}
