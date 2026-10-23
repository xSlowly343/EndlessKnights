using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public abstract class SkillEffect
    {
    }

    public sealed class ChainHitsEffect : SkillEffect
    {
        public ChainHitsEffect(int hits, double nextEnemyShare)
        {
            Hits = hits;
            NextEnemyShare = nextEnemyShare;
        }

        public int Hits { get; }
        public double NextEnemyShare { get; }
    }

    public sealed class StrikeEffect : SkillEffect
    {
        public StrikeEffect(double multiplier)
        {
            Multiplier = multiplier;
        }

        public double Multiplier { get; }
    }

    public sealed class AuraEffect : SkillEffect
    {
        public AuraEffect(IReadOnlyList<Modifier> modifiers)
        {
            Modifiers = modifiers;
        }

        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class IdleStrikeEffect : SkillEffect
    {
        public IdleStrikeEffect(double chargePerSecond, double maxChargeSeconds, double minIdleSeconds)
        {
            ChargePerSecond = chargePerSecond;
            MaxChargeSeconds = maxChargeSeconds;
            MinIdleSeconds = minIdleSeconds;
        }

        public double ChargePerSecond { get; }
        public double MaxChargeSeconds { get; }
        public double MinIdleSeconds { get; }
    }
}
