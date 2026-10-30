using System.Collections.Generic;
using System.Globalization;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class HeroSummary
    {
        public static readonly IReadOnlyList<string> ArmorSlots = new[] { "helmet", "armor", "gloves", "boots" };
        public static readonly IReadOnlyList<string> JewelrySlots = new[] { "amulet", "ring", "ring" };

        public static string ClassName(HeroClass heroClass) => Texts.Get("class." + HeroClassNames.Of(heroClass));

        public static string ClassName(GameState state) => Texts.Get("class." + state.ClassName);

        public static string SlotName(string slot) => Texts.Get("hero.slot." + slot);

        public static string JewelryTitle => Texts.Get("hero.jewelry");

        public static string Title(GameState state) => Texts.Format("hero.title", ClassName(state), state.HeroLevel);

        public static string Progress(GameState state) => Texts.Format("hero.progress", state.Stage, System.Math.Max(state.RecordStage, state.HighestStage));

        public static string Attributes(GameLoop loop) =>
            string.Format(CultureInfo.InvariantCulture, Texts.Get("hero.attributes"),
                Whole(loop.Book.Attribute("strength")), Whole(loop.Book.Attribute("dexterity")), Whole(loop.Book.Attribute("intelligence")));

        public static HeroHand Hand(GameLoop loop, WeaponHand hand)
        {
            var label = Texts.Get(hand == WeaponHand.Right ? "hero.hand.right" : "hero.hand.left");
            var weapon = loop.Weapons.In(hand);
            return weapon == null
                ? new HeroHand(label, Texts.Get("hero.empty"), string.Empty, null)
                : new HeroHand(label, Texts.Pick(weapon.NameRu, weapon.NameEn), Texts.Get("rarity.simple"), weapon.Type.Color);
        }

        public static IReadOnlyList<HeroStatTile> Tiles(GameLoop loop, NumberFormatSettings format)
        {
            var state = loop.State;
            var keyHit = loop.Combat.SkillDamage(loop.Combat.BaseKeyDamage, 1, StatTags.Keyboard);
            var crit = loop.Combat.CritChance(StatTags.Keyboard);
            var critMultiplier = loop.Combat.CritMultiplier(StatTags.Keyboard);
            var goldBonus = loop.Hero.Value(CombatStats.Gold, 1) - 1;
            return new[]
            {
                new HeroStatTile(Texts.Get("hero.stat.keyHit"), NumberFormatter.Format(keyHit, format)),
                new HeroStatTile(Texts.Get("hero.stat.dps"), NumberFormatter.Format(state.RecentDps, format), HeroStatAccent.Gold),
                new HeroStatTile(Texts.Get("hero.stat.crit"), Invariant("{0:0}% ×{1:0.#}", crit * 100, critMultiplier)),
                new HeroStatTile(Texts.Get("hero.stat.focus"), Invariant("×{0:0.0} / {1:0.#}", state.Focus, loop.Focus.Max), HeroStatAccent.Focus),
                new HeroStatTile(Texts.Get("hero.stat.resource"), Invariant("{0:0} / {1:0}", state.Resource, loop.Data.Combat.Resources.Max), HeroStatAccent.Resource),
                new HeroStatTile(Texts.Get("hero.stat.gold"), Invariant("{0:+0;-0;0}%", goldBonus * 100), HeroStatAccent.Gold),
            };
        }

        public static string? Hint(GameState state)
        {
            var points = state.Tree.PointsLeft;
            return points > 0 ? Texts.Format("hero.hint.treePoints", points) : null;
        }

        public static string TreeAction => Texts.Get("hero.hint.toTree");

        public static string Preset(GameState state) => Texts.Format("hero.preset", state.ActivePreset + 1);

        private static long Whole(double value) => (long)System.Math.Floor(value);

        private static string Invariant(string format, double first, double second = 0) => string.Format(CultureInfo.InvariantCulture, format, first, second);
    }
}
