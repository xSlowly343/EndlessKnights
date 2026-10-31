using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class ClassTreeNode
    {
        public ClassTreeNode(string id, int row, int column, string? parentId, TreeTemplate template, bool passive)
        {
            Id = id;
            Row = row;
            Column = column;
            ParentId = parentId;
            Template = template;
            Passive = passive;
        }

        public string Id { get; }
        public int Row { get; }
        public int Column { get; }
        public string? ParentId { get; }
        public ClassTreeNode? Parent { get; internal set; }
        public TreeTemplate Template { get; }
        public bool Passive { get; }
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

        public ClassTree(string id, bool common, IReadOnlyList<ClassTreeNode> nodes, IReadOnlyList<int> rowThresholds, int barEvery,
            IReadOnlyList<IReadOnlyList<Modifier>> barPassives, IReadOnlyList<MajorTalentSlot> majors, RespecRules respec)
        {
            Id = id;
            Common = common;
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

        public string Id { get; }
        public bool Common { get; }
        public string NameRu { get; internal set; } = string.Empty;
        public string NameEn { get; internal set; } = string.Empty;
        public string AboutRu { get; internal set; } = string.Empty;
        public string AboutEn { get; internal set; } = string.Empty;
        public ClassAttributeDef Attribute { get; internal set; } = ClassAttributeDef.None;
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

        public bool HasBar => BarEvery > 0;

        public void RequireKnownSkills(Gems.GemCatalog gems)
        {
            foreach (var node in Nodes)
            {
                RequireKnownSkills(gems, node.Id, node.Template.First.Modifiers);
                RequireKnownSkills(gems, node.Id, node.Template.Second.Modifiers);
                RequireKnownSkills(gems, node.Id, node.Template.Third.Modifiers);
            }
            foreach (var slot in Majors)
            {
                foreach (var option in slot.Options)
                    RequireKnownSkills(gems, option.Id, option.Modifiers);
            }
        }

        private void RequireKnownSkills(Gems.GemCatalog gems, string owner, IReadOnlyList<Modifier> modifiers)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.Stat.IsForSkill && !gems.TryGet(modifier.Stat.SkillId, out _))
                    throw new ContentException(Common ? ClassTreeLoader.CommonFileNameOf(Id) : ClassTreeLoader.FileNameOf(Id), owner, $"unknown skill '{modifier.Stat.SkillId}'");
            }
        }

        public bool OffersMajor(string talentId)
        {
            foreach (var slot in Majors)
            {
                foreach (var option in slot.Options)
                {
                    if (option.Id == talentId)
                        return true;
                }
            }
            return false;
        }

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
