using System;
using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponItem
    {
        public WeaponItem(WeaponBase weaponBase, ItemRarity rarity, IReadOnlyList<string> skills)
        {
            Base = weaponBase;
            Rarity = rarity;
            Skills = skills;
        }

        public WeaponBase Base { get; }
        public ItemRarity Rarity { get; }
        public IReadOnlyList<string> Skills { get; }
        public string Id => Base.Id;
        public WeaponType Type => Base.Type;
        public string NameRu => Base.NameRu;
        public string NameEn => Base.NameEn;
        public IReadOnlyList<Modifier> Innate => Base.Innate;

        public WeaponItem Rebased(WeaponBase weaponBase) => new WeaponItem(weaponBase, Rarity, Skills);

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
