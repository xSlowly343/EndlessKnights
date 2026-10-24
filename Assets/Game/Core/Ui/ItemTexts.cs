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

        public static string Name(WeaponBase weapon) => Texts.Pick(weapon.NameRu, weapon.NameEn);

        public static string Initial(WeaponBase weapon)
        {
            var type = Texts.Pick(weapon.Type.NameRu, weapon.Type.NameEn);
            return type.Length > 0 ? type.Substring(0, 1) : "?";
        }

        public static ItemRarity RarityOf(WeaponBase weapon) => ItemRarity.Simple;

        public static string Rarity(ItemRarity rarity) => Texts.Get("rarity." + rarity.ToString().ToLowerInvariant());

        public static string Subtitle(WeaponBase weapon) =>
            string.Join(" · ", Texts.Pick(weapon.Type.NameRu, weapon.Type.NameEn), HeroSummary.ClassName(weapon.Type.HeroClass), Rarity(RarityOf(weapon)).ToLowerInvariant());

        public static string Innate(WeaponBase weapon) =>
            string.Join(" · ", weapon.Type.Innate.Concat(weapon.Innate).Select(ModifierText.Describe));

        public static string InHands(GameState state) =>
            Texts.Format("items.inHands", HandName(state.Weapons.Right), HandName(state.Weapons.Left));

        public static string LightUp(WeaponSwap swap) =>
            swap.LightUp.Count == 0 ? string.Empty : Texts.Format("items.lightUp", string.Join(", ", swap.LightUp));

        public static string GoOut(WeaponSwap swap) =>
            swap.GoOut.Count == 0 ? string.Empty : Texts.Format("items.goOut", swap.Replaced != null ? Name(swap.Replaced) : string.Empty, string.Join(", ", swap.GoOut));

        public static string EquipTo(WeaponHand hand) => Texts.Get(hand == WeaponHand.Right ? "items.toRight" : "items.toLeft");

        public static IReadOnlyList<WeaponBase> Bag(GameState state, ItemFilter filter) =>
            filter == ItemFilter.All || filter == ItemFilter.Weapons ? state.Weapons.Bag : System.Array.Empty<WeaponBase>();

        private static string HandName(WeaponBase? weapon) => weapon != null ? Name(weapon) : Texts.Get("hero.empty");
    }
}
