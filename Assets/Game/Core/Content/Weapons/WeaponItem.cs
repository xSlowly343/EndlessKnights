using System;
using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponItem
    {
        public WeaponItem(WeaponBase weaponBase, ItemRarity rarity, int itemLevel = 1, IReadOnlyList<GearAffix>? affixes = null, UniqueWeaponDef? unique = null, SetDef? set = null, ForgeMark? forge = null)
        {
            Forge = forge ?? new ForgeMark();
            Set = set;
            Base = weaponBase;
            Rarity = rarity;
            ItemLevel = itemLevel;
            Affixes = affixes ?? Array.Empty<GearAffix>();
            Unique = unique;
        }

        public WeaponBase Base { get; }
        public UniqueWeaponDef? Unique { get; }
        public SetDef? Set { get; }
        public ItemRarity Rarity { get; }
        public int Quality { get; set; }
        public int ItemLevel { get; }
        public IReadOnlyList<GearAffix> Affixes { get; }
        public ForgeMark Forge { get; }
        public string Id => Base.Id;
        public WeaponType Type => Base.Type;
        public string NameRu => Base.NameRu;
        public string NameEn => Base.NameEn;
        public IReadOnlyList<Modifier> Innate => Base.Innate;
        public IReadOnlyList<string> OpensSkills => Unique?.OpensSkills ?? Array.Empty<string>();

        public WeaponItem Rebased(WeaponBase weaponBase) => new WeaponItem(weaponBase, Rarity, ItemLevel, Affixes, Unique, Set, Forge) { Quality = Quality };

        public WeaponItem WithAffixes(IReadOnlyList<GearAffix> affixes) => new WeaponItem(Base, Rarity, ItemLevel, affixes, Unique, Set, Forge) { Quality = Quality };

        public WeaponItem InSet(SetDef set) => new WeaponItem(Base, Rarity, ItemLevel, Affixes, Unique, set, Forge) { Quality = Quality };

        public bool Opens(string skillId)
        {
            var opens = OpensSkills;
            for (var i = 0; i < opens.Count; i++)
            {
                if (string.Equals(opens[i], skillId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public Gems.GemDef? BuiltInSupport(Sim.SkillSlotRole role)
        {
            for (var i = 0; i < Affixes.Count; i++)
            {
                var affix = Affixes[i];
                if (affix.Support != null && affix.Def.BuiltInRole == role)
                    return affix.Support;
            }
            return null;
        }
    }
}
