using Game.Core.Content.Gems;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public static class SupportEffects
    {
        public static double Grow(double bonus, double scale) => bonus > 0 ? bonus * scale : bonus;

        public static double GrowMultiplier(double multiplier, double scale) => 1 + Grow(multiplier - 1, scale);

        public static double ShrinkMultiplier(double multiplier, double scale) => multiplier < 1 ? 1 - (1 - multiplier) * scale : multiplier;

        public static double ScaleOf(SkillSlot slot, SupportDef support)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                if (ReferenceEquals(slot.Supports[i]?.Support, support))
                    return slot.SupportScale[i];
            }
            return 1;
        }

        public static double CooldownMultiplier(SkillSlot slot)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    multiplier *= ShrinkMultiplier(support.CooldownMultiplier, slot.SupportScale[i]);
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
                var scale = slot.SupportScale[i];
                power *= GrowMultiplier(support.Power, scale) * slot.Supports[i]!.DamageMultiplier * Product(support, SupportLineEffect.Power, Signals(slot, state), scale);
                if (support.RhythmLongSeries > 0 && state.KeyStreak > support.RhythmLongSeries)
                    power *= GrowMultiplier(support.RhythmPower, scale);
            }
            slot.KeyHitsAtCast = state.StrikeHits;
            return power;
        }

        private static SupportSignals Signals(SkillSlot slot, GameState state) =>
            new SupportSignals(state.FightingBoss ? 1 : 0, state.PartyMembers, state.IdleTicksBeforeInput * GameLoop.TickSeconds,
                state.StrikeHits - slot.KeyHitsAtCast, System.Math.Max(0, state.FocusMultiplier - 1));

        private static double Product(SupportDef support, SupportLineEffect effect, in SupportSignals signals, double scale)
        {
            var product = 1.0;
            var lines = support.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Effect == effect)
                    product *= 1 + Grow(lines[i].Amount(signals), scale);
            }
            return product;
        }

        private static double Sum(SupportDef support, SupportLineEffect effect, in SupportSignals signals, double scale)
        {
            var sum = 0.0;
            var lines = support.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Effect == effect)
                    sum += Grow(lines[i].Amount(signals), scale);
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
                    product *= Product(support, effect, Signals(slot, state), slot.SupportScale[i]);
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
                    power *= GrowMultiplier(support.AuraPowerMultiplier, slot.SupportScale[i]);
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
                    keys *= GrowMultiplier(support.ActiveKeyDamage, slot.SupportScale[i]);
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
                    chance += Sum(support, SupportLineEffect.CritChance, Signals(slot, state), slot.SupportScale[i]);
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
                    multiplier *= GrowMultiplier(support.LowHpMultiplier, slot.SupportScale[i]);
                if (marked)
                    multiplier *= GrowMultiplier(support.MarkedMultiplier, slot.SupportScale[i]);
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
                var scale = slot.SupportScale[i];
                if (support.QueueShare > 0)
                {
                    var last = System.Math.Min(state.QueueDamage.Length, support.QueueSlot + support.QueueTargets);
                    for (var slotIndex = support.QueueSlot; slotIndex < last; slotIndex++)
                        state.QueueDamage[slotIndex] += damage * (support.QueueShare * scale);
                }
                if (support.PoisonPerHit > 0 && slot.InkHits < support.PoisonHitsMax)
                {
                    slot.InkHits++;
                    state.PoisonStacks = System.Math.Min(poisonMax, state.PoisonStacks + support.PoisonPerHit * scale);
                }
                if (support.VulnerabilityShare > 0)
                {
                    state.VulnerabilityShare = System.Math.Max(state.VulnerabilityShare, support.VulnerabilityShare * scale);
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
                seconds += support.KillCooldownSeconds * slot.SupportScale[i];
                resource += support.KillResource * slot.SupportScale[i];
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
                    multiplier *= GrowMultiplier(support.AfflictionMultiplier, slot.SupportScale[i]);
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
                    multiplier *= GrowMultiplier(support.StackCapMultiplier, slot.SupportScale[i]);
            }
            return multiplier;
        }
    }
}
