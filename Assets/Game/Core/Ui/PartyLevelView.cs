using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class PartyLevelView
    {
        private PartyLevelView(PartyGrowthRules rules, double experience)
        {
            var level = rules.LevelOf(experience);
            Level = level.Level;
            Into = level.Into;
            Need = level.Need;
            Max = level.Max;
            Progress = level.Max || level.Need <= 0 ? 1 : System.Math.Min(1, level.Into / level.Need);
            NextLevel = rules.NextUnlockLevel(level.Level);
            var next = new List<string>();
            foreach (var unlock in rules.Unlocks)
            {
                if (unlock.Level == NextLevel)
                    next.Add(UnlockName(unlock.Kind));
            }
            NextUnlocks = next;
        }

        public int Level { get; }
        public double Into { get; }
        public double Need { get; }
        public bool Max { get; }
        public double Progress { get; }
        public int NextLevel { get; }
        public IReadOnlyList<string> NextUnlocks { get; }

        public string Title => Texts.Format("party.growth.level", Level);
        public string ProgressText => Max ? Texts.Get("party.growth.max") : Texts.Format("party.growth.progress", (long)System.Math.Floor(Into), (long)Need);
        public string NextText => NextLevel <= 0 ? string.Empty : Texts.Format("party.growth.next", NextLevel, string.Join(", ", NextUnlocks));

        public static PartyLevelView Of(PartyGrowthRules rules, double experience) => new PartyLevelView(rules, experience);

        public static string UnlockName(PartyUnlockKind kind) => Texts.Get("party.growth.unlock." + Key(kind));

        private static string Key(PartyUnlockKind kind)
        {
            var name = kind.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
