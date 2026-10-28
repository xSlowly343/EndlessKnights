using System;
using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponItem
    {
        public WeaponItem(WeaponBase weaponBase, ItemRarity rarity, IReadOnlyList<string> skills, UniqueWeaponDef? unique = null, SetDef? set = null)
        {
            Set = set;
            Base = weaponBase;
            Rarity = rarity;
            Skills = skills;
            Unique = unique;
        }

        public WeaponBase Base { get; }
        public UniqueWeaponDef? Unique { get; }
        public SetDef? Set { get; }
        public WeaponType? SecondType => Unique?.SecondType;
        public ItemRarity Rarity { get; }
        public IReadOnlyList<string> Skills { get; }
        public string Id => Base.Id;
        public WeaponType Type => Base.Type;
        public string NameRu => Base.NameRu;
        public string NameEn => Base.NameEn;
        public IReadOnlyList<Modifier> Innate => Base.Innate;

        public WeaponItem Rebased(WeaponBase weaponBase) => new WeaponItem(weaponBase, Rarity, Skills, Unique, Set);

        public WeaponItem InSet(SetDef set) => new WeaponItem(Base, Rarity, Skills, Unique, set);

        public bool Counts(string typeId) =>
            string.Equals(Type.Id, typeId, StringComparison.Ordinal) || (SecondType != null && string.Equals(SecondType.Id, typeId, StringComparison.Ordinal));

        public bool Opens(string skillId, int heroLevel, IReadOnlyList<int> unlockLevels)
        {
            for (var place = 0; place < Skills.Count; place++)
            {
                if (string.Equals(Skills[place], skillId, StringComparison.Ordinal))
                    return place >= unlockLevels.Count || heroLevel >= unlockLevels[place];
            }
            return false;
        }
    }
}
