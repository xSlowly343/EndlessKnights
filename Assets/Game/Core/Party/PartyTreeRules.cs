using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public enum PartyTreeTheme
    {
        Onslaught,
        Links,
        Support,
        Mascot,
        Colossus,
        Hearth,
        Treasury
    }

    public enum PartyTreeRefusal
    {
        None,
        Unknown,
        MaxRank,
        NoPoints,
        RowClosed,
        NoParent
    }

    public sealed class PartyTreeLine
    {
        private PartyTreeLine(Modifier? modifier, string waits, bool perMember, int minMembers, double price, int priceRows, double treeScale)
        {
            Modifier = modifier;
            Waits = waits;
            PerMember = perMember;
            MinMembers = minMembers;
            Price = price;
            PriceRows = priceRows;
            TreeScale = treeScale;
        }

        public Modifier? Modifier { get; }
        public string Waits { get; }
        public bool PerMember { get; }
        public int MinMembers { get; }
        public double Price { get; }
        public int PriceBasisPoints => (int)System.Math.Round(Price * 10000);
        public int PriceRows { get; }
        public double TreeScale { get; }
        public bool Works => Waits.Length == 0;

        public bool CutsPriceOf(int row) => (PriceRows & (1 << row)) != 0;

        internal static PartyTreeLine Read(ContentNode node)
        {
            if (node.HasField("price"))
            {
                var rows = 0;
                foreach (var row in node.Field("rows").Items())
                    rows |= 1 << row.AsInt(1, PartyTreeRules.Rows);
                return new PartyTreeLine(null, string.Empty, false, 0, node.Field("price").AsNumber(0, 0.5), rows, 0);
            }
            if (node.HasField("treeScale"))
                return new PartyTreeLine(null, string.Empty, false, 0, 0, 0, node.Field("treeScale").AsNumber(0, 10));
            var waits = node.HasField("waits") ? node.Field("waits").AsString() : string.Empty;
            return new PartyTreeLine(ModifierReader.Read(node), waits,
                node.HasField("perMember") && node.Field("perMember").AsBool(),
                node.HasField("minMembers") ? node.Field("minMembers").AsInt(1, 100) : 0, 0, 0, 0);
        }

        internal static IReadOnlyList<PartyTreeLine> ReadList(ContentNode node)
        {
            var lines = new List<PartyTreeLine>();
            foreach (var item in node.Items())
                lines.Add(Read(item));
            return lines;
        }
    }

    public sealed class PartyTreeNode
    {
        public PartyTreeNode(int number, int row, int parent, PartyTreeTheme theme, string nameRu, string nameEn, IReadOnlyList<PartyTreeLine> a, IReadOnlyList<PartyTreeLine> b, IReadOnlyList<PartyTreeLine> c, bool keystone)
        {
            Number = number;
            Row = row;
            Parent = parent;
            Theme = theme;
            NameRu = nameRu;
            NameEn = nameEn;
            A = a;
            B = b;
            C = c;
            Keystone = keystone;
        }

        public int Number { get; }
        public int Row { get; }
        public int Parent { get; }
        public PartyTreeTheme Theme { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<PartyTreeLine> A { get; }
        public IReadOnlyList<PartyTreeLine> B { get; }
        public IReadOnlyList<PartyTreeLine> C { get; }
        public bool Keystone { get; }

        public double AMultiplier(int rank) => rank <= 0 ? 0 : Keystone || rank == 1 ? 1 : rank < 4 ? 2 : 3;
    }

    public sealed class PartyTreeMajor
    {
        public PartyTreeMajor(string id, string nameRu, string nameEn, IReadOnlyList<PartyTreeLine> lines)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Lines = lines;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<PartyTreeLine> Lines { get; }
    }

    public sealed class PartyTreeMajorSlot
    {
        public PartyTreeMajorSlot(int at, IReadOnlyList<PartyTreeMajor> options)
        {
            At = at;
            Options = options;
        }

        public int At { get; }
        public IReadOnlyList<PartyTreeMajor> Options { get; }
    }

    public sealed class PartyTreeRules
    {
        public const int Rows = 7;

        public static readonly PartyTreeRules None = new PartyTreeRules();

        private static readonly Dictionary<string, PartyTreeTheme> Themes = new Dictionary<string, PartyTreeTheme>(StringComparer.Ordinal)
        {
            ["onslaught"] = PartyTreeTheme.Onslaught,
            ["links"] = PartyTreeTheme.Links,
            ["support"] = PartyTreeTheme.Support,
            ["mascot"] = PartyTreeTheme.Mascot,
            ["colossus"] = PartyTreeTheme.Colossus,
            ["hearth"] = PartyTreeTheme.Hearth,
            ["treasury"] = PartyTreeTheme.Treasury
        };

        public IReadOnlyList<PartyTreeNode> Nodes { get; private set; } = Array.Empty<PartyTreeNode>();
        public IReadOnlyList<int> RowThresholds { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<int> RowPrices { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<PartyTreeMajorSlot> Majors { get; private set; } = Array.Empty<PartyTreeMajorSlot>();
        public IReadOnlyList<PartyTreeLine> BarPassives { get; private set; } = Array.Empty<PartyTreeLine>();
        public int BarEvery { get; private set; } = 10;
        public int MaxPoints { get; private set; }
        public int MaxRank { get; private set; } = 5;
        public double MajorChangeHours { get; private set; }

        public PartyTreeNode? Node(int number) => number >= 1 && number <= Nodes.Count ? Nodes[number - 1] : null;

        public int Threshold(int row) => row >= 1 && row <= RowThresholds.Count ? RowThresholds[row - 1] : int.MaxValue;

        public static int Rank(IReadOnlyDictionary<int, int> levels, int node) => levels.TryGetValue(node, out var rank) ? rank : 0;

        public static int Points(IReadOnlyDictionary<int, int> levels)
        {
            var points = 0;
            foreach (var pair in levels)
                points += pair.Value;
            return points;
        }

        public int DiscountBasisPoints(int row, IReadOnlyDictionary<int, int> levels)
        {
            var discount = 0;
            foreach (var pair in levels)
            {
                var node = Node(pair.Key);
                if (node == null || pair.Value <= 0)
                    continue;
                foreach (var line in node.A)
                {
                    if (line.CutsPriceOf(row))
                        discount += line.PriceBasisPoints * (int)node.AMultiplier(pair.Value);
                }
            }
            return System.Math.Min(9000, discount);
        }

        public long Price(int number, IReadOnlyDictionary<int, int> levels)
        {
            var node = Node(number);
            return node == null ? 0 : ((long)RowPrices[node.Row - 1] * (10000 - DiscountBasisPoints(node.Row, levels)) + 5000) / 10000;
        }

        public PartyTreeRefusal CanRaise(int number, IReadOnlyDictionary<int, int> levels)
        {
            var node = Node(number);
            if (node == null)
                return PartyTreeRefusal.Unknown;
            if (Rank(levels, number) >= MaxRank)
                return PartyTreeRefusal.MaxRank;
            var points = Points(levels);
            if (points >= MaxPoints)
                return PartyTreeRefusal.NoPoints;
            if (points < Threshold(node.Row))
                return PartyTreeRefusal.RowClosed;
            if (node.Parent > 0 && Rank(levels, node.Parent) <= 0)
                return PartyTreeRefusal.NoParent;
            return PartyTreeRefusal.None;
        }

        public static PartyTreeRules Read(ContentNode node)
        {
            var nodes = new List<PartyTreeNode>();
            foreach (var item in node.Field("nodes").Items())
            {
                var number = nodes.Count + 1;
                var row = item.Field("row").AsInt(1, Rows);
                var parent = item.HasField("parent") ? item.Field("parent").AsInt(1, number - 1) : 0;
                if (parent > 0 && nodes[parent - 1].Row >= row)
                    throw item.Field("parent").Error("a parent must stand in a lower row");
                var themeName = item.Field("theme").AsString();
                if (!Themes.TryGetValue(themeName, out var theme))
                    throw item.Field("theme").Error($"unknown party tree theme '{themeName}'");
                var name = item.Field("name");
                nodes.Add(new PartyTreeNode(number, row, parent, theme, name.Field("ru").AsString(), name.Field("en").AsString(),
                    PartyTreeLine.ReadList(item.Field("a")), PartyTreeLine.ReadList(item.Field("b")), PartyTreeLine.ReadList(item.Field("c")),
                    item.HasField("keystone") && item.Field("keystone").AsBool()));
            }
            var thresholds = new List<int>();
            foreach (var item in node.Field("rows").Items())
                thresholds.Add(item.AsInt(0, 10000));
            var prices = new List<int>();
            foreach (var item in node.Field("rowPrices").Items())
                prices.Add(item.AsInt(1, 1000000));
            if (thresholds.Count != Rows || prices.Count != Rows)
                throw node.Field("rows").Error($"the party tree needs {Rows} rows and {Rows} row prices");
            var majors = new List<PartyTreeMajorSlot>();
            foreach (var slot in node.Field("majors").Items())
            {
                var options = new List<PartyTreeMajor>();
                foreach (var option in slot.Field("options").Items())
                {
                    var name = option.Field("name");
                    options.Add(new PartyTreeMajor(option.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), PartyTreeLine.ReadList(option.Field("lines"))));
                }
                majors.Add(new PartyTreeMajorSlot(slot.Field("at").AsInt(1, 10000), options));
            }
            var bar = node.Field("bar");
            return new PartyTreeRules
            {
                Nodes = nodes,
                RowThresholds = thresholds,
                RowPrices = prices,
                Majors = majors,
                BarEvery = bar.Field("every").AsInt(1, 1000),
                BarPassives = PartyTreeLine.ReadList(bar.Field("passives")),
                MaxPoints = node.Field("maxPoints").AsInt(1, 10000),
                MaxRank = node.Field("maxRank").AsInt(1, 10),
                MajorChangeHours = node.Field("majorChangeHours").AsNumber(0, 24 * 30)
            };
        }
    }
}
