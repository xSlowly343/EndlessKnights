using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class UniqueWeaponDef
    {
        public UniqueWeaponDef(string id, string nameRu, string nameEn, WeaponBase weaponBase, WeaponType? secondType, ItemRarity rarity,
            IReadOnlyList<Modifier> lines, string ruleRu, string ruleEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Base = weaponBase;
            SecondType = secondType;
            Rarity = rarity;
            Lines = lines;
            RuleRu = ruleRu;
            RuleEn = ruleEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public WeaponBase Base { get; }
        public WeaponType? SecondType { get; }
        public ItemRarity Rarity { get; }
        public IReadOnlyList<Modifier> Lines { get; }
        public string RuleRu { get; }
        public string RuleEn { get; }
    }
}
