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
        public AuraEffect(IReadOnlyList<Modifier> modifiers, bool party = false)
        {
            Modifiers = modifiers;
            Party = party;
        }

        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool Party { get; }
    }

    public sealed class EnemyDebuffEffect : SkillEffect
    {
        public EnemyDebuffEffect(double damageTaken)
        {
            DamageTaken = damageTaken;
        }

        public double DamageTaken { get; }
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
