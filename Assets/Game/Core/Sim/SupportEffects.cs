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

        public static double HitMultiplier(SkillSlot slot, GameState state)
        {
            var multiplier = 1.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.LowHpBelow > 0 && state.EnemyHp < state.EnemyMaxHp * support.LowHpBelow)
                    multiplier *= support.LowHpMultiplier;
            }
            return multiplier;
        }

        public static void AfterHit(SkillSlot slot, GameState state, BigNumber damage)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.QueueShare > 0)
                    state.QueueDamage[0] += damage * support.QueueShare;
            }
        }

        public static void OnKill(SkillSlot slot)
        {
            var seconds = 0.0;
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null)
                    seconds += support.KillCooldownSeconds;
            }
            if (seconds > 0)
                slot.CooldownTicks = System.Math.Max(0, slot.CooldownTicks - (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
        }
    }
}
