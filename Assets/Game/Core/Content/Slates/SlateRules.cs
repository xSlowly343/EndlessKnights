using System;
using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Content.Slates
{
    public sealed class SlateRarityRules
    {
        public SlateRarityRules(SlateRarity rarity, double weight, double power, int maxLines, int extraLines, bool rule, double dust)
        {
            Rarity = rarity;
            Weight = weight;
            Power = power;
            MaxLines = maxLines;
            ExtraLines = extraLines;
            Rule = rule;
            Dust = dust;
        }

        public SlateRarity Rarity { get; }
        public double Weight { get; }
        public double Power { get; }
        public int MaxLines { get; }
        public int ExtraLines { get; }
        public bool Rule { get; }
        public double Dust { get; }

        public int LinesFor(SlateShape shape) => System.Math.Min(shape.Lines, MaxLines) + ExtraLines;
    }

    public sealed class SlateDropRules
    {
        public SlateDropRules(double bossChance, double bossPerBiome, double bossMax, double guardianChance, SlateRarity guardianMinRarity, double dungeonChance, double riftChance)
        {
            BossChance = bossChance;
            BossPerBiome = bossPerBiome;
            BossMax = bossMax;
            GuardianChance = guardianChance;
            GuardianMinRarity = guardianMinRarity;
            DungeonChance = dungeonChance;
            RiftChance = riftChance;
        }

        public double BossChance { get; }
        public double BossPerBiome { get; }
        public double BossMax { get; }
        public double GuardianChance { get; }
        public SlateRarity GuardianMinRarity { get; }
        public double DungeonChance { get; }
        public double RiftChance { get; }

        public double BossChanceAt(int biome) => System.Math.Min(BossMax, BossChance + BossPerBiome * System.Math.Max(0, biome - 1));
    }

    public sealed class SlateRules
    {
        public const string FileName = "slates.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, SlateShape> _shapes;
        private readonly Dictionary<string, SlateTheme> _themes;
        private readonly Dictionary<string, SlateLine> _lines;
        private readonly Dictionary<HeroClass, IReadOnlyList<SlateLine>> _classLines;

        private SlateRules(int boardStart, int boardMax, int bagSize, double adjacencyPerNeighbour, double adjacencyMax, int patternSize, long temperShards,
            double temperChance, double classLineChance, SlateDropRules drops, IReadOnlyList<SlateRarityRules> rarities, IReadOnlyList<SlateShape> shapes,
            IReadOnlyList<SlateTheme> themes, Dictionary<HeroClass, IReadOnlyList<SlateLine>> classLines)
        {
            BoardStart = boardStart;
            BoardMax = boardMax;
            BagSize = bagSize;
            AdjacencyPerNeighbour = adjacencyPerNeighbour;
            AdjacencyMax = adjacencyMax;
            PatternSize = patternSize;
            TemperShards = temperShards;
            TemperChance = temperChance;
            ClassLineChance = classLineChance;
            Drops = drops;
            Rarities = rarities;
            Shapes = shapes;
            Themes = themes;
            _classLines = classLines;
            _shapes = new Dictionary<string, SlateShape>(StringComparer.Ordinal);
            foreach (var shape in shapes)
                _shapes.Add(shape.Id, shape);
            _themes = new Dictionary<string, SlateTheme>(StringComparer.Ordinal);
            _lines = new Dictionary<string, SlateLine>(StringComparer.Ordinal);
            foreach (var theme in themes)
            {
                _themes.Add(theme.Id, theme);
                foreach (var line in theme.Lines)
                    AddLine(line);
                AddLine(theme.Pattern);
                AddLine(theme.Rule);
            }
            foreach (var lines in classLines.Values)
            {
                foreach (var line in lines)
                    AddLine(line);
            }
        }

        public int BoardStart { get; }
        public int BoardMax { get; }
        public int BagSize { get; }
        public double AdjacencyPerNeighbour { get; }
        public double AdjacencyMax { get; }
        public int PatternSize { get; }
        public long TemperShards { get; }
        public double TemperChance { get; }
        public double ClassLineChance { get; }
        public SlateDropRules Drops { get; }
        public IReadOnlyList<SlateRarityRules> Rarities { get; }
        public IReadOnlyList<SlateShape> Shapes { get; }
        public IReadOnlyList<SlateTheme> Themes { get; }

        public SlateRarityRules Rarity(SlateRarity rarity) => Rarities[(int)rarity];

        public bool TryGetShape(string id, out SlateShape shape) => _shapes.TryGetValue(id, out shape!);

        public bool TryGetTheme(string id, out SlateTheme theme) => _themes.TryGetValue(id, out theme!);

        public bool TryGetLine(string id, out SlateLine line) => _lines.TryGetValue(id, out line!);

        public IReadOnlyList<SlateLine> ClassLines(HeroClass heroClass) =>
            _classLines.TryGetValue(heroClass, out var lines) ? lines : Array.Empty<SlateLine>();

        public (int Width, int Height) BoardSize(int limits)
        {
            var steps = System.Math.Max(0, limits);
            var width = System.Math.Min(BoardMax, BoardStart + (steps + 1) / 2);
            var height = System.Math.Min(BoardMax, BoardStart + steps / 2);
            return (width, height);
        }

        private void AddLine(SlateLine line)
        {
            if (!_lines.ContainsKey(line.Id))
                _lines.Add(line.Id, line);
            else if (!ReferenceEquals(_lines[line.Id], line))
                throw new ContentException(FileName, line.Id, $"line '{line.Id}' is defined twice");
        }

        public static SlateRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var board = root.Field("board");
            var start = board.Field("start").AsInt(1, 32);
            var max = board.Field("max").AsInt(start, 32);
            var adjacency = root.Field("adjacency");
            var temper = root.Field("temper");
            return new SlateRules(start, max, root.Field("bagSize").AsInt(1, 10000), adjacency.Field("perNeighbour").AsNumber(0, 10), adjacency.Field("max").AsNumber(0, 10),
                root.Field("patternSize").AsInt(2, 64), temper.Field("shards").AsLong(0), temper.Field("chance").AsNumber(0, 1), root.Field("classLineChance").AsNumber(0, 1),
                ReadDrops(root.Field("drops")), ReadRarities(root.Field("rarities")), ReadShapes(root.Field("shapes")), ReadThemes(root.Field("themes")),
                ReadClassLines(root.Field("classLines")));
        }

        private static SlateDropRules ReadDrops(ContentNode node)
        {
            var boss = node.Field("boss");
            var guardian = node.Field("guardian");
            return new SlateDropRules(boss.Field("chance").AsNumber(0, 1), boss.Field("perBiome").AsNumber(0, 1), boss.Field("max").AsNumber(0, 1),
                guardian.Field("chance").AsNumber(0, 1), ReadRarity(guardian.Field("minRarity")), node.Field("dungeon").AsNumber(0, 1), node.Field("rift").AsNumber(0, 1));
        }

        private static SlateRarity ReadRarity(ContentNode node)
        {
            var name = node.AsString();
            if (!SlateRarityNames.TryParse(name, out var rarity))
                throw node.Error($"unknown slate rarity '{name}'");
            return rarity;
        }

        private static IReadOnlyList<SlateRarityRules> ReadRarities(ContentNode node)
        {
            var rarities = new List<SlateRarityRules>();
            for (var i = 0; i < SlateRarityNames.Count; i++)
            {
                var rarity = (SlateRarity)i;
                var item = node.Field(SlateRarityNames.Of(rarity));
                rarities.Add(new SlateRarityRules(rarity, item.Field("weight").AsNumber(0, 1e6), item.Field("power").AsNumber(0, 100),
                    item.HasField("maxLines") ? item.Field("maxLines").AsInt(1, 8) : 8, item.HasField("extraLines") ? item.Field("extraLines").AsInt(0, 8) : 0,
                    item.HasField("rule") && item.Field("rule").AsBool(), item.Field("dust").AsNumber(0, 1e9)));
            }
            return rarities;
        }

        private static IReadOnlyList<SlateShape> ReadShapes(ContentNode node)
        {
            var shapes = new List<SlateShape>();
            foreach (var item in node.Items())
            {
                var cells = new List<(int X, int Y)>();
                foreach (var cell in item.Field("cells").Items())
                {
                    var xy = cell.Items();
                    if (xy.Count != 2)
                        throw cell.Error("a cell is [x, y]");
                    var point = (xy[0].AsInt(0, 8), xy[1].AsInt(0, 8));
                    if (cells.Contains(point))
                        throw cell.Error("the cell is listed twice");
                    cells.Add(point);
                }
                if (cells.Count == 0)
                    throw item.Field("cells").Error("a shape needs at least one cell");
                shapes.Add(new SlateShape(item.Field("id").AsString(), cells, item.Field("lines").AsInt(1, 8), item.Field("power").AsNumber(0, 100)));
            }
            if (shapes.Count == 0)
                throw node.Error("at least one shape is required");
            return shapes;
        }

        private static IReadOnlyList<SlateTheme> ReadThemes(ContentNode node)
        {
            var themes = new List<SlateTheme>();
            foreach (var item in node.Items())
            {
                var lines = new List<SlateLine>();
                foreach (var line in item.Field("lines").Items())
                    lines.Add(ReadLine(line, null));
                if (lines.Count == 0)
                    throw item.Field("lines").Error("a theme needs at least one line");
                themes.Add(new SlateTheme(item.Field("id").AsString(), lines, ReadLine(item.Field("pattern"), null), ReadLine(item.Field("rule"), null)));
            }
            if (themes.Count == 0)
                throw node.Error("at least one theme is required");
            return themes;
        }

        private static Dictionary<HeroClass, IReadOnlyList<SlateLine>> ReadClassLines(ContentNode node)
        {
            var byClass = new Dictionary<HeroClass, IReadOnlyList<SlateLine>>();
            foreach (var name in node.FieldNames())
            {
                if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                    throw node.Field(name).Error($"unknown class '{name}'");
                var lines = new List<SlateLine>();
                foreach (var line in node.Field(name).Items())
                    lines.Add(ReadLine(line, heroClass));
                byClass[heroClass] = lines;
            }
            return byClass;
        }

        private static SlateLine ReadLine(ContentNode node, HeroClass? heroClass)
        {
            var modifiers = ModifierReader.ReadList(node.Field("modifiers"));
            if (modifiers.Count == 0)
                throw node.Field("modifiers").Error("a line needs at least one modifier");
            return new SlateLine(node.Field("id").AsString(), modifiers, node.HasField("whole") && node.Field("whole").AsBool(), heroClass);
        }
    }
}
