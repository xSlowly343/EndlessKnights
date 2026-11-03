using System.Collections.Generic;
using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ItemTexts
    {
        public static readonly IReadOnlyList<ItemFilter> Filters = new[] { ItemFilter.All, ItemFilter.Weapons, ItemFilter.Armor, ItemFilter.Jewelry };

        public static string Filter(ItemFilter filter) => Texts.Get("items.filter." + filter.ToString().ToLowerInvariant());

        public static string Empty(ItemFilter filter) => Texts.Get(filter == ItemFilter.All || filter == ItemFilter.Weapons ? "items.empty" : "items.emptyKind");

        public static string Name(WeaponItem weapon) => Texts.Pick(weapon.NameRu, weapon.NameEn);

        public static string Initial(WeaponItem weapon)
        {
            var type = Texts.Pick(weapon.Type.NameRu, weapon.Type.NameEn);
            return type.Length > 0 ? type.Substring(0, 1) : "?";
        }

        public static ItemRarity RarityOf(WeaponItem weapon) => weapon.Rarity;

        public static string Rarity(ItemRarity rarity) => Texts.Get("rarity." + rarity.ToString().ToLowerInvariant());

        public static string Subtitle(WeaponItem weapon) =>
            string.Join(" · ", Texts.Pick(weapon.Type.NameRu, weapon.Type.NameEn), HeroSummary.ClassName(weapon.Type.HeroClass), Rarity(RarityOf(weapon)).ToLowerInvariant());

        public static string Innate(WeaponItem weapon) =>
            string.Join(" · ", weapon.Type.Innate.Concat(weapon.Innate).Select(ModifierText.Describe));

        public static string InHands(GameState state) => Texts.Format("items.inHand", HandName(state.Weapons.Held));

        public static string Equip => Texts.Get("items.equip");

        public static string Affixes(WeaponItem weapon, int? locked = null) => Affixes(weapon.Affixes, locked);

        public static string Affixes(IReadOnlyList<GearAffix> affixes, int? locked = null) =>
            string.Join(" · ", affixes.Select((affix, index) => index == locked ? Texts.Format("items.reroll.lockedLine", Affix(affix)) : Affix(affix)));

        public static string Offer(RerollOffer offer) =>
            string.Join(" · ", offer.After.Select((affix, index) => index == offer.Locked ? Texts.Format("items.reroll.lockedLine", Affix(affix)) : Change(offer.Before[index], affix)));

        public static string Reroll(RerollResult check, double cost, double dust) => check switch
        {
            RerollResult.Unique => Texts.Get("items.reroll.unique"),
            RerollResult.NothingToRoll => Texts.Get("items.reroll.nothing"),
            RerollResult.NotEnoughDust => Texts.Format("items.reroll.noDust", Whole(cost), Whole(dust)),
            _ => Texts.Format("items.reroll", Whole(cost))
        };

        public static string Lock(int? locked) =>
            locked == null ? Texts.Get("items.reroll.lockNone") : Texts.Format("items.reroll.lock", locked.Value + 1);

        public static string KeepOld => Texts.Get("items.reroll.keep");

        public static string TakeNew => Texts.Get("items.reroll.take");

        private static string Whole(double value) => System.Math.Floor(value + 1e-9).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        private static string Affix(GearAffix affix) => affix.Support != null
            ? Texts.Format("items.builtIn", Texts.Get(affix.Def.BuiltInRole == SkillSlotRole.Left ? "items.builtIn.left" : "items.builtIn.right"), Texts.Pick(affix.Support.NameRu, affix.Support.NameEn))
            : string.Join(" · ", AffixLines(affix));

        private static IEnumerable<string> AffixLines(GearAffix affix)
        {
            var lines = new List<Modifier>();
            affix.AddModifiers(lines);
            return lines.Select((line, index) => WithRange(line, affix.Def.StatRange(index, affix.Tier)));
        }

        private static string WithRange(Modifier line, TierRange range) =>
            range.Spreads
                ? Texts.Format("items.range", ModifierText.Describe(line), ModifierText.Bare(line, range.Low), ModifierText.Bare(line, range.High))
                : ModifierText.Describe(line);

        private static string Change(GearAffix before, GearAffix after)
        {
            if (after.Support != null)
                return Affix(after);
            var old = new List<Modifier>();
            var fresh = new List<Modifier>();
            before.AddModifiers(old);
            after.AddModifiers(fresh);
            return string.Join(" · ", fresh.Select((line, index) => System.Math.Abs(line.Value - old[index].Value) < 1e-12
                ? ModifierText.Describe(line)
                : Texts.Format("items.reroll.change", ModifierText.Describe(line), ModifierText.Bare(old[index], old[index].Value))));
        }

        public static IReadOnlyList<WeaponItem> Bag(GameState state, ItemFilter filter) =>
            filter == ItemFilter.All || filter == ItemFilter.Weapons ? state.Weapons.Bag : System.Array.Empty<WeaponItem>();

        private static string HandName(WeaponItem? weapon) => weapon != null ? Name(weapon) : Texts.Get("hero.empty");
    }
}
