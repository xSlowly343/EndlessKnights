using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionAbilityTests
    {
        private static GameLoop Awake(params string[] companions)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            foreach (var companion in companions)
            {
                loop.Companions.Grant(companion);
                var progress = loop.Companions.Progress(companion);
                progress.Stage = TestWorld.Data.Companions.Stages.Count - 1;
                progress.Circle = 10;
            }
            return loop;
        }

        private static long Loot(Expedition trip)
        {
            var total = trip.Shards;
            foreach (var count in trip.Items)
                total += count;
            return total;
        }

        [Test]
        public void EveryFamilyHasOneAbility()
        {
            var rules = TestWorld.Data.Companions;
            Assert.That(rules.Abilities.Abilities.Count, Is.EqualTo(rules.Families.Families.Count));
            foreach (var family in rules.Families.Families)
                Assert.That(rules.Abilities.Of(family.Id), Is.Not.Null, family.Id);
            Assert.That(rules.Abilities.Of("party")!.Works, Is.False, "the far party trip lives on the server");
        }

        [Test]
        public void TheAbilityOpensOnTheTenthCircleAndWorksOnceADay()
        {
            var loop = Awake("camp_dog");
            var abilities = loop.CompanionAbilities;
            loop.Companions.Progress("camp_dog").Circle = 9;
            Assert.That(abilities.Use("camp_dog"), Is.EqualTo(CompanionResult.Locked));
            loop.Companions.Progress("camp_dog").Circle = 10;
            Assert.That(abilities.Use("camp_dog"), Is.EqualTo(CompanionResult.NothingToDo), "nothing grows yet");
            Assert.That(loop.Garden.Plant(0, "knockbloom"), Is.EqualTo(GardenResult.Done));
            Assert.That(abilities.Use("camp_dog"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Garden.Beds[0].Ready, Is.True);
            loop.Garden.Harvest(0);
            loop.Garden.Plant(0, "knockbloom");
            Assert.That(abilities.Use("camp_dog"), Is.EqualTo(CompanionResult.TooSoon));
            loop.SetLocalDay(101);
            Assert.That(abilities.Ready("camp_dog"), Is.True);
        }

        [Test]
        public void TailwindShortensEveryTrip()
        {
            var loop = Awake("egg_griffin");
            Assert.That(loop.Expeditions.Send("camp_dog", "green_hills", 4), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.Expeditions.Send("camp_crow", "green_hills", 1), Is.EqualTo(ExpeditionResult.Done));
            Assert.That(loop.CompanionAbilities.Use("egg_griffin"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Expeditions.Active.Count, Is.EqualTo(1), "the short trip is home");
            Assert.That(loop.State.Expeditions.Active[0].Elapsed, Is.EqualTo(7200).Within(1e-6));
        }

        [Test]
        public void TheFoldBringsATripHomeWithMoreLoot()
        {
            var plain = Awake("heart_fold");
            plain.Expeditions.Send("camp_dog", "green_hills", 4);
            plain.Expeditions.Finish(0);
            var folded = Awake("heart_fold");
            folded.Expeditions.Send("camp_dog", "green_hills", 4);
            Assert.That(folded.CompanionAbilities.Use("heart_fold"), Is.EqualTo(CompanionResult.Done));
            Assert.That(folded.State.Expeditions.Active, Is.Empty);
            Assert.That(Loot(folded.State.Expeditions.Reports[0]), Is.GreaterThan(Loot(plain.State.Expeditions.Reports[0])));
        }

        [Test]
        public void FrenzyMakesFishBiteFasterForTenMinutesOfFishing()
        {
            var loop = Awake("egg_mammoth");
            Assert.That(loop.Fishing.BiteSpeed, Is.EqualTo(1));
            Assert.That(loop.CompanionAbilities.Use("egg_mammoth"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Fishing.BiteSpeed, Is.EqualTo(3));
            Assert.That(loop.State.Companions.QuickBiteSeconds, Is.EqualTo(600));
        }

        [Test]
        public void TrueHeatMakesOneColdQuenchCountAsHot()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(1));
            loop.State.RecordStage = 51;
            loop.State.Camp.Tier = 1;
            loop.Tick();
            loop.SetLocalDay(100);
            loop.Camp.SetGuilds(new[] { "smiths" });
            loop.Companions.Grant("egg_wormlet");
            loop.Companions.Progress("egg_wormlet").Circle = 10;
            Assert.That(loop.CompanionAbilities.Use("egg_wormlet"), Is.EqualTo(CompanionResult.Done));
            loop.SetLocalDay(101);
            Assert.That(loop.CompanionAbilities.Use("egg_wormlet"), Is.EqualTo(CompanionResult.NothingToDo), "one sure quench waits at a time");
            loop.Camp.Add("ore_hills", 100);
            Assert.That(loop.Forge.TemperWeapon(), Is.EqualTo(ForgeResult.Done));
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(ForgeSystem.Activity);
            for (var i = 0; i < TestWorld.Data.Forge.Hammers; i += 10)
            {
                input.PressKeys(10);
                loop.Tick();
            }
            loop.State.Forge.Heat = 0;
            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.State.Weapons.Held!.Forge.Temper, Is.EqualTo(TestWorld.Data.Forge.StepInZone).Within(1e-12));
            Assert.That(loop.State.Companions.SureForge, Is.False);
        }

        [Test]
        public void RallyHelpsOnlyTheHeroOfItsClassForAnHour()
        {
            var loop = Awake("rune_stoat");
            loop.State.Class = HeroClass.Duelist;
            Assert.That(loop.CompanionAbilities.Use("rune_stoat"), Is.EqualTo(CompanionResult.WrongClass));
            loop.State.Class = HeroClass.Runedancer;
            Assert.That(loop.CompanionAbilities.Use("rune_stoat"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Levels.ExtraXp, Is.EqualTo(0.1).Within(1e-9));
            loop.Companions.Advance(3600);
            Assert.That(loop.Levels.ExtraXp, Is.Zero);
        }

        [Test]
        public void TheGatheringWaitsForTheServer()
        {
            var loop = Awake("banner_pup");
            Assert.That(loop.CompanionAbilities.Use("banner_pup"), Is.EqualTo(CompanionResult.Waits));
        }

        [Test]
        public void TheAbilityDaySurvivesASave()
        {
            var loop = Awake("egg_mammoth");
            loop.CompanionAbilities.Use("egg_mammoth");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["egg_mammoth"].AbilityDay, Is.EqualTo(100));
            Assert.That(copy.Companions.QuickBiteSeconds, Is.EqualTo(600));
            Assert.That(copy.Companions.QuickBiteFactor, Is.EqualTo(3));
        }
    }
}
