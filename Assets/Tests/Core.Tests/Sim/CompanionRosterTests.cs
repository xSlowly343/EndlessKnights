using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionRosterTests
    {
        private const double Hour = ExpeditionSystem.SecondsPerHour;
        private static CompanionRules Rules => TestWorld.Data.Companions;

        private static GameLoop Camp(ulong seed = 1)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(seed));
            loop.Tick();
            loop.SetLocalDay(100);
            return loop;
        }

        [Test]
        public void EighteenCompanionsWithNamesStagesAndRarity()
        {
            Assert.That(Rules.Companions.Count, Is.EqualTo(60));
            foreach (var def in Rules.Companions)
            {
                Assert.That(def.NameRu, Is.Not.Empty, def.Id);
                Assert.That(def.StagesRu.Count, Is.EqualTo(Rules.Stages.Count), def.Id);
                Assert.That(Rules.Rarities, Does.Contain(def.Rarity), def.Id);
            }
            Assert.That(Rules.Companions.Count(c => c.Source is "shop" or "pass"), Is.EqualTo(5));
        }

        [Test]
        public void BoughtAndEventCompanionsHaveTheirOwnTraitInTheFirstSlot()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("pet_golem");
            Assert.That(loop.Companions.BaseTrait("pet_golem"), Is.EqualTo("porter"));
            Assert.That(loop.Companions.TraitsOf("pet_golem"), Is.EqualTo(new[] { "porter" }));
            Assert.That(loop.Companions.BaseTrait("camp_dog"), Is.EqualTo("herbalist"));
        }

        [Test]
        public void ThePorterBringsOneMoreItem()
        {
            var golem = Camp(3);
            golem.State.Cosmetics.Owned.Add("pet_golem");
            golem.Expeditions.Send("pet_golem", "green_hills", 4);
            golem.Expeditions.Advance(4 * Hour);
            Assert.That(golem.State.Expeditions.Reports[0].Items[0], Is.EqualTo(9), "2 per hour x 4 h, +1 from the porter");
        }

        [Test]
        public void TheOwlFindsRareThingsTwiceAsOftenAtNight()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("pet_owl");
            loop.State.ServerHour = 12;
            Assert.That(loop.Companions.RareNightMultiplier("pet_owl"), Is.EqualTo(1));
            loop.State.ServerHour = 23;
            Assert.That(loop.Companions.RareNightMultiplier("pet_owl"), Is.EqualTo(2));
            Assert.That(loop.Companions.RareNightMultiplier("camp_dog"), Is.EqualTo(1));
        }

        [Test]
        public void TheSnowmanShortensIceTripsOnly()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("longnight_companion");
            Assert.That(loop.Expeditions.DurationFactor("longnight_companion", "ice_peak"), Is.EqualTo(0.9).Within(1e-9));
            Assert.That(loop.Expeditions.DurationFactor("longnight_companion", "green_hills"), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void TheFrogCatchesFishAndTheSunGivesMoreExperience()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("flood_companion");
            loop.State.Cosmetics.Owned.Add("solstice_companion");
            loop.Expeditions.Send("flood_companion", "green_hills", 1);
            loop.Expeditions.Send("solstice_companion", "green_hills", 2);
            loop.Expeditions.Advance(2 * Hour);
            Assert.That(loop.Camp.Count("fish_hills_roach"), Is.EqualTo(1));
            Assert.That(loop.Companions.Progress("solstice_companion").Experience, Is.EqualTo(24).Within(1e-9));
        }

        [Test]
        public void TheHarvesterBringsHalfAgainTheHerbs()
        {
            var loop = Camp(4);
            loop.State.Cosmetics.Owned.Add("pumpkin_companion");
            loop.Expeditions.Send("pumpkin_companion", "green_hills", 4);
            loop.Expeditions.Advance(4 * Hour);
            Assert.That(loop.State.Expeditions.Reports[0].Items[0], Is.EqualTo(15), "herbs x1.5 and own biome +25%");
        }

        [Test]
        public void TheFoxGetsLettersWithAChoiceMoreOften()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("pet_fox");
            Assert.That(loop.Companions.Own("pet_fox", OwnEffectKind.Choice), Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void TheFireflySendsLettersEarlier()
        {
            var loop = Camp();
            loop.State.Cosmetics.Owned.Add("pet_firefly");
            loop.Expeditions.Send("pet_firefly", "green_hills", 8);
            loop.Expeditions.Send("camp_dog", "green_hills", 8);
            loop.Events.Clear();
            loop.Expeditions.Advance(3.7 * Hour);
            Assert.That(loop.Events.CountOf(GameEventKind.ExpeditionLetter), Is.EqualTo(1));
        }

        [Test]
        public void EggCompanionsAreGrantedAndKeptInTheSave()
        {
            var loop = Camp();
            Assert.That(loop.Companions.Grant("egg_griffin"), Is.True);
            Assert.That(loop.Companions.Grant("egg_griffin"), Is.False);
            Assert.That(loop.Companions.Grant("camp_dog"), Is.False);
            Assert.That(loop.Companions.Grant("nobody"), Is.False);
            Assert.That(loop.Expeditions.Companions(), Does.Contain("egg_griffin"));
            Assert.That(loop.Expeditions.TraitOf("egg_griffin")!.Id, Is.EqualTo("wanderer"));
            Assert.That(loop.Expeditions.Send("egg_griffin", "green_hills", 1), Is.EqualTo(ExpeditionResult.Done));
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Owned, Does.Contain("egg_griffin"));
        }

        [Test]
        public void TheMoleSometimesBringsABone()
        {
            var bones = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var loop = Camp(seed);
                loop.Companions.Grant("museum_mole");
                loop.Expeditions.Send("museum_mole", "green_hills", 1);
                loop.Expeditions.Advance(Hour);
                bones += (int)loop.Camp.Count("bone_hills");
            }
            Assert.That(bones, Is.InRange(1, 12));
        }
    }
}
