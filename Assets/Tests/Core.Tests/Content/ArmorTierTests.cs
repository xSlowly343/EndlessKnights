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
        [TestCase(ItemRarity.Mythic, ItemRarity.Rare)]
        [TestCase(ItemRarity.Ancient, ItemRarity.Ancient)]
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
                Assert.That(pool.Any(affix => affix.FromRarity == ItemRarity.Ancient), Is.True, slot.ToString());
            }
        }

        [TestCase(ItemSlot.Helmet, "auraSkillLevel", ItemRarity.Ancient)]
        [TestCase(ItemSlot.Gloves, "buttonSkillLevel", ItemRarity.Ancient)]
        [TestCase(ItemSlot.Boots, "autoSkillLevel", ItemRarity.Ancient)]
        [TestCase(ItemSlot.Amulet, "classSkillLevel", ItemRarity.Ancient)]
        public void EachSlotHasItsOwnSkillLevelAffix(ItemSlot slot, string stat, ItemRarity from)
        {
            var levels = Catalog.Affixes.Where(affix => affix.FitsSlot(slot) && affix.Group == "slot_level").ToList();

            Assert.That(levels.Count, Is.EqualTo(1));
            Assert.That(levels[0].Stats.Count, Is.EqualTo(2), "an ultimate affix has two lines");
            Assert.That(levels[0].Stats[0].Stat.Name, Is.EqualTo(stat));
            Assert.That(levels[0].FromRarity, Is.EqualTo(from));
            Assert.That(levels[0].Slots, Is.EqualTo(new[] { slot }));
        }

        [Test]
        public void TiersOpenWithAnyRarityWithTheFourthOrWithTheEighth()
        {
            foreach (var affix in Catalog.Affixes)
                Assert.That(affix.FromRarity, Is.AnyOf(ItemRarity.Simple, ItemRarity.Rare, ItemRarity.Ancient), affix.Id);
            Assert.That((int)ItemRarity.Rare, Is.EqualTo(3), "the fourth rarity");
            Assert.That((int)ItemRarity.Ancient, Is.EqualTo(7), "the eighth rarity");
        }

        [Test]
        public void EveryUltimateAffixGivesTwoLinesAndARuleKeepsItsPriceFixed()
        {
            var ultimate = Catalog.Affixes.Where(affix => affix.FromRarity == ItemRarity.Ancient).ToList();
            Assert.That(ultimate, Is.Not.Empty);
            Assert.That(ultimate.Where(affix => affix.BuiltInRole != null).All(affix => affix.Stats.Count == 1 && affix.Stats[0].OwnTiers), Is.True, "a built-in support plus one line");
            foreach (var def in ultimate.Where(affix => affix.BuiltInRole == null))
            {
                Assert.That(def.Stats.Count, Is.GreaterThanOrEqualTo(2), def.Id);
                Assert.That(def.Stats[1].OwnTiers, Is.True, def.Id);
                var low = new System.Collections.Generic.List<Modifier>();
                var high = new System.Collections.Generic.List<Modifier>();
                new GearAffix(def, 1, def.ValueAt(1, Catalog.Tiers)).AddModifiers(low);
                new GearAffix(def, Catalog.Tiers, def.ValueAt(Catalog.Tiers, Catalog.Tiers)).AddModifiers(high);
                Assert.That(low.Count, Is.EqualTo(def.Stats.Count), def.Id);
                Assert.That(high[1].Value, Is.EqualTo(def.Stats[1].TierLast).Within(1e-9), def.Id);
                if (def.Group == "ultimate_rule")
                {
                    Assert.That(high[1].Value, Is.EqualTo(low[1].Value).Within(1e-9), def.Id + ": the price of a rule stays fixed");
                    if (def.TierOne != def.TierLast)
                        Assert.That(System.Math.Abs(high[0].Value), Is.GreaterThan(System.Math.Abs(low[0].Value)), def.Id);
                }
                else
                {
                    Assert.That(System.Math.Abs(high[1].Value), Is.GreaterThan(System.Math.Abs(low[1].Value)), def.Id);
                }
            }
        }

        [Test]
        public void AnUltimateAffixWithOneLineOrAnOddRarityIsRejected()
        {
            var json = TestData.ReadGameDataFile(ItemCatalog.FileName);
            var single = json.Replace(", {\"stat\": \"auraEffect\", \"kind\": \"increased\", \"tiers\": [0.08, 0.26], \"ranges\": [[0.08, 0.12], [0.13, 0.17], [0.18, 0.25], [0.26, 0.38]]}", "");
            var epic = json.Replace("\"id\": \"armor_dot_damage\", \"slots\": [\"helmet\", \"chest\", \"gloves\", \"boots\"], \"part\": \"prefix\", \"stats\": [{\"stat\": \"dotDamage\", \"kind\": \"increased\"}], \"tiers\": [0.05, 0.35], \"ranges\": [[0.05, 0.11], [0.12, 0.22], [0.23, 0.34], [0.35, 0.53]], \"fromRarity\": \"rare\"",
                "\"id\": \"armor_dot_damage\", \"slots\": [\"helmet\", \"chest\", \"gloves\", \"boots\"], \"part\": \"prefix\", \"stats\": [{\"stat\": \"dotDamage\", \"kind\": \"increased\"}], \"tiers\": [0.05, 0.35], \"ranges\": [[0.05, 0.11], [0.12, 0.22], [0.23, 0.34], [0.35, 0.53]], \"fromRarity\": \"epic\"");

            Assert.That(json, Does.Contain(", {\"stat\": \"auraEffect\", \"kind\": \"increased\", \"tiers\": [0.08, 0.26], \"ranges\": [[0.08, 0.12], [0.13, 0.17], [0.18, 0.25], [0.26, 0.38]]}"));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(single))!.Message, Does.Contain("two lines"));
            Assert.That(epic, Is.Not.EqualTo(json));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(epic))!.Message, Does.Contain("ancient"));
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
