using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FacetTests
    {
        private static FacetRules Rules => TestWorld.Data.Combat.Facets;

        private static GameLoop Mounted(int level)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            for (var i = 0; i < MountRules.Slots; i++)
                loop.State.Mount.Levels[i] = level;
            return loop;
        }

        [Test]
        public void EverySlotOffersThreeFacetsOnFiveTiers()
        {
            Assert.That(Rules.Tiers, Is.EqualTo(new[] { 5, 10, 15, 20, 25 }));
            Assert.That(Rules.Costs, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(Rules.Slots.Count, Is.EqualTo(MountRules.Slots));
            foreach (var slot in Rules.Slots)
                Assert.That(slot.Count, Is.EqualTo(3));
        }

        [Test]
        public void TheFirstChoiceIsFreeAndAChangeCostsCuttersByTier()
        {
            var loop = Mounted(10);
            var facets = loop.Facets;
            Assert.That(facets.Choose(0, 2, "edge"), Is.EqualTo(FacetResult.Locked));
            Assert.That(facets.Choose(0, 1, "nothing"), Is.EqualTo(FacetResult.UnknownOption));
            Assert.That(facets.Choose(0, 1, "edge"), Is.EqualTo(FacetResult.Done));
            Assert.That(facets.Choose(0, 1, "edge"), Is.EqualTo(FacetResult.Repeat));
            Assert.That(facets.Choose(0, 1, "sting"), Is.EqualTo(FacetResult.NoCutters));
            loop.State.Mount.Cutters = 2;
            Assert.That(facets.Choose(0, 1, "sting"), Is.EqualTo(FacetResult.Done));
            Assert.That(loop.State.Mount.Cutters, Is.Zero);
            Assert.That(facets.Chosen(0, 1), Is.EqualTo("sting"));
        }

        [Test]
        public void AFacetAddsItsLines()
        {
            var loop = Mounted(5);
            var before = loop.Hero.Value(CombatStats.CritChance, 0);
            loop.Facets.Choose(3, 0, "grip");
            Assert.That(loop.Hero.Value(CombatStats.CritChance, 0), Is.EqualTo(before + 0.02).Within(1e-9));
        }

        [Test]
        public void GuardiansAndTheDailyQuestGiveCutters()
        {
            var loop = Mounted(0);
            loop.Facets.NoteGuardian(500, true);
            loop.Facets.NoteGuardian(1000, false);
            loop.Facets.NoteGuardian(550, true);
            Assert.That(loop.State.Mount.Cutters, Is.EqualTo(Rules.GuardianCutters));
            loop.Facets.NoteDailyClaim();
            Assert.That(loop.State.Mount.Cutters, Is.EqualTo(Rules.GuardianCutters + Rules.DailyCutters));
        }

        [Test]
        public void TheFirstRebuildAfterAClassChangeIsFree()
        {
            var loop = Mounted(5);
            loop.Facets.Choose(1, 0, "gaze");
            Assert.That(loop.Facets.RebuildAll(), Is.EqualTo(FacetResult.NoCrystals));
            loop.State.ClassChanges = 1;
            loop.Facets.NoteClassChange();
            Assert.That(loop.Facets.RebuildAll(), Is.EqualTo(FacetResult.Done));
            Assert.That(loop.Facets.Chosen(1, 0), Is.Empty);
            loop.State.ClassChanges = 2;
            loop.Facets.NoteClassChange();
            Assert.That(loop.Facets.RebuildAll(), Is.EqualTo(FacetResult.NoCrystals), "later rebuilds wait for the Crystal shop");
        }

        [Test]
        public void FacetsSurviveASave()
        {
            var loop = Mounted(5);
            loop.Facets.Choose(7, 0, "master");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Mount.Facets[7 * MountState.Tiers], Is.EqualTo("master"));
        }
    }
}
