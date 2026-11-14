using System.Collections.Generic;
using Game.Core.Input;
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
        public void ThePhoenixHelpsAgainstTheColossusAndTheSerpentStrengthensRoleBuffs()
        {
            var loop = Member(1);
            loop.SetPartyMascot("phoenix", 1);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.02).Within(1e-12));
            var buffs = loop.Hero.Value(CombatStats.RoleBuffEffect, 1);
            loop.SetPartyMascot("serpent", 100);
            Assert.That(loop.Hero.Value(CombatStats.RoleBuffEffect, 1) / buffs, Is.EqualTo(1.4).Within(1e-9), "aura +40% to role buffs");
            loop.StartPartyMascotCall();
            Assert.That(loop.Hero.Value(CombatStats.RoleBuffEffect, 1) / buffs, Is.EqualTo(2.8).Within(1e-9), "the call doubles role buffs");
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

        [Test]
        public void TheFeedSendsTheItemsItPicksAndTakesWhatTheServerAte()
        {
            var loop = Member();
            loop.State.Camp.Bag["fish_hills_roach"] = 2;
            loop.State.Camp.Bag["fish_ice_crucian"] = 3;
            loop.State.Cooking.Meals.Add("fish_soup");
            loop.State.Cooking.Meals.Add("fish_soup");
            Assert.That(loop.PickMascotFood("fish", 4), Is.EqualTo(new Dictionary<string, int> { ["fish_hills_roach"] = 2, ["fish_ice_crucian"] = 2 }));
            Assert.That(loop.PickMascotFood("dish", 5), Is.EqualTo(new Dictionary<string, int> { ["dish:fish_soup"] = 2 }));
            Assert.That(loop.TakeMascotItems(new Dictionary<string, int> { ["fish_ice_crucian"] = 4 }), Is.False, "more than the bag holds takes nothing");
            Assert.That(loop.State.Camp.Bag["fish_ice_crucian"], Is.EqualTo(3));
            Assert.That(loop.TakeMascotItems(new Dictionary<string, int> { ["fish_ice_crucian"] = 1, ["dish:fish_soup"] = 1 }), Is.True);
            Assert.That(loop.State.Camp.Bag["fish_ice_crucian"], Is.EqualTo(2));
            Assert.That(loop.State.Cooking.Meals.Count, Is.EqualTo(1));
        }

        [Test]
        public void ThreeBranchesOfFiveRitesAPointEveryFiveLevels()
        {
            Assert.That(Rules.RiteBranches.Count, Is.EqualTo(3));
            Assert.That(Rules.Rites.Count, Is.EqualTo(15));
            Assert.That(Rules.RitePoints(4), Is.Zero);
            Assert.That(Rules.RitePoints(100), Is.EqualTo(20));
            Assert.That(Rules.RiteMaxRank, Is.EqualTo(3));
            Assert.That(Rules.Trials.Count, Is.EqualTo(5));
            Assert.That(Rules.CallGauge, Is.EqualTo(1500));
            Assert.That(Rules.CallActions(10, 2, 250), Is.EqualTo(14.5).Within(1e-12));
            Assert.That(Rules.Gift(100), Is.EqualTo(0.2).Within(1e-12));
        }

        [Test]
        public void RitesRaiseTheAuraColossusDamageAndThePeacefulGift()
        {
            var loop = Member(1);
            loop.SetPartyMascot("wolf", 50);
            var aura = loop.PartyGrowth.MascotAura;
            var peaceful = loop.PartyGrowth.Peaceful;
            Assert.That(peaceful, Is.EqualTo(Rules.Gift(50)).Within(1e-12), "the peaceful gift grows with the level");
            loop.SetPartyMascot("wolf", 50, new Dictionary<string, int> { ["power_aura"] = 2, ["hunt_fang"] = 3, ["home_gift"] = 1 });
            Assert.That(loop.PartyGrowth.MascotAura, Is.EqualTo(aura * 1.1).Within(1e-12));
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.15).Within(1e-12));
            Assert.That(loop.PartyGrowth.Peaceful, Is.EqualTo(peaceful + 0.03).Within(1e-12));
        }

        [Test]
        public void TheCallLastsThirtySecondsAndTheTreeAndRitesStretchAndStrengthenIt()
        {
            var loop = Member(1);
            Assert.That(loop.StartPartyMascotCall(), Is.False, "no beast, no call");
            loop.SetPartyMascot("boar", 10);
            var strike = loop.Hero.Value(CombatStats.Damage, 1, StatTags.Strike);
            Assert.That(loop.StartPartyMascotCall(), Is.True);
            Assert.That(loop.State.MascotCallTicks, Is.EqualTo(300));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Strike) / strike, Is.EqualTo(2).Within(1e-9), "the boar doubles key hits");
            for (var i = 0; i < 300; i++)
                loop.Tick();
            Assert.That(loop.MascotCalling, Is.False);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Strike), Is.EqualTo(strike).Within(1e-9));
            loop.SetPartyTree(new Dictionary<int, int> { [45] = 3 }, new string[0]);
            loop.SetPartyMascot("boar", 10, new Dictionary<string, int> { ["power_length"] = 1, ["power_call"] = 1 });
            loop.StartPartyMascotCall();
            Assert.That(loop.State.MascotCallTicks, Is.EqualTo((30 + 5 + 3) * 10));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Strike) / strike, Is.EqualTo(2.2).Within(1e-9), "Call Fury +20%");
        }

        [Test]
        public void TheBearStopsTheBossTimerAndTheMammothSwitchesOffRegeneration()
        {
            var loop = Member(1);
            loop.SetPartyMascot("bear", 10);
            loop.State.SpawnEnemy(null, Game.Core.Content.EnemyType.Boss, Game.Core.Math.BigNumber.FromDouble(1e300));
            loop.State.BossTicksLeft = 300;
            loop.StartPartyMascotCall();
            for (var i = 0; i < 50; i++)
                loop.Tick();
            Assert.That(loop.State.BossTicksLeft, Is.EqualTo(300), "the boss timer stands still");
            var mammoth = Member(1);
            mammoth.SetPartyMascot("mammoth", 10);
            mammoth.StartPartyMascotCall();
            Assert.That(mammoth.Hero.Value(new StatId("enemyRegen"), 0.05), Is.Zero);
        }

        [Test]
        public void ThePhoenixDoublesColossusPointsDuringTheCallAndTheStormEyeWorksOnlyThen()
        {
            var loop = Member(1);
            loop.SetPartyMascot("phoenix", 1);
            loop.SetPartyRelics(new Dictionary<string, int> { ["storm_eye"] = 30 });
            loop.Tick();
            var damage = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.ColossusCallFactor, Is.EqualTo(1));
            loop.StartPartyMascotCall();
            loop.Tick();
            Assert.That(loop.ColossusCallFactor, Is.EqualTo(2).Within(1e-12));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / damage, Is.EqualTo(System.Math.Pow(1.01, 30)).Within(1e-9));
            for (var i = 0; i < 300; i++)
                loop.Tick();
            Assert.That(loop.ColossusCallFactor, Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(damage).Within(damage * 1e-9));
        }

        [Test]
        public void ActionsOfThePlayerAreCountedForTheCallGauge()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.SetPartyStanding(true, Now - 10 * Day, 2, Now);
            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(3);
                loop.Tick();
            }
            Assert.That(loop.MascotActions, Is.Zero, "no beast, no gauge");
            loop.SetPartyMascot("wolf", 5);
            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(3);
                loop.Tick();
            }
            var taken = loop.TakeMascotActions();
            Assert.That(taken, Is.GreaterThan(0).And.LessThanOrEqualTo(30), "effective presses: spam does not fill the gauge faster");
            Assert.That(loop.MascotActions, Is.Zero);
        }

        [Test]
        public void TheMascotWalksBehindChargesOnTheCallStandsAtBossesAndSleepsWhenIdle()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Tick();
            Assert.That(loop.MascotStrip.Pose, Is.EqualTo(MascotPose.Hidden));
            loop.SetPartyStanding(true, Now - 10 * Day, 2, Now);
            loop.SetPartyMascot("wolf", 5);
            loop.MascotStrip.SetTemper("playful");
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.MascotStrip.Pose, Is.EqualTo(MascotPose.Walk));
            Assert.That(loop.MascotStrip.Place, Is.EqualTo(MascotPlace.Behind));
            Assert.That(loop.MascotStrip.Stage, Is.EqualTo("cub"));
            loop.StartPartyMascotCall();
            loop.Tick();
            Assert.That(loop.MascotStrip.Pose, Is.EqualTo(MascotPose.Charge));
            Assert.That(loop.MascotStrip.Place, Is.EqualTo(MascotPlace.Front));
            for (var i = 0; i < 300; i++)
                loop.Tick();
            Assert.That(loop.MascotStrip.Place, Is.EqualTo(MascotPlace.Behind));
            for (var i = 0; i < 1200; i++)
                loop.Tick();
            Assert.That(loop.MascotStrip.Pose, Is.EqualTo(MascotPose.Sleep));
        }

        [Test]
        public void CalmBeastsShowGesturesOfTheirKindAndTemper()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.SetPartyStanding(true, Now - 10 * Day, 2, Now);
            loop.SetPartyMascot("wolf", 5);
            loop.MascotStrip.SetTemper("lazy");
            var seen = new HashSet<string>();
            for (var i = 0; i < 20000; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                if (loop.MascotStrip.Gesture.Length > 0)
                    seen.Add(loop.MascotStrip.Gesture);
            }
            Assert.That(seen, Is.Not.Empty);
            var allowed = new HashSet<string>(Rules.Beast("wolf")!.Gestures);
            allowed.UnionWith(Rules.TemperGestures("lazy"));
            Assert.That(allowed.IsSupersetOf(seen), Is.True);
        }
    }
}
