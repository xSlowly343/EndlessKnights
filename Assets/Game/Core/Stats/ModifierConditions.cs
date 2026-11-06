using System;

namespace Game.Core.Stats
{
    public static class ModifierConditions
    {
        public static bool NeedsParty(ModifierCondition condition) => condition == ModifierCondition.InParty;

        public static ulong Bit(ModifierCondition condition) => condition == ModifierCondition.Always ? 0 : 1UL << (int)condition;

        public static bool TryParse(string name, out ModifierCondition condition) =>
            Enum.TryParse(name, true, out condition) && condition != ModifierCondition.Always && Enum.IsDefined(typeof(ModifierCondition), condition);

        public static string Key(ModifierCondition condition)
        {
            var name = condition.ToString();
            return "when." + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
