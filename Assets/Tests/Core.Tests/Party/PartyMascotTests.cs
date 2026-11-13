using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyMascotTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyMascotRules Rules => TestWorld.Data.Party.Growth.Mascot;

        private static GameLoop Member(int members = 4)
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, members, Now);
            return loop;
        }

        [Test]
        public void TwelveBeastsSixStagesAndTheirOwnFavouriteFood()
        {
            Assert.That(Rules.Beasts.Count, Is.EqualTo(12));
            Assert.That(Rules.Stages.Count, Is.EqualTo(6));
            Assert.That(Rules.StageOf(1).Id, Is.EqualTo("cub"));
            Assert.That(Rules.StageOf(14).Id, Is.EqualTo("cub"));
            Assert.That(Rules.StageOf(15).Id, Is.EqualTo("youngling"));
            Assert.That(Rules.StageOf(70).Id, Is.EqualTo("leader"));
            Assert.That(Rules.StageOf(90).Id, Is.EqualTo("ancient"));
            Assert.That(Rules.StageOf(80, 10).Id, Is.EqualTo("ancient"));
            Assert.That(Rules.BranchOpen(69), Is.False);
            Assert.That(Rules.BranchOpen(70), Is.True);
            foreach (var beast in Rules.Beasts)
            {
                Assert.That(Rules.Food(beast.Favourite), Is.Not.Null, beast.Id);
                Assert.That(beast.Branches.Count, Is.EqualTo(2), beast.Id);
                Assert.That(beast.Aura.Count, Is.GreaterThan(0), beast.Id);
            }
            Assert.That(Rules.FoodOf("fish_hills_roach")!.Id, Is.EqualTo("fish"));
            Assert.That(Rules.FoodOf("ore_ice")!.Id, Is.EqualTo("ore"));
            Assert.That(Rules.FoodOf("stone_wall"), Is.Null);
            Assert.That(Rules.Tempers.Count, Is.EqualTo(6));
        }

        [Test]
        public void LevelsNeedMoreExperienceEachTime()
        {
            Assert.That(Rules.Need(1), Is.EqualTo(500));
            Assert.That(Rules.Need(2), Is.EqualTo(590));
            Assert.That(Rules.LevelOf(499).Level, Is.EqualTo(1));
            Assert.That(Rules.LevelOf(500).Level, Is.EqualTo(2));
            Assert.That(Rules.LevelOf(1e9).Level, Is.EqualTo(100));
            Assert.That(Rules.HeartsOf(250), Is.EqualTo(2));
            Assert.That(Rules.HeartsOf(5000), Is.EqualTo(10));
        }

        [Test]
        public void TheAuraGrowsFromTwoToFortyPercentAndWithTheParty()
        {
            Assert.That(Rules.AuraAt(1, 1), Is.EqualTo(0.02).Within(1e-12));
            Assert.That(Rules.AuraAt(100, 1), Is.EqualTo(0.4).Within(1e-12));
            Assert.That(Rules.AuraAt(100, 4), Is.EqualTo(0.64).Within(1e-12));
            Assert.That(Rules.AuraAt(100, 2), Is.EqualTo(0.48).Within(1e-12));
            Assert.That(Rules.AuraAt(0, 4), Is.Zero);
        }

        [Test]
        public void TheBearAuraGoesToBossDamageInThePartyBasket()
        {
            var loop = Member();
            var boss = new StatId("damageVsBoss");
            var before = loop.Hero.Value(boss, 1);
            loop.SetPartyMascot("bear", 100);
            Assert.That(loop.PartyGrowth.MascotAura, Is.EqualTo(0.64).Within(1e-12));
            Assert.That(loop.Hero.Value(boss, 1) - before, Is.EqualTo(0.64 * before).Within(1e-9));
            Assert.That(loop.PartyGrowth.MascotStage!.Id, Is.EqualTo("ancient"));
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.Hero.Value(boss, 1) - before, Is.EqualTo(0.64 * 0.3 * before).Within(1e-9), "a newcomer gets a share");
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.Hero.Value(boss, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void ThePhoenixHelpsAgainstTheColossusAndTheSerpentWaitsForReactions()
        {
            var loop = Member(1);
            loop.SetPartyMascot("phoenix", 1);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.02).Within(1e-12));
            loop.SetPartyMascot("serpent", 50);
            Assert.That(loop.PartyGrowth.Mascot, Is.Empty);
        }

        [Test]
        public void ThePartyTreeRaisesTheAuraAndBringsAncientEarlier()
        {
            var loop = Member(1);
            loop.SetPartyMascot("wolf", 80);
            var plain = loop.PartyGrowth.MascotAura;
            Assert.That(loop.PartyGrowth.MascotStage!.Id, Is.EqualTo("leader"));
            loop.SetPartyTree(new Dictionary<int, int> { [5] = 3, [45] = 5 }, new string[0]);
            Assert.That(loop.PartyGrowth.MascotAura, Is.EqualTo(plain * 1.03).Within(1e-12));
            Assert.That(loop.PartyGrowth.MascotStage!.Id, Is.EqualTo("ancient"));
            Assert.That(loop.Hero.Value(PartyGrowthLayer.MascotAuraStat, 0), Is.Zero, "tree lines about the mascot stay out of the hero's sheet");
        }

        [Test]
        public void FoodIsTakenFromTheCampByKind()
        {
            var loop = Member();
            loop.State.Camp.Bag["fish_hills_roach"] = 2;
            loop.State.Camp.Bag["fish_ice_crucian"] = 3;
            loop.State.Camp.Bag["herb_windwort"] = 4;
            loop.State.Cooking.Meals.Add("fish_soup");
            Assert.That(loop.MascotFoodCount("fish"), Is.EqualTo(5));
            Assert.That(loop.MascotFoodCount("dish"), Is.EqualTo(1));
            Assert.That(loop.TakeMascotFood("fish", 4), Is.EqualTo(4));
            Assert.That(loop.MascotFoodCount("fish"), Is.EqualTo(1));
            Assert.That(loop.TakeMascotFood("fish", 4), Is.EqualTo(1), "only what is there");
            Assert.That(loop.State.Camp.Bag.ContainsKey("herb_windwort"), Is.True);
            Assert.That(loop.TakeMascotFood("dish", 2), Is.EqualTo(1));
            Assert.That(loop.TakeMascotFood("cake", 1), Is.Zero);
        }
    }
}
