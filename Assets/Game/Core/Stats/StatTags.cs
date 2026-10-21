using System;

namespace Game.Core.Stats
{
    [Flags]
    public enum StatTags : uint
    {
        None = 0,
        Keyboard = 1 << 0,
        Mouse = 1 << 1,
        Click = 1 << 2,
        Idle = 1 << 3,
        Skill = 1 << 4,
        Aura = 1 << 5,
        DoT = 1 << 6,
        Crit = 1 << 7,
        Gold = 1 << 8,
        Minion = 1 << 9
    }
}
