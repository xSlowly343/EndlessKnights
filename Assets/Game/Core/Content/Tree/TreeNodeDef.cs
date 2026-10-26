using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeNodeDef
    {
        private readonly List<TreeNodeDef> _neighbours = new List<TreeNodeDef>();

        public TreeNodeDef(string id, TreeNodeType type, double x, double y, string nameRu, string nameEn, IReadOnlyList<Modifier> modifiers,
            IReadOnlyList<IReadOnlyList<Modifier>> options, double attributeAmount, string? startClass, bool locked,
            string scope = CommonScope, string descriptionRu = "", string descriptionEn = "")
        {
            Id = id;
            Type = type;
            X = x;
            Y = y;
            NameRu = nameRu;
            NameEn = nameEn;
            Modifiers = modifiers;
            Options = options;
            AttributeAmount = attributeAmount;
            StartClass = startClass;
            Locked = locked;
            Scope = scope;
            ScopeClass = scope.StartsWith(ClassScopePrefix, System.StringComparison.Ordinal) ? scope.Substring(ClassScopePrefix.Length) : null;
            DescriptionRu = descriptionRu;
            DescriptionEn = descriptionEn;
        }

        public const string CommonScope = "common";
        public const string ClassScopePrefix = "class:";

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
        public IReadOnlyList<IReadOnlyList<Modifier>> Options { get; }
        public double AttributeAmount { get; }
        public string? StartClass { get; }
        public bool Locked { get; }
        public string Scope { get; }
        public string? ScopeClass { get; }
        public IReadOnlyList<TreeNodeDef> Neighbours => _neighbours;

        internal void Connect(TreeNodeDef other) => _neighbours.Add(other);
    }
}
