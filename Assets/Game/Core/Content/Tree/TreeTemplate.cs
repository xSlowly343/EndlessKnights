using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class RankLine
    {
        public RankLine(IReadOnlyList<Modifier> modifiers, string textRu, string textEn, string? waits)
        {
            Modifiers = modifiers;
            TextRu = textRu;
            TextEn = textEn;
            Waits = waits;
        }

        public IReadOnlyList<Modifier> Modifiers { get; }
        public string TextRu { get; }
        public string TextEn { get; }
        public string? Waits { get; }
    }

    public sealed class TreeTemplate
    {
        public const int Ranks = 5;

        private readonly IReadOnlyList<Modifier>[] _byRank;

        public TreeTemplate(string id, string nameRu, string nameEn, RankLine first, RankLine second, RankLine third)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            First = first;
            Second = second;
            Third = third;
            _byRank = new IReadOnlyList<Modifier>[Ranks + 1];
            for (var rank = 0; rank <= Ranks; rank++)
                _byRank[rank] = Collect(rank);
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public RankLine First { get; }
        public RankLine Second { get; }
        public RankLine Third { get; }

        public static int FirstLineRepeats(int rank) => rank >= 4 ? 3 : rank >= 2 ? 2 : rank;

        public IReadOnlyList<Modifier> ModifiersAt(int rank) => _byRank[System.Math.Max(0, System.Math.Min(Ranks, rank))];

        private IReadOnlyList<Modifier> Collect(int rank)
        {
            var modifiers = new List<Modifier>();
            var repeats = FirstLineRepeats(rank);
            foreach (var modifier in First.Modifiers)
            {
                if (repeats > 0)
                    modifiers.Add(new Modifier(modifier.Stat, modifier.Kind, modifier.Value * repeats, modifier.Tags));
            }
            if (rank >= 3)
                modifiers.AddRange(Second.Modifiers);
            if (rank >= 5)
                modifiers.AddRange(Third.Modifiers);
            return modifiers;
        }
    }
}
