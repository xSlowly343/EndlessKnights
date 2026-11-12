using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MenagerieTreeTests
    {
        private static GameLoop Camp()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        private static void GivePoints(GameLoop loop, int level) => loop.State.Companions.MenagerieLevel = level;

        [Test]
        public void TheTreeHasFiftyNodesInSevenRowsLikeTheHeroTree()
        {
            var rules = TestWorld.Data.Companions.Menagerie;
            Assert.That(rules.Nodes.Count, Is.EqualTo(50));
            int[] perRow = { 10, 9, 8, 7, 6, 5, 5 };
            for (var row = 1; row <= 7; row++)
            {
                var count = 0;
                foreach (var node in rules.Nodes)
                    count += node.Row == row ? 1 : 0;
                Assert.That(count, Is.EqualTo(perRow[row - 1]), $"row {row}");
            }
            Assert.That(rules.MaxPoints, Is.EqualTo(180));
            Assert.That(rules.Majors.Count, Is.EqualTo(4));
        }

        [Test]
        public void PointsComeFromTheMenagerieLevelEvolutionsAndTheAlbumAndAreSeparateFromTheHero()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            Assert.That(tree.Points, Is.EqualTo(1));
            loop.Companions.Progress("camp_dog").Stage = 2;
            Assert.That(tree.Points, Is.EqualTo(5));
            loop.Companions.Gain("camp_dog", 1000);
            Assert.That(tree.Level, Is.GreaterThan(1), "a tenth of companion experience fills the menagerie level");
            GivePoints(loop, 500);
            Assert.That(tree.Points, Is.EqualTo(180));
        }

        [Test]
        public void RowsOpenByPointsInLowerRowsAndNodesNeedTheirParent()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            GivePoints(loop, 40);
            Assert.That(tree.Learn(11), Is.EqualTo(TreeResult.RowLocked));
            for (var i = 0; i < 5; i++)
                Assert.That(tree.Learn(2), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Learn(2), Is.EqualTo(TreeResult.MaxRank));
            Assert.That(tree.Learn(11), Is.EqualTo(TreeResult.NeedsParent));
            Assert.That(tree.Learn(1), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Learn(11), Is.EqualTo(TreeResult.Done));
            loop.Companions.AddSouls(100);
            Assert.That(tree.Refund(1), Is.EqualTo(TreeResult.Holds), "the child still stands on it");
            Assert.That(tree.Refund(2), Is.EqualTo(TreeResult.Done), "five points stay below row 2");
            Assert.That(tree.Refund(2), Is.EqualTo(TreeResult.Holds), "row 2 needs five points below");
            Assert.That(tree.Refund(11), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Spent, Is.EqualTo(5));
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(90));
        }

        [Test]
        public void NodeLevelsFollowTheHeroRule()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            GivePoints(loop, 40);
            tree.Learn(2);
            Assert.That(tree.Stat(MenagerieStat.TripItems), Is.EqualTo(0.02).Within(1e-9));
            tree.Learn(2);
            Assert.That(tree.Stat(MenagerieStat.TripItems), Is.EqualTo(0.04).Within(1e-9));
            tree.Learn(2);
            Assert.That(tree.Stat(MenagerieStat.TripShards), Is.EqualTo(0.02).Within(1e-9));
            tree.Learn(2);
            Assert.That(tree.Stat(MenagerieStat.TripItems), Is.EqualTo(0.06).Within(1e-9));
        }

        [Test]
        public void TreeStatsReachTheCompanions()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            GivePoints(loop, 40);
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            var trip = loop.State.Expeditions.Active[0];
            var before = loop.Companions.StatOf(trip, TravelStat.Items);
            var food = loop.Companions.FoodLeft("camp_crow");
            tree.Learn(2);
            tree.Learn(4);
            tree.Learn(4);
            tree.Learn(4);
            Assert.That(loop.Companions.StatOf(trip, TravelStat.Items), Is.EqualTo(before * 1.02).Within(1e-9));
            Assert.That(loop.Companions.FoodLeft("camp_crow"), Is.EqualTo(food + 10).Within(1e-9));
        }

        [Test]
        public void TheUndersideWhisperGivesSoulsOnceAWeek()
        {
            var loop = Camp();
            GivePoints(loop, 40);
            for (var i = 0; i < 5; i++)
                loop.Menagerie.Learn(9);
            Assert.That(loop.Menagerie.Stat(MenagerieStat.WeeklySouls), Is.EqualTo(5));

            loop.SetLocalDay(102);
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(5));
            loop.SetLocalDay(103);
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(5), "the same week");
            loop.SetLocalDay(109);
            Assert.That(loop.State.Companions.Souls, Is.EqualTo(10));
        }

        [Test]
        public void TheTreeAddsAPetADayAndCheapensFormsAndTraits()
        {
            var loop = Camp();
            GivePoints(loop, 60);
            var pets = TestWorld.Data.Companions.Bond.PetsPerDay;
            var form = loop.Companions.FormChangeSouls;
            for (var i = 0; i < 3; i++)
                loop.Menagerie.Learn(7);
            for (var node = 1; node <= 3; node++)
                for (var i = 0; i < 5; i++)
                    loop.Menagerie.Learn(node);
            loop.Menagerie.Learn(9);
            loop.Menagerie.Learn(19);
            Assert.That(loop.Menagerie.Learn(27), Is.EqualTo(TreeResult.Done));

            Assert.That(loop.Menagerie.Stat(MenagerieStat.PetsPerDay), Is.EqualTo(1));
            Assert.That(loop.Menagerie.Stat(MenagerieStat.FormCost), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(loop.Companions.FormChangeSouls, Is.EqualTo((long)System.Math.Ceiling(form * 0.95 - 1e-9)));
            var done = 0;
            for (var i = 0; i < pets + 3; i++)
            {
                loop.State.Tick += TestWorld.Data.Companions.Bond.PetEveryTicks;
                done += loop.Companions.Pet("camp_dog") == CompanionResult.Done ? 1 : 0;
            }
            Assert.That(done, Is.EqualTo(pets + 1));
        }

        private static GameLoop FullTree(params string[] majors)
        {
            var loop = Camp();
            loop.State.RecordStage = 60;
            for (var node = 1; node <= TestWorld.Data.Companions.Menagerie.Nodes.Count; node++)
                loop.State.Companions.TreeRanks[node] = MenagerieTreeRules.MaxRank;
            foreach (var major in majors)
                loop.State.Companions.TreeMajors[int.Parse(major.Substring("menagerie_m".Length, major.LastIndexOf('_') - "menagerie_m".Length))] = major;
            loop.Menagerie.Recalculate();
            loop.Menagerie.Changed();
            return loop;
        }

        private static void GiveHearts(GameLoop loop, string companion, int hearts) =>
            loop.Companions.Progress(companion).Bond = TestWorld.Data.Companions.Bond.PointsTo(hearts);

        [Test]
        public void TheLinesOfHeartsLettersSparksAndThePackCarryTheNumbersOfTheDesign()
        {
            var tree = FullTree("menagerie_m90_3", "menagerie_m140_3").Menagerie;

            Assert.That(tree.Stat(MenagerieStat.FavouriteFood), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.HeartsExperience), Is.EqualTo(0.06).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.HeartsTraits), Is.EqualTo(0.01).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.DevotedTraits), Is.EqualTo(0.06).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.DevotedExperience), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.DevotedStarCost), Is.EqualTo(0.02).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.MentorShare), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.ChoiceLetters), Is.EqualTo(0.05 + 0.09).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.LetterBond), Is.EqualTo(1));
            Assert.That(tree.Stat(MenagerieStat.Sparks), Is.EqualTo(0.02 + 0.06).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.RepeatSpark), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.TripSpark), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.PerCompanion), Is.EqualTo(0.002).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.PackLoot), Is.EqualTo(0.09).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.PackFamily), Is.EqualTo(0.03).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.BurrowTries), Is.EqualTo(1));
        }

        [Test]
        public void HeartsStrengthenTraitsAndCheapenStarsOnlyForCompanionsThatHaveThem()
        {
            var plain = Camp();
            var loop = FullTree();
            var rules = TestWorld.Data.Companions;
            var own = 1 + loop.Menagerie.Stat(MenagerieStat.OwnTraits);
            foreach (var world in new[] { plain, loop })
            {
                GiveHearts(world, "camp_dog", rules.Bond.MaxHearts);
                GiveHearts(world, "camp_crow", rules.Menagerie.HeartsForTraits);
            }

            Assert.That(loop.Companions.TraitFactor("camp_dog") / plain.Companions.TraitFactor("camp_dog"), Is.EqualTo(own * 1.07).Within(1e-9));
            Assert.That(loop.Companions.TraitFactor("camp_crow") / plain.Companions.TraitFactor("camp_crow"), Is.EqualTo(own * 1.01).Within(1e-9));
            var cut = loop.Menagerie.Stat(MenagerieStat.StarCost);
            Assert.That(loop.Companions.Devoted("camp_dog"), Is.True);
            Assert.That(loop.Companions.StarPrice("camp_crow", 1000), Is.EqualTo((int)System.Math.Ceiling(1000 * (1 - cut) - 1e-9)));
            Assert.That(loop.Companions.StarPrice("camp_dog", 1000), Is.EqualTo((int)System.Math.Ceiling(1000 * (1 - cut - 0.02) - 1e-9)));
        }

        [Test]
        public void AnAnsweredLetterBondsAndTripsStrikeSparks()
        {
            var loop = FullTree();
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            var trip = loop.State.Expeditions.Active[0];
            trip.Choice = true;
            var bond = loop.Companions.Progress("camp_dog").Bond;

            Assert.That(loop.Expeditions.Answer(0, true), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Companions.Progress("camp_dog").Bond, Is.GreaterThanOrEqualTo(bond + 1));

            var plain = Camp();
            plain.Expeditions.Send("camp_dog", "green_hills", 8);
            foreach (var world in new[] { loop, plain })
            {
                var done = world.State.Expeditions.Active[0];
                done.Elapsed = done.Planned;
                for (var i = 0; i < 600; i++)
                    world.Companions.TripDone(done);
            }
            Assert.That(loop.State.Companions.Sparks, Is.InRange(6, 40), "about three trips in a hundred");
            Assert.That(plain.State.Companions.Sparks, Is.Zero);
        }

        [Test]
        public void TheBurrowOpensTwiceAWeekAndEveryCollectedCompanionAddsToTheLoot()
        {
            var loop = FullTree("menagerie_m140_3");
            var party = new[] { "camp_dog" };
            Assert.That(loop.CompanionWork.BurrowTries, Is.EqualTo(2));
            Assert.That(loop.CompanionWork.Burrow(party, out _), Is.EqualTo(WorkResult.Done));
            Assert.That(loop.CompanionWork.Burrow(party, out _), Is.EqualTo(WorkResult.Done));
            Assert.That(loop.CompanionWork.Burrow(party, out _), Is.EqualTo(WorkResult.TooSoon));
            loop.SetLocalDay(100 + 7);
            Assert.That(loop.CompanionWork.BurrowReady, Is.True);

            var plain = Camp();
            Assert.That(plain.CompanionWork.Burrow(party, out _), Is.EqualTo(WorkResult.Done));
            Assert.That(plain.CompanionWork.Burrow(party, out _), Is.EqualTo(WorkResult.TooSoon));

            var without = FullTree();
            var trip = new Expedition { Companion = "camp_dog", Biome = "green_hills" };
            var collected = loop.Companions.Collected;
            Assert.That(collected, Is.GreaterThan(0));
            var tree = loop.Menagerie;
            var baseLoot = 1 + tree.Stat(MenagerieStat.TripItems) + tree.Stat(MenagerieStat.TripLoot);
            Assert.That(loop.Companions.StatOf(trip, TravelStat.Items) / without.Companions.StatOf(trip, TravelStat.Items),
                Is.EqualTo((baseLoot + 0.002 * collected) / baseLoot).Within(1e-9));
        }

        [Test]
        public void ThePackAndTheMentorGainFromTheTree()
        {
            var loop = FullTree("menagerie_m90_3");
            var plain = Camp();
            plain.State.RecordStage = 60;
            foreach (var world in new[] { loop, plain })
            {
                world.Companions.Grant("egg_boarlet");
                world.Companions.Grant("hills_piglet");
                world.Companions.Progress("camp_dog").Stars = 1;
            }
            Assert.That(loop.Companions.TakeStudent("camp_dog", "camp_crow"), Is.EqualTo(CompanionResult.Done));
            Assert.That(plain.Companions.TakeStudent("camp_dog", "camp_crow"), Is.EqualTo(CompanionResult.Done));
            var pack = new Expedition { Companion = "camp_dog", Partner = "egg_boarlet", Third = "hills_piglet", Biome = "green_hills" };
            var rules = TestWorld.Data.Companions.Pack;
            Assert.That(loop.Companions.OneFamily(pack), Is.True);

            double Sum(GameLoop world) => world.Companions.TravelMultiplier("camp_dog", TravelStat.Shards) + world.Companions.TravelMultiplier("egg_boarlet", TravelStat.Shards)
                + world.Companions.TravelMultiplier("hills_piglet", TravelStat.Shards) - 2;
            var tree = loop.Menagerie;
            var treeLoot = 1 + tree.Stat(MenagerieStat.TripShards) + tree.Stat(MenagerieStat.TripLoot);
            var expected = Sum(loop) * rules.Loot * 1.09 * (1 + rules.FamilyBonus + 0.03) * treeLoot / (Sum(plain) * rules.Loot * (1 + rules.FamilyBonus));
            Assert.That(loop.Companions.StatOf(pack, TravelStat.Shards) / plain.Companions.StatOf(pack, TravelStat.Shards), Is.EqualTo(expected).Within(1e-9));
            Assert.That(rules.MentorShare + tree.Stat(MenagerieStat.MentorShare), Is.EqualTo(0.5).Within(1e-9), "the student gets half instead of thirty per cent");
        }

        [Test]
        public void TheBarGivesPassivesAndMajorTalentsSwapFreely()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            GivePoints(loop, 40);
            Assert.That(tree.ChooseMajor(20, "menagerie_m20_3"), Is.EqualTo(TreeResult.BarTooShort));
            for (var node = 1; node <= 4; node++)
                for (var i = 0; i < 5; i++)
                    tree.Learn(node);
            Assert.That(tree.BarPassives, Is.EqualTo(2));
            Assert.That(tree.Stat(MenagerieStat.AllExperience), Is.EqualTo(0.02).Within(1e-9));
            Assert.That(tree.Stat(MenagerieStat.TripLoot), Is.EqualTo(0.01).Within(1e-9));
            Assert.That(tree.ChooseMajor(20, "menagerie_m20_3"), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Stat(MenagerieStat.PetBond), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(tree.ChooseMajor(20, "menagerie_m20_2"), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Stat(MenagerieStat.PetBond), Is.Zero);
            Assert.That(tree.ChooseMajor(20, "menagerie_m50_1"), Is.EqualTo(TreeResult.UnknownMajor));
        }

        [Test]
        public void TheFullResetIsFreeOnceADayAndTheTreeSurvivesTheSave()
        {
            var loop = Camp();
            var tree = loop.Menagerie;
            GivePoints(loop, 40);
            tree.Learn(3);
            tree.Learn(3);
            var copy = Game.Core.Save.GameStateJson.Read(new ContentNode(Game.Core.Save.GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.TreeRanks[3], Is.EqualTo(2));
            Assert.That(copy.Companions.MenagerieLevel, Is.EqualTo(40));
            Assert.That(tree.ResetAll(), Is.EqualTo(TreeResult.Done));
            Assert.That(tree.Spent, Is.Zero);
            tree.Learn(3);
            Assert.That(tree.ResetAll(), Is.EqualTo(TreeResult.TooSoon));
            loop.SetLocalDay(101);
            Assert.That(tree.ResetAll(), Is.EqualTo(TreeResult.Done));
        }
    }
}
