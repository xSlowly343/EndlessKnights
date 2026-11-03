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
                power *= support.Power * slot.Supports[i]!.DamageMultiplier * Product(support, SupportLineEffect.Power, Signals(slot, state));
                if (support.RhythmLongSeries > 0 && state.KeyStreak > support.RhythmLongSeries)
                    power *= support.RhythmPower;
            }
            slot.KeyHitsAtCast = state.StrikeHits;
            return power;
        }

        private static SupportSignals Signals(SkillSlot slot, GameState state) =>
            new SupportSignals(state.FightingBoss ? 1 : 0, state.PartyMembers, state.IdleTicksBeforeInput * GameLoop.TickSeconds,
                state.StrikeHits - slot.KeyHitsAtCast, System.Math.Max(0, state.FocusMultiplier - 1));

        private static double Product(SupportDef support, SupportLineEffect effect, in SupportSignals signals)
        {
            var product = 1.0;
            var lines = support.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Effect == effect)
                    product *= 1 + lines[i].Amount(signals);
            }
            return product;
        }

        private static double Sum(SupportDef support, SupportLineEffect effect, in SupportSignals signals)
        {
            var sum = 0.0;
            var lines = support.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Effect == effect)
                    sum += lines[i].Amount(signals);
            }
            return sum;
        }

        private static double Product(SkillSlot slot, GameState state, SupportLineEffect effect)
        {
            var product = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.Lines.Count > 0)
                    product *= Product(support, effect, Signals(slot, state));
            }
            return product;
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


        public static double ActiveKeyDamage(SkillSlot slot)
        {
            var keys = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    keys *= support.ActiveKeyDamage;
            }
            return keys;
        }


        public static double Redirect(SkillSlot slot)
        {
            var multiplier = 0.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.RedirectMultiplier > 0)
                    multiplier = System.Math.Max(multiplier, support.RedirectMultiplier);
            }
            return multiplier;
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
                if (support != null && support.Lines.Count > 0)
                    chance += Sum(support, SupportLineEffect.CritChance, Signals(slot, state));
            }
            return chance;
        }

        public static double CritMultiplier(SkillSlot slot, GameState state) => Product(slot, state, SupportLineEffect.CritMultiplier);

        public static double CostMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.CostMultiplier * slot.Supports[i]!.ManaMultiplier;
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



        public static double ExperienceMultiplier(SkillSlot slot, GameState state) => Product(slot, state, SupportLineEffect.Experience);

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
                if (support.VulnerabilityShare > 0)
                {
                    state.VulnerabilityShare = System.Math.Max(state.VulnerabilityShare, support.VulnerabilityShare);
                    state.VulnerabilityTicks = System.Math.Max(state.VulnerabilityTicks, (int)System.Math.Round(support.VulnerabilitySeconds * GameLoop.TicksPerSecond));
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

        public static SupportDef? KeyEcho(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.EchoEveryKeys > 0)
                    return support;
            }
            return null;
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
            var reward = KillReward(slot, state);
            if (reward > 1)
                state.NextKillReward = System.Math.Max(state.NextKillReward, reward);
        }

        public static double KillReward(SkillSlot slot, GameState state) => Product(slot, state, SupportLineEffect.KillReward);

        public static double Affliction(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.AfflictionMultiplier;
            }
            return multiplier;
        }

        public static double StackCap(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= support.StackCapMultiplier;
            }
            return multiplier;
        }
    }
}
