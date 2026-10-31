using System;

namespace Game.Core.Stats
{
    public static class ModifierConditions
    {
        public const ModifierCondition FirstMark = ModifierCondition.Stun;
        public const ModifierCondition LastMark = ModifierCondition.Blood;
        public const ModifierCondition FirstAllyMark = ModifierCondition.AllyStun;
        public const ModifierCondition LastAllyMark = ModifierCondition.AllyBlood;
        public const int Marks = LastMark - FirstMark + 1;
        private const string AllyPrefix = "Ally";

        public static ModifierCondition Mark(int offset) => (ModifierCondition)((int)FirstMark + offset);

        public static ModifierCondition AllyMark(int offset) => (ModifierCondition)((int)FirstAllyMark + offset);

        public static bool IsAlly(ModifierCondition condition) => condition >= FirstAllyMark && condition <= LastAllyMark;

        public static bool NeedsParty(ModifierCondition condition) =>
            IsAlly(condition) || condition == ModifierCondition.AnyAllyMark || condition == ModifierCondition.InParty;

        public static ulong Bit(ModifierCondition condition) => condition == ModifierCondition.Always ? 0 : 1UL << (int)condition;

        public static string MarkName(ModifierCondition condition)
        {
            var name = condition.ToString();
            if (IsAlly(condition))
                name = name.Substring(AllyPrefix.Length);
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static bool TryParse(string name, out ModifierCondition condition) =>
            Enum.TryParse(name, true, out condition) && condition != ModifierCondition.Always && Enum.IsDefined(typeof(ModifierCondition), condition);

        public static string Key(ModifierCondition condition)
        {
            var name = condition.ToString();
            return "when." + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
