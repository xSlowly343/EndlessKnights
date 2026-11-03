using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ArmorTierTests
    {
        private static ItemCatalog Catalog => TestWorld.Data.Items;
        private static readonly ItemSlot[] Armor = { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots };

        [TestCase(ItemRarity.Uncommon, ItemRarity.Simple)]
        [TestCase(ItemRarity.Rare, ItemRarity.Rare)]
        [TestCase(ItemRarity.Epic, ItemRarity.Epic)]
        public void AnAffixWaitsForTheRarityOfItsTier(ItemRarity rarity, ItemRarity highestTier)
        {
            var rng = new Rng(5);
            foreach (var slot in Armor)
            {
                for (var i = 0; i < 200; i++)
                {
                    foreach (var affix in Catalog.Roll(slot, "red", rarity, 6, rng).Affixes)
                        Assert.That(affix.Def.FromRarity, Is.LessThanOrEqualTo(highestTier), affix.Def.Id);
                }
            }
        }

        [Test]
        public void EverySlotHasThreeTiersOfAffixes()
        {
            foreach (var slot in Armor)
            {
                var pool = Catalog.Affixes.Where(affix => affix.FitsSlot(slot)).ToList();
                Assert.That(pool.Any(affix => affix.FromRarity == ItemRarity.Simple), Is.True, slot.ToString());
                Assert.That(pool.Any(affix => affix.FromRarity == ItemRarity.Rare), Is.True, slot.ToString());
                Assert.That(pool.Any(affix => affix.FromRarity == ItemRarity.Epic), Is.True, slot.ToString());
            }
        }

        [TestCase(ItemSlot.Helmet, "auraSkillLevel", ItemRarity.Epic)]
        [TestCase(ItemSlot.Gloves, "buttonSkillLevel", ItemRarity.Epic)]
        [TestCase(ItemSlot.Boots, "autoSkillLevel", ItemRarity.Epic)]
        [TestCase(ItemSlot.Amulet, "classSkillLevel", ItemRarity.Legendary)]
        public void EachSlotHasItsOwnSkillLevelAffix(ItemSlot slot, string stat, ItemRarity from)
        {
            var levels = Catalog.Affixes.Where(affix => affix.FitsSlot(slot) && affix.Group == "slot_level").ToList();

            Assert.That(levels.Count, Is.EqualTo(1));
            Assert.That(levels[0].Stats.Single().Stat.Name, Is.EqualTo(stat));
            Assert.That(levels[0].FromRarity, Is.EqualTo(from));
            Assert.That(levels[0].Slots, Is.EqualTo(new[] { slot }));
        }

        [Test]
        public void ArmorRollsOneBaseImplicitFromItsSlotPoolAndJewelleryNone()
        {
            var rng = new Rng(9);
            foreach (var slot in Armor)
            {
                var seen = Enumerable.Range(0, 100).Select(_ => Catalog.Roll(slot, "blue", ItemRarity.Rare, 3, rng).RolledImplicit!).ToList();
                Assert.That(seen.All(rolled => rolled.Slot == slot), Is.True, slot.ToString());
                Assert.That(seen.Select(rolled => rolled.Id).Distinct().Count(), Is.EqualTo(Catalog.ImplicitsOf(slot).Count), slot.ToString());
            }
            Assert.That(Catalog.Roll(ItemSlot.Ring, null, ItemRarity.Rare, 3, rng).RolledImplicit, Is.Null);
        }

        [Test]
        public void TheRolledImplicitWorksAndSurvivesASave()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            Assert.That(Catalog.TryGetImplicit("helmet_base_aura_effect", out var rolled), Is.True);
            Catalog.TryGetBase("cap", out var cap);
            var helmet = new GearItem(cap, ItemRarity.Rare, 3, System.Array.Empty<GearAffix>(), null, rolled);
            loop.State.Drops.Gear.Add(helmet);
            var before = loop.Hero.Value(CombatStats.AuraEffect, 1);

            Assert.That(loop.Gear.Equip(helmet), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1), Is.GreaterThan(before));

            var restored = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;
            Assert.That(restored.Gear.Worn.Single(item => item != null)!.RolledImplicit, Is.SameAs(rolled));
        }

        [Test]
        public void ImplicitPoolsAreCheckedOnLoad()
        {
            var json = TestData.ReadGameDataFile(ItemCatalog.FileName);
            var twice = json.Replace("\"id\": \"chest_base_strike_crit\"", "\"id\": \"chest_base_strike_damage\"");
            var weapon = json.Replace("\"implicitPools\": {\n    \"helmet\":", "\"implicitPools\": {\n    \"weapon\":");

            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(twice))!.Message, Does.Contain("used twice"));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(weapon))!.Message, Does.Contain("unknown gear slot"));
        }

        private static GameLoop LevelHero(StatId stat, double value)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(stat, ModifierKind.Flat, value));
            var loop = TestWorld.Loop(state: TestWorld.WithAllSkillsOpen(new GameState { Class = HeroClass.Runedancer, RecordStage = 300 }), hero: hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.Skills.Equip(TestWorld.Data.Gems["runedancer_hit_1"], role: SkillSlotRole.Left);
            loop.Skills.Equip(TestWorld.Data.Gems["runedancer_auto_1"], role: SkillSlotRole.Auto);
            loop.Skills.Equip(TestWorld.Data.Gems["runedancer_aura_1"], role: SkillSlotRole.Aura);
            loop.State.OwnedGems.Add(TestWorld.Data.Gems["through_stride"]);
            loop.Skills.Equip(TestWorld.Data.Gems["through_stride"], role: SkillSlotRole.Right);
            return loop;
        }

        private static int Bonus(GameLoop loop, string id) => loop.Skills.LevelBonus(TestWorld.Data.Gems[id]);

        [Test]
        public void ButtonLevelRaisesOnlyTheButtonSkills()
        {
            var loop = LevelHero(CombatStats.ButtonSkillLevel, 2);

            Assert.That(Bonus(loop, "runedancer_hit_1"), Is.EqualTo(2));
            Assert.That(Bonus(loop, "through_stride"), Is.EqualTo(2));
            Assert.That(Bonus(loop, "runedancer_auto_1"), Is.EqualTo(0));
            Assert.That(Bonus(loop, "runedancer_aura_1"), Is.EqualTo(0));
        }

        [Test]
        public void AutoLevelRaisesOnlyTheAutoSkills()
        {
            var loop = LevelHero(CombatStats.AutoSkillLevel, 2);

            Assert.That(Bonus(loop, "runedancer_auto_1"), Is.EqualTo(2));
            Assert.That(Bonus(loop, "runedancer_hit_1"), Is.EqualTo(0));
        }

        [Test]
        public void ClassLevelRaisesClassSkillsButNotGeneralOnes()
        {
            var loop = LevelHero(CombatStats.ClassSkillLevel, 1);

            Assert.That(Bonus(loop, "runedancer_hit_1"), Is.EqualTo(1));
            Assert.That(Bonus(loop, "runedancer_aura_1"), Is.EqualTo(1));
            Assert.That(Bonus(loop, "through_stride"), Is.EqualTo(0));
            Assert.That(loop.Skills.LevelPower(TestWorld.Data.Gems["runedancer_hit_1"]),
                Is.EqualTo(TestWorld.Data.Gems.Levels.EffectMultiplier(1, 1) * loop.Skills.ColorPower(TestWorld.Data.Gems["runedancer_hit_1"])).Within(1e-9));
        }
    }
}
