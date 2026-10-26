using System.Globalization;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class MasterTexts
    {
        public static string Place => Texts.Get("master.place");
        public static string Remove => Texts.Get("master.remove");
        public static string NoWeapon => Texts.Get("master.noWeapon");
        public static string InBook => Texts.Get("master.inBook");

        public static string Hand(WeaponItem weapon) => Texts.Format("master.hand", ItemTexts.Name(weapon), ItemTexts.Rarity(weapon.Rarity));

        public static string Tier(SkillTier tier) => Texts.Get("master.tier." + tier.ToString().ToLowerInvariant());

        public static string Line(SkillDef skill)
        {
            var line = Kind(skill);
            if (skill.ResourceCost > 0)
                line += " · " + Number(skill.ResourceCost);
            if (skill.CooldownSeconds > 0)
                line += " · " + Texts.Format("master.cooldown", Number(skill.CooldownSeconds));
            return line;
        }

        public static string Kind(SkillDef skill)
        {
            if (skill.Role == SkillRole.Fast)
                return Texts.Get("master.kind.fast");
            if (skill.Role == SkillRole.Heavy)
                return Texts.Get("master.kind.heavy");
            if (skill.Effect is AuraEffect aura)
                return aura.Reserves ? Texts.Format("master.kind.reserve", Number(aura.Reserve)) : Texts.Get("master.kind.aura");
            return skill.IsPassive ? Texts.Get("master.kind.passive") : Texts.Get("master.kind.skill");
        }

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
