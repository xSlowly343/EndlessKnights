using Game.Core.Content.Gems;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public static class SupportEffects
    {
        public static double CooldownMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.CooldownMultiplier;
            }
            return multiplier;
        }

        public static double Power(SkillSlot slot, GameState state)
        {
            var power = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                power *= support.Power;
                if (support.QuietSeconds > 0 && state.IdleTicksBeforeInput * GameLoop.TickSeconds >= support.QuietSeconds)
                    power *= support.QuietPower;
                if (support.HungerHitsPerStep > 0)
                    power *= 1 + System.Math.Min(support.HungerMax, support.HungerStep * System.Math.Floor((state.KeyHits - slot.KeyHitsAtCast) / support.HungerHitsPerStep));
                if (support.FocusStep > 0)
                    power *= 1 + System.Math.Min(support.FocusBonusMax, support.FocusBonus * System.Math.Max(0, state.FocusMultiplier - 1) / support.FocusStep);
                if (support.RhythmLongSeries > 0 && state.KeyStreak > support.RhythmLongSeries)
                    power *= support.RhythmPower;
                if (support.CirclesForPower > 0 && slot.CirclesSinceCast >= support.CirclesForPower)
                    power *= support.CirclePower;
            }
            slot.KeyHitsAtCast = state.KeyHits;
            slot.CirclesSinceCast = 0;
            return power;
        }

        public static double AuraPower(SkillSlot slot)
        {
            var power = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    power *= support.AuraPowerMultiplier;
            }
            return power;
        }

        public static double MouseDistance(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.MouseDistanceMultiplier;
            }
            return multiplier;
        }

        public static void ActiveInputDamage(SkillSlot slot, ref double keys, ref double mouse)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                keys *= support.ActiveKeyDamage;
                mouse *= support.ActiveMouseDamage;
            }
        }

        public static SupportDef? Whirlpool(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.CirclesForPower > 0)
                    return support;
            }
            return null;
        }

        public static SupportDef? Tailwind(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.WindPxPerSecond > 0)
                    return support;
            }
            return null;
        }

        public static double CritChance(SkillSlot slot, GameState state)
        {
            var chance = 0.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.CritFocusStep > 0)
                    chance += support.CritPerFocusStep * System.Math.Max(0, state.FocusMultiplier - 1) / support.CritFocusStep;
            }
            return chance;
        }

        public static double CritMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.CritMultiplier;
            }
            return multiplier;
        }

        public static double CostMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.CostMultiplier;
            }
            return multiplier;
        }

        public static double DeferPerSecond(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.DeferPerSecond > 0)
                    return support.DeferPerSecond;
            }
            return 0;
        }

        public static int ExtraCharges(SkillSlot slot)
        {
            var charges = 0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    charges += support.ExtraCharges;
            }
            return charges;
        }

        public static SupportDef? Twins(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.TwinSeconds > 0)
                    return support;
            }
            return null;
        }

        public static double DurationMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.DurationMultiplier;
            }
            return multiplier;
        }

        public static double LingerMax(SkillSlot slot)
        {
            var max = 0.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    max = System.Math.Max(max, support.LingerMax);
            }
            return max;
        }

        public static double ExperienceMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.ExperienceMultiplier;
            }
            return multiplier;
        }

        public static double HitMultiplier(SkillSlot slot, GameState state)
        {
            var multiplier = 1.0;
            var marked = slot.MarkedSpawnTick == state.EnemySpawnTick;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                if (support.LowHpBelow > 0 && state.EnemyHp < state.EnemyMaxHp * support.LowHpBelow)
                    multiplier *= support.LowHpMultiplier;
                multiplier *= state.FightingBoss ? support.BossMultiplier : support.NormalMultiplier;
                if (marked)
                    multiplier *= support.MarkedMultiplier;
            }
            return multiplier;
        }

        public static void AfterHit(SkillSlot slot, GameState state, BigNumber damage, double poisonMax)
        {
            if (slot.MarkedSpawnTick != state.EnemySpawnTick)
                slot.InkHits = 0;
            slot.MarkedSpawnTick = state.EnemySpawnTick;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                if (support.QueueShare > 0)
                    state.QueueDamage[support.QueueSlot] += damage * support.QueueShare;
                if (support.PoisonPerHit > 0 && slot.InkHits < support.PoisonHitsMax)
                {
                    slot.InkHits++;
                    state.PoisonStacks = System.Math.Min(poisonMax, state.PoisonStacks + support.PoisonPerHit);
                }
            }
        }

        public static void OnKeyHit(SkillSlot slot, GameState state)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                if (support.RhythmEvery > 0 && state.KeyStreak % support.RhythmEvery == 0)
                    slot.CooldownTicks = System.Math.Max(0, slot.CooldownTicks - (int)System.Math.Round(support.RhythmCooldownSeconds * GameLoop.TicksPerSecond));
            }
        }

        public static SupportDef? Finish(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.FinishAfterHits > 0)
                    return support;
            }
            return null;
        }

        public static void OnKill(SkillSlot slot, GameState state)
        {
            var seconds = 0.0;
            var resource = 0.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support == null)
                    continue;
                seconds += support.KillCooldownSeconds;
                resource += support.KillResource;
            }
            if (seconds > 0)
                slot.CooldownTicks = System.Math.Max(0, slot.CooldownTicks - (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
            if (resource > 0)
                state.PendingResource += resource;
        }
    }
}
