using System;

namespace Game.Core.Stats
{
    public static class ModifierConditions
    {
        public const ModifierCondition FirstMark = ModifierCondition.Stun;
        public const ModifierCondition LastMark = ModifierCondition.Blind;

        public static ModifierCondition Mark(int offset) => (ModifierCondition)((int)FirstMark + offset);

        public static ulong Bit(ModifierCondition condition) => condition == ModifierCondition.Always ? 0 : 1UL << (int)condition;

        public static string MarkName(ModifierCondition condition)
        {
            var name = condition.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static bool TryParse(string name, out ModifierCondition condition) =>
            Enum.TryParse(name, true, out condition) && condition != ModifierCondition.Always && Enum.IsDefined(typeof(ModifierCondition), condition);

        public static string Key(ModifierCondition condition) => "when." + MarkName(condition);
    }
}
