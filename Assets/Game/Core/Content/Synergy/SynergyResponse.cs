using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Synergy
{
    public sealed class SynergyResponse
    {
        public const int AnyMark = -1;

        public SynergyResponse(string id, HeroClass heroClass, int mark, bool second, bool perMark, int maxMarks, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            HeroClass = heroClass;
            Mark = mark;
            Second = second;
            PerMark = perMark;
            MaxMarks = maxMarks;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public HeroClass HeroClass { get; }
        public int Mark { get; }
        public bool Second { get; }
        public bool PerMark { get; }
        public int MaxMarks { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }
}
