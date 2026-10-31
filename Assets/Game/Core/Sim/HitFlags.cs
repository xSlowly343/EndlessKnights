using System;

namespace Game.Core.Sim
{
    [Flags]
    public enum HitFlags
    {
        None = 0,
        Crit = 1 << 0,
        ComboStrike = 1 << 1,
        HeavyStrike = 1 << 2,
        ShieldStrike = 1 << 3
    }
}
