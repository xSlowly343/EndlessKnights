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

        public static double Power(SkillSlot slot)
        {
            var power = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    power *= support.Power;
            }
            return power;
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

        public static void AfterHit(SkillSlot slot, GameState state, BigNumber damage)
        {
            slot.MarkedSpawnTick = state.EnemySpawnTick;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.QueueShare > 0)
                    state.QueueDamage[0] += damage * support.QueueShare;
            }
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
