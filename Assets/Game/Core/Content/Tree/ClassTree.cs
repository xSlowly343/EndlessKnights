using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class ClassTreeNode
    {
        public ClassTreeNode(string id, int row, int column, string? parentId, TreeTemplate template, bool common)
        {
            Id = id;
            Row = row;
            Column = column;
            ParentId = parentId;
            Template = template;
            Common = common;
        }

        public string Id { get; }
        public int Row { get; }
        public int Column { get; }
        public string? ParentId { get; }
        public ClassTreeNode? Parent { get; internal set; }
        public TreeTemplate Template { get; }
        public bool Common { get; }
        public string NameRu => Template.NameRu;
        public string NameEn => Template.NameEn;

        public IReadOnlyList<Modifier> ModifiersAt(int rank) => Template.ModifiersAt(rank);
    }

    public sealed class MajorTalentDef
    {
        public MajorTalentDef(string id, string nameRu, string nameEn, string descriptionRu, string descriptionEn, IReadOnlyList<Modifier> modifiers, string? waits)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            DescriptionRu = descriptionRu;
            DescriptionEn = descriptionEn;
            Modifiers = modifiers;
            Waits = waits;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string DescriptionRu { get; }
        public string DescriptionEn { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public string? Waits { get; }
    }

    public sealed class MajorTalentSlot
    {
        public MajorTalentSlot(int at, IReadOnlyList<MajorTalentDef> options)
        {
            At = at;
            Options = options;
        }

        public int At { get; }
        public IReadOnlyList<MajorTalentDef> Options { get; }
    }

    public sealed class ClassTree
    {
        private readonly Dictionary<string, ClassTreeNode> _byId = new Dictionary<string, ClassTreeNode>(StringComparer.Ordinal);

        public ClassTree(string heroClass, IReadOnlyList<ClassTreeNode> nodes, IReadOnlyList<int> rowThresholds, int barEvery,
            IReadOnlyList<IReadOnlyList<Modifier>> barPassives, IReadOnlyList<MajorTalentSlot> majors, RespecRules respec)
        {
            HeroClass = heroClass;
            Nodes = nodes;
            RowThresholds = rowThresholds;
            BarEvery = barEvery;
            BarPassives = barPassives;
            Majors = majors;
            Respec = respec;
            foreach (var node in nodes)
                _byId.Add(node.Id, node);
            foreach (var node in nodes)
            {
                if (node.ParentId != null)
                    node.Parent = _byId[node.ParentId];
            }
        }

        public string HeroClass { get; }
        public IReadOnlyList<ClassTreeNode> Nodes { get; }
        public IReadOnlyList<int> RowThresholds { get; }
        public int Rows => RowThresholds.Count;
        public int BarEvery { get; }
        public IReadOnlyList<IReadOnlyList<Modifier>> BarPassives { get; }
        public IReadOnlyList<MajorTalentSlot> Majors { get; }
        public RespecRules Respec { get; }
        public int MaxRanks => Nodes.Count * TreeTemplate.Ranks;

        public ClassTreeNode this[string id] => _byId[id];

        public bool TryGet(string id, out ClassTreeNode node) => _byId.TryGetValue(id, out node!);

        public int ThresholdOf(int row) => RowThresholds[row - 1];

        public IReadOnlyList<Modifier> BarPassiveAt(int step) => BarPassives[(step - 1) % BarPassives.Count];

        public MajorTalentSlot? MajorAt(int at)
        {
            foreach (var slot in Majors)
            {
                if (slot.At == at)
                    return slot;
            }
            return null;
        }
    }
}
