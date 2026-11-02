using System;
using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponItem
    {
        public WeaponItem(WeaponBase weaponBase, ItemRarity rarity, UniqueWeaponDef? unique = null, SetDef? set = null)
        {
            Set = set;
            Base = weaponBase;
            Rarity = rarity;
            Unique = unique;
        }

        public WeaponBase Base { get; }
        public UniqueWeaponDef? Unique { get; }
        public SetDef? Set { get; }
        public WeaponType? SecondType => Unique?.SecondType;
        public ItemRarity Rarity { get; }
        public string Id => Base.Id;
        public WeaponType Type => Base.Type;
        public string NameRu => Base.NameRu;
        public string NameEn => Base.NameEn;
        public IReadOnlyList<Modifier> Innate => Base.Innate;

        public WeaponItem Rebased(WeaponBase weaponBase) => new WeaponItem(weaponBase, Rarity, Unique, Set);

        public WeaponItem InSet(SetDef set) => new WeaponItem(Base, Rarity, Unique, set);

        public bool Counts(string typeId) =>
            string.Equals(Type.Id, typeId, StringComparison.Ordinal) || (SecondType != null && string.Equals(SecondType.Id, typeId, StringComparison.Ordinal));

    }
}
