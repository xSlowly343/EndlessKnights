using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeNodeDef
    {
        private readonly List<TreeNodeDef> _neighbours = new List<TreeNodeDef>();

        public TreeNodeDef(string id, TreeNodeType type, double x, double y, string nameRu, string nameEn, IReadOnlyList<Modifier> modifiers,
            bool locked, string descriptionRu = "", string descriptionEn = "")
        {
            Id = id;
            Type = type;
            X = x;
            Y = y;
            NameRu = nameRu;
            NameEn = nameEn;
            Modifiers = modifiers;
            Locked = locked;
            DescriptionRu = descriptionRu;
            DescriptionEn = descriptionEn;
        }

        public string Id { get; }
        public TreeNodeType Type { get; }
        public double X { get; }
        public double Y { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string DescriptionRu { get; }
        public string DescriptionEn { get; }
        public bool HasDescription => DescriptionRu.Length > 0 || DescriptionEn.Length > 0;
        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool Locked { get; }
        public IReadOnlyList<TreeNodeDef> Neighbours => _neighbours;

        internal void Connect(TreeNodeDef other) => _neighbours.Add(other);
    }
}
