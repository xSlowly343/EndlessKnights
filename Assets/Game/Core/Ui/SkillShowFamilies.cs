using System;
using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public static class SkillShowFamilies
    {
        public const int Count = 4;

        private const char Separator = '_';
        private const double HeavyCooldownSeconds = 20d;

        private static readonly string[] Roots = { "hit", "queue", "auto", "strong" };

        public static SkillShowFamily Of(GemDef gem)
        {
            var id = gem.Root.Id;
            for (var family = 0; family < Roots.Length; family++)
            {
                if (Names(id, Roots[family]))
                    return (SkillShowFamily)family;
            }
            return Fallback(gem.Skill);
        }

        private static bool Names(string id, string root)
        {
            var at = id.IndexOf(root, StringComparison.Ordinal);
            while (at >= 0)
            {
                var end = at + root.Length;
                var opens = at > 0 && id[at - 1] == Separator;
                var closes = end == id.Length || id[end] == Separator;
                if (opens && closes)
                    return true;
                at = id.IndexOf(root, end, StringComparison.Ordinal);
            }
            return false;
        }

        private static SkillShowFamily Fallback(SkillDef? skill)
        {
            if (skill == null)
                return SkillShowFamily.Auto;
            if (skill.Role == SkillRole.Heavy || skill.CooldownSeconds >= HeavyCooldownSeconds)
                return SkillShowFamily.Strong;
            return skill.Role == SkillRole.Fast ? SkillShowFamily.Hit : SkillShowFamily.Auto;
        }
    }
}
