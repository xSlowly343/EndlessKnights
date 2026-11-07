using System;

namespace Game.Core.Sim
{
    [Flags]
    public enum HitFlags
    {
        None = 0,
        Crit = 1 << 0,
        HeavyStrike = 1 << 2,
        Overcrit = 1 << 3
    }
}
