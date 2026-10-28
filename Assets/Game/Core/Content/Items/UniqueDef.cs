using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class UniqueDef
    {
        public UniqueDef(string id, string nameRu, string nameEn, GearBase gearBase, ItemRarity rarity, IReadOnlyList<Modifier> lines, string ruleRu, string ruleEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Base = gearBase;
            Rarity = rarity;
            Lines = lines;
            RuleRu = ruleRu;
            RuleEn = ruleEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public GearBase Base { get; }
        public ItemSlot Slot => Base.Slot;
        public ItemRarity Rarity { get; }
        public IReadOnlyList<Modifier> Lines { get; }
        public string RuleRu { get; }
        public string RuleEn { get; }
    }
}
