using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Items;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ElementSetTests
    {
        private static ItemCatalog Items => TestWorld.Real.Items;

        private static readonly (Element Element, string Armor, string Jewelry)[] Lines =
        {
            (Element.Fire, "ember_mantle", "ember_jewels"),
            (Element.Air, "gale_mantle", "gale_jewels"),
            (Element.Earth, "stone_mantle", "stone_jewels"),
            (Element.Lightning, "storm_mantle", "storm_jewels"),
            (Element.Water, "tide_mantle", "tide_jewels"),
            (Element.Corrosion, "rust_mantle", "rust_jewels")
        };

        [Test]
        public void EveryElementHasAnArmourLineAJewelryLineAndAConsonance()
        {
            foreach (var (element, armor, jewelry) in Lines)
            {
                Assert.That(Items.Sets.Count(s => s.Theme == armor), Is.EqualTo(6), armor);
                Assert.That(Items.Sets.Count(s => s.Theme == jewelry), Is.EqualTo(6), jewelry);
                Assert.That(Items.SetOf(jewelry, ItemRarity.Rare)!.Jewelry, Is.True);
                Assert.That(Items.SetOf(armor, ItemRarity.Rare)!.Jewelry, Is.False);
                Assert.That(Items.Consonances.Any(c => c.Armor == armor && c.Jewelry == jewelry), Is.True, armor);
                var stats = Items.SetOf(armor, ItemRarity.Rare)!.PieceLines(ItemSlot.Weapon).Select(line => line.Stat).ToList();
                Assert.That(stats, Does.Contain(ElementRules.Conversion[(int)element]).And.Contain(ElementRules.Damage[(int)element]));
            }
        }

        [Test]
        public void TheArmourLineFitsTheWeaponsOfTheClassesOfItsElement()
        {
            foreach (var (element, armor, _) in Lines)
            {
                var set = Items.SetOf(armor, ItemRarity.Epic)!;
                var types = TestWorld.Real.Weapons.Types.Where(type => TestWorld.Real.ClassTreeOf(type.HeroClass.ToString().ToLowerInvariant())!.Element == (int)element).ToList();
                Assert.That(types.Count, Is.EqualTo(3), armor);
                foreach (var type in types)
                    Assert.That(set.AllowsType(type.Id), Is.True, armor + " " + type.Id);
            }
        }

        [Test]
        public void TheLinesGrowByHalfAStepPerRarity()
        {
            var rare = Items.SetOf("storm_mantle", ItemRarity.Rare)!.PieceLines(ItemSlot.Weapon)[0].Value;
            var rift = Items.SetOf("storm_mantle", ItemRarity.Rift)!.PieceLines(ItemSlot.Weapon)[0].Value;
            Assert.That(rift / rare, Is.EqualTo(System.Math.Pow(1.5, 5)).Within(0.01));
        }
    }
}
