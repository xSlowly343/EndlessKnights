using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class TreeTemplate
    {
        public TreeTemplate(string id, TreeNodeType type, string theme, string nameRu, string nameEn, string descriptionRu, string descriptionEn,
            IReadOnlyList<Modifier> modifiers, IReadOnlyList<IReadOnlyList<Modifier>> options, double attributeAmount, IReadOnlyList<string> roles, string? heroClass)
        {
            Id = id;
            Type = type;
            Theme = theme;
            NameRu = nameRu;
            NameEn = nameEn;
            DescriptionRu = descriptionRu;
            DescriptionEn = descriptionEn;
            Modifiers = modifiers;
            Options = options;
            AttributeAmount = attributeAmount;
            Roles = roles;
            HeroClass = heroClass;
        }

        public string Id { get; }
        public TreeNodeType Type { get; }
        public string Theme { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string DescriptionRu { get; }
        public string DescriptionEn { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public IReadOnlyList<IReadOnlyList<Modifier>> Options { get; }
        public double AttributeAmount { get; }
        public IReadOnlyList<string> Roles { get; }
        public string? HeroClass { get; }
        public bool IsClassOwn => HeroClass != null;

        public TreeNodeDef Place(string id, double x, double y, bool locked)
        {
            var scope = HeroClass == null ? TreeNodeDef.CommonScope : TreeNodeDef.ClassScopePrefix + HeroClass;
            var startClass = Type == TreeNodeType.Start ? HeroClass : null;
            return new TreeNodeDef(id, Type, x, y, NameRu, NameEn, Modifiers, Options, AttributeAmount, startClass, locked, scope, DescriptionRu, DescriptionEn);
        }
    }
}
