using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Json;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Weapons
{
    public class WeaponSkillTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));
        private static readonly string[] KatanaStarter =
        {
            "thin_cut", "hundred_cuts", "no_rush", "kitchen_rhythm", "what_remains_of_the_debt", "sweep_over_the_queue", "slow_simmer"
        };

        private static WeaponCatalog Catalog => TestWorld.Real.Weapons;

        private static GameLoop Hero(HeroClass heroClass) =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { Class = heroClass }, new StatSheet());

        private static WeaponBase Base(string id)
        {
            Catalog.TryGetBase(id, out var weapon);
            return weapon;
        }

        private static WeaponCatalog WithMythicRoninSkills()
        {
            var gems = GemCatalog.Load(TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"tier\": \"rare\"", "\"tier\": \"mythic\""));
            var catalog = WeaponCatalog.Load(TestData.ReadGameDataFile(WeaponCatalog.FileName));
            catalog.BindGems(gems);
            return catalog;
        }

        private static IEnumerable<IReadOnlyList<string>> Rolls(WeaponCatalog catalog, ItemRarity rarity, int count)
        {
            var katana = catalog.TypeOf(HeroClass.Ronin);
            for (var seed = 1; seed <= count; seed++)
                yield return catalog.RollSkills(katana, rarity, new Rng((ulong)seed));
        }

        private static SkillTier TierOf(string id) => TestWorld.Real.Gems[id].Skill!.Tier;

        [Test]
        public void StarterKatanaHoldsTheRoninsSevenSkillsInPlaceOrder()
        {
            var loop = Hero(HeroClass.Ronin);
            var katana = loop.Weapons.In(WeaponHand.Right)!;

            Assert.That(katana.Rarity, Is.EqualTo(ItemRarity.Simple));
            Assert.That(katana.Skills, Is.EqualTo(KatanaStarter));
            Assert.That(TestWorld.Real.Gems["thin_cut"].Skill!.Role, Is.EqualTo(SkillRole.Fast));
            Assert.That(TestWorld.Real.Gems["hundred_cuts"].Skill!.Role, Is.EqualTo(SkillRole.Heavy));
        }

        [Test]
        public void PlacesOpenWithHeroLevelOneOneEightFifteenTwentyFiveThirtyFiveFifty()
        {
            Assert.That(Catalog.SkillRules.UnlockLevels, Is.EqualTo(new[] { 1, 1, 8, 15, 25, 35, 50 }));
            var loop = Hero(HeroClass.Ronin);
            foreach (var id in KatanaStarter)
                loop.Skills.Equip(TestWorld.Real.Gems[id]);

            Assert.That(Enumerable.Range(0, 7).Select(loop.Skills.Armed), Is.EqualTo(new[] { true, true, false, false, false, false, false }));

            loop.State.HeroLevel = 8;
            Assert.That(loop.Skills.Armed(2), Is.True);
            Assert.That(loop.Skills.Armed(3), Is.False);

            loop.State.HeroLevel = 49;
            Assert.That(loop.Skills.Armed(6), Is.False);
            loop.State.HeroLevel = 50;
            Assert.That(loop.Skills.Armed(6), Is.True);
        }

        [Test]
        public void SkillOfTheRightTypeButNotOnTheWeaponStaysDark()
        {
            var loop = Hero(HeroClass.Ronin);
            loop.State.HeroLevel = 50;
            var katana = loop.Weapons.In(WeaponHand.Right)!;
            loop.Weapons.Unequip(WeaponHand.Right);
            var shortKatana = loop.Weapons.Give(new WeaponItem(katana.Base, ItemRarity.Rare, new[] { "thin_cut", "slow_simmer" }));
            loop.Weapons.Equip(WeaponHand.Right, shortKatana);
            loop.Skills.Equip(TestWorld.Real.Gems["slow_simmer"]);
            loop.Skills.Equip(TestWorld.Real.Gems["no_rush"]);

            Assert.That(loop.Skills.Armed(0), Is.True);
            Assert.That(loop.Skills.Armed(1), Is.False);

            loop.Weapons.Equip(WeaponHand.Left, katana);
            Assert.That(loop.Skills.Armed(1), Is.True);
        }

        [Test]
        public void PathSkillsStillNeedOnlyTheWeaponType()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Skills.Equip(TestWorld.Real.Gems["second_axe"]);

            Assert.That(loop.Skills.Armed(0), Is.True);
            loop.Weapons.Unequip(WeaponHand.Right);
            Assert.That(loop.Skills.Armed(0), Is.False);
        }

        [TestCase(ItemRarity.Simple)]
        [TestCase(ItemRarity.Rare)]
        [TestCase(ItemRarity.Mythic)]
        [TestCase(ItemRarity.Primordial)]
        public void RollPutsAFastSkillFirstAHeavySecondAndNeverRepeats(ItemRarity rarity)
        {
            foreach (var skills in Rolls(Catalog, rarity, 200))
            {
                Assert.That(skills.Distinct().Count(), Is.EqualTo(skills.Count));
                Assert.That(skills.Count, Is.InRange(2, 7));
                Assert.That(TestWorld.Real.Gems[skills[0]].Skill!.Role, Is.EqualTo(SkillRole.Fast));
                Assert.That(skills.All(id => TestWorld.Real.Gems[id].Skill!.Weapon == "katana"), Is.True);
            }
        }

        [Test]
        public void SimpleWeaponsGetOnlyCommonAndRareSkills()
        {
            var tiers = Rolls(Catalog, ItemRarity.Simple, 300).SelectMany(skills => skills).Select(TierOf).Distinct().ToList();

            Assert.That(tiers, Is.EquivalentTo(new[] { SkillTier.Common, SkillTier.Rare }));
        }

        [Test]
        public void EpicSkillsStartFromTheFourthRarity()
        {
            var uncommon = Rolls(Catalog, ItemRarity.Uncommon, 300).SelectMany(skills => skills).Select(TierOf);
            var rare = Rolls(Catalog, ItemRarity.Rare, 300).SelectMany(skills => skills).Select(TierOf);

            Assert.That(uncommon, Has.None.EqualTo(SkillTier.Epic));
            Assert.That(rare, Has.Some.EqualTo(SkillTier.Epic));
        }

        [Test]
        public void HigherRarityRollsStrongerSkills()
        {
            double Average(ItemRarity rarity) => Rolls(Catalog, rarity, 400).SelectMany(skills => skills).Average(id => (int)TierOf(id));

            Assert.That(Average(ItemRarity.Legendary), Is.GreaterThan(Average(ItemRarity.Fine)));
            Assert.That(Average(ItemRarity.Primordial), Is.GreaterThan(Average(ItemRarity.Legendary)));
        }

        [Test]
        public void MythicSkillsOnlyOnAstralAndPrimordialAtMostTwo()
        {
            var catalog = WithMythicRoninSkills();
            int Mythic(IReadOnlyList<string> skills) => skills.Count(id => catalog.PoolOf(catalog.TypeOf(HeroClass.Ronin)).First(gem => gem.Id == id).Skill!.Tier == SkillTier.Mythic);

            Assert.That(Rolls(catalog, ItemRarity.Rift, 300).Select(Mythic), Has.All.EqualTo(0));
            Assert.That(Rolls(catalog, ItemRarity.Astral, 300).Select(Mythic), Has.All.InRange(0, 2));
            Assert.That(Rolls(catalog, ItemRarity.Astral, 300).Select(Mythic), Has.Some.GreaterThan(0));
            Assert.That(Rolls(catalog, ItemRarity.Primordial, 300).Select(Mythic), Has.All.InRange(1, 2));
        }

        [Test]
        public void SameSeedRollsTheSameSkills()
        {
            var katana = Catalog.TypeOf(HeroClass.Ronin);

            Assert.That(Catalog.RollSkills(katana, ItemRarity.Ancient, new Rng(42)), Is.EqualTo(Catalog.RollSkills(katana, ItemRarity.Ancient, new Rng(42))));
        }

        [Test]
        public void SaveKeepsRarityAndRolledSkills()
        {
            var loop = Hero(HeroClass.Ronin);
            var rolled = loop.Weapons.Give(Base("shogun_blade"), ItemRarity.Ancient, new Rng(7));
            loop.Weapons.Equip(WeaponHand.Left, rolled);

            var text = SaveCodec.Encode(loop.State, Clock);
            var restored = SaveCodec.Decode(text, TestWorld.Real).State;

            Assert.That(SaveCodec.Encode(restored, Clock), Is.EqualTo(text));
            Assert.That(restored.Weapons.Left!.Rarity, Is.EqualTo(ItemRarity.Ancient));
            Assert.That(restored.Weapons.Left.Skills, Is.EqualTo(rolled.Skills));
            Assert.That(restored.Weapons.Right!.Skills, Is.EqualTo(KatanaStarter));
        }

        [Test]
        public void OldSaveWeaponsBecomeSimpleWithTheStarterSkills()
        {
            var loop = Hero(HeroClass.Viking);
            var payload = JsonParser.Parse(SaveCodec.Encode(loop.State, Clock), "save").Fields["payload"];
            var oldWeapons = JsonParser.Parse("{\"starterGiven\": true, \"right\": \"jarl_axe\", \"left\": null, \"bag\": [\"wanderer_katana\"]}", "old");
            var edited = JsonEdit.Update(payload, "state", state => JsonEdit.Set(state, "weapons", oldWeapons));
            var state = GameStateJson.Read(new ContentNode(edited, "save", "payload").Field("state"), TestWorld.Real.Enemies, TestWorld.Real.Gems, Catalog);

            Assert.That(state.Weapons.Right!.Id, Is.EqualTo("jarl_axe"));
            Assert.That(state.Weapons.Right.Rarity, Is.EqualTo(ItemRarity.Simple));
            Assert.That(state.Weapons.Right.Skills, Is.EqualTo(Catalog.TypeOf(HeroClass.Viking).StarterSkills));
            Assert.That(state.Weapons.Bag[0].Skills, Is.EqualTo(KatanaStarter));
        }

        [Test]
        public void StarterSkillOfAnotherTypeIsRejected()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName).Replace("\"starterSkills\": [\"thin_cut\"", "\"starterSkills\": [\"iron_rim_bash\"");
            var catalog = WeaponCatalog.Load(json);

            var error = Assert.Throws<ContentException>(() => catalog.BindGems(TestWorld.Real.Gems))!;

            Assert.That(error.Message, Does.Contain("iron_rim_bash"));
        }

        [Test]
        public void TierOnACommonSkillIsRejected()
        {
            var json = TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"id\": \"through_stride\",", "\"id\": \"through_stride\", \"tier\": \"rare\",");

            Assert.Throws<ContentException>(() => GemCatalog.Load(json));
        }

        [Test]
        public void EveryRarityNeedsSkillTierWeights()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName).Replace("      \"rift\": [5, 25, 40, 30, 0],", string.Empty);

            Assert.Throws<ContentException>(() => WeaponCatalog.Load(json));
        }
    }
}
