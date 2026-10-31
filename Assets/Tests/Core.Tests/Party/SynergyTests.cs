using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Synergy;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SynergyTests
    {
        private static readonly StatId RageGain = new StatId("resourceGain");

        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        private static GameLoop Hero(HeroClass heroClass)
        {
            var loop = TestWorld.Loop();
            loop.State.Class = heroClass;
            return loop;
        }

        [Test]
        public void OwnMarkDoesNotWakeOwnResponse()
        {
            var loop = Hero(HeroClass.Voodoo);
            TestWorld.ArmFor(loop, TestWorld.Data.Gems["charm_prick"]);
            loop.Skills.Equip(TestWorld.Data.Gems["charm_prick"]);
            loop.State.Resource = 100;
            loop.RequestCast(0);

            loop.Tick();

            Assert.That(loop.Synergy.OwnMarks, Is.EqualTo(Mark("curse")));
            Assert.That(loop.Synergy.AllyMarks, Is.EqualTo(0));
            Assert.That(loop.Hero.Value(new StatId("curseEffect"), 1), Is.EqualTo(1));
        }

        [Test]
        public void AllyCurseWakesTheVikingResponse()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Synergy.SetAlly(2, true, HeroClass.Voodoo, Mark("curse"));

            loop.Tick();

            Assert.That(loop.Hero.Value(RageGain, 1), Is.EqualTo(1.25).Within(1e-9));
            loop.Synergy.SetAlly(2, true, HeroClass.Voodoo, 0);
            loop.Tick();
            Assert.That(loop.Hero.Value(RageGain, 1), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void SecondResponseNeedsTheNotable()
        {
            var loop = Hero(HeroClass.Viking);
            var threshold = new StatId("crushRageThreshold");
            loop.Synergy.SetAlly(0, true, HeroClass.Metalhead, Mark("aura"));

            loop.Tick();
            Assert.That(loop.Hero.Value(threshold, 0), Is.EqualTo(0));

            loop.Hero.Add(new Modifier(Rules.SecondResponseStat, ModifierKind.Flat, 1));
            loop.Tick();
            Assert.That(loop.Hero.Value(threshold, 0), Is.EqualTo(-0.1).Within(1e-9));
        }

        [Test]
        public void NomadGainsPerDistinctAllyMark()
        {
            var loop = Hero(HeroClass.Nomad);
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.Synergy.SetAlly(0, true, HeroClass.Voodoo, Mark("curse") | Mark("time"));
            loop.Synergy.SetAlly(1, true, HeroClass.PlagueDoctor, Mark("poison") | Mark("curse"));

            loop.Tick();

            Assert.That(loop.Synergy.PerMarkCount, Is.EqualTo(3));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.09).Within(1e-9));
        }

        [Test]
        public void FireAndPoisonFromTwoPlayersMakeMiasmaWithCooldown()
        {
            var loop = Hero(HeroClass.Ronin);
            var miasma = IndexOfReaction("miasma_burst");
            loop.Synergy.SetAlly(0, true, HeroClass.PlagueDoctor, Mark("poison"));
            loop.Synergy.SetAlly(1, true, HeroClass.Moonshiner, Mark("fire"));

            loop.Tick();
            Assert.That(loop.Synergy.ReactionActive(miasma), Is.True);
            Assert.That(loop.Synergy.ReactionMask, Is.EqualTo(1u << miasma), "the mask goes to the host for the chain reaction");
            Assert.That(loop.Hero.Value(new StatId("miasmaBurstShare"), 0), Is.EqualTo(0.3).Within(1e-9));

            for (var tick = 0; tick < 20; tick++)
                loop.Tick();
            Assert.That(loop.Synergy.ReactionActive(miasma), Is.False);
            Assert.That(loop.Synergy.ReactionCooldownSeconds(miasma), Is.GreaterThan(7));
            for (var tick = 0; tick < 80; tick++)
                loop.Tick();
            Assert.That(loop.Synergy.ReactionActive(miasma), Is.True);
        }

        [Test]
        public void OnePlayerWithBothMarksIsNoReaction()
        {
            var loop = Hero(HeroClass.Ronin);
            loop.Synergy.SetAlly(0, true, HeroClass.Captain, Mark("poison") | Mark("fire"));

            loop.Tick();

            Assert.That(loop.Synergy.ReactionActive(IndexOfReaction("miasma_burst")), Is.False);
        }

        [Test]
        public void FullOrchestraNeedsFourRoles()
        {
            var loop = Hero(HeroClass.Viking);
            var before = loop.Hero.Value(CombatStats.Gold, 1);
            loop.Synergy.SetAlly(0, true, HeroClass.Metalhead, 0);
            loop.Synergy.SetAlly(1, true, HeroClass.Voodoo, 0);
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(before).Within(1e-9));

            loop.Synergy.SetAlly(3, true, HeroClass.Gravedigger, 0);
            loop.Tick();

            Assert.That(loop.Synergy.ConstellationActive(IndexOfConstellation("full_orchestra")), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(before * 1.15).Within(1e-9));
        }

        [Test]
        public void AConstellationHoldsOnlyWhileEveryMemberFights()
        {
            var loop = Hero(HeroClass.Viking);
            var orchestra = IndexOfConstellation("full_orchestra");
            loop.Synergy.SetAlly(0, true, HeroClass.Metalhead, 0);
            loop.Synergy.SetAlly(1, true, HeroClass.Voodoo, 0);
            loop.Synergy.SetAlly(3, true, HeroClass.Gravedigger, 0, fighting: false);
            loop.Tick();
            Assert.That(loop.Synergy.ConstellationActive(orchestra), Is.False, "the summoner rests");

            loop.Synergy.SetAlly(3, true, HeroClass.Gravedigger, 0);
            loop.Tick();
            Assert.That(loop.Synergy.ConstellationActive(orchestra), Is.True);

            loop.Synergy.OwnFighting = false;
            loop.Tick();
            Assert.That(loop.Synergy.ConstellationActive(orchestra), Is.False, "the player rests");

            loop.Synergy.ClearAllies();
            Assert.That(loop.Synergy.OwnFighting, Is.True, "in solo the player always counts as fighting");
        }

        [Test]
        public void ElementsRemoveTheReactionCooldown()
        {
            var loop = Hero(HeroClass.Stormweaver);
            var overload = IndexOfReaction("overload");
            loop.Synergy.SetAlly(0, true, HeroClass.PlagueDoctor, Mark("poison"));
            loop.Synergy.SetAlly(1, true, HeroClass.Moonshiner, Mark("fire") | Mark("lightning"));
            loop.Synergy.SetAlly(2, true, HeroClass.Stormweaver, Mark("lightning"));

            for (var tick = 0; tick < 35; tick++)
                loop.Tick();

            Assert.That(loop.Synergy.ConstellationActive(IndexOfConstellation("elements")), Is.True);
            Assert.That(loop.Synergy.ReactionActive(overload), Is.True);
        }

        [Test]
        public void AStrikeMarkStaysForTheSecondsOfItsMarkAfterTheCast()
        {
            var loop = Hero(HeroClass.Viking);
            var gem = TestWorld.Data.Gems["no_mercy"];
            TestWorld.ArmFor(loop, gem);
            loop.Skills.Equip(gem);
            loop.State.Resource = 1000;
            loop.RequestCast(0);

            loop.Tick();

            Assert.That(loop.Synergy.OwnMarks & Mark("stun"), Is.EqualTo(Mark("stun")), "the heavy axe stuns");
            var ticks = Rules.MarkTicks(IndexOfMark("stun"));
            for (var tick = 0; tick < ticks; tick++)
                loop.Tick();
            Assert.That(loop.Synergy.OwnMarks & Mark("stun"), Is.Zero, "two seconds later the stun is gone");
        }

        [Test]
        public void PoisonFireAndMinionsAreMarksWhileTheyAreThere()
        {
            var loop = Hero(HeroClass.Captain);
            loop.State.PoisonStacks = 3;
            loop.State.BurnStacks = 2;
            loop.State.Minions[1] = new MinionState { Kind = MinionKind.Cannon, TicksLeft = 1000 };

            loop.Synergy.Tick();

            Assert.That(loop.Synergy.OwnMarks, Is.EqualTo(Mark("poison") | Mark("fire") | Mark("summon")));
            loop.State.PoisonStacks = 0;
            loop.State.BurnStacks = 0;
            loop.State.Minions[1] = default;
            loop.Synergy.Tick();
            Assert.That(loop.Synergy.OwnMarks, Is.Zero);
        }

        [TestCase(@"""stun"": 2,", @"""frost"": 2,", "unknown mark 'frost'")]
        [TestCase(@"""minionsOut"": ""summon""", @"""cannonsOut"": ""summon""", "unknown state 'cannonsOut'")]
        public void BrokenMarkSourcesAreReported(string from, string to, string message)
        {
            var json = TestData.ReadGameDataFile(SynergyRules.FileName).Replace(from, to);

            var error = Assert.Throws<ContentException>(() => SynergyRules.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        private static int IndexOfMark(string mark)
        {
            for (var i = 0; i < Rules.Marks.Count; i++)
                if (Rules.Marks[i] == mark)
                    return i;
            throw new System.ArgumentException(mark);
        }

        [Test]
        public void SynergyTicksDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var loop = Hero(HeroClass.Nomad);
            loop.Synergy.SetAlly(0, true, HeroClass.PlagueDoctor, Mark("poison"));
            loop.Synergy.SetAlly(1, true, HeroClass.Moonshiner, Mark("fire"));
            for (var tick = 0; tick < 200; tick++)
                loop.Tick();

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 50; tick++)
                loop.Tick();

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [TestCase(@"""mark"": ""curse"", ""modifiers"": [{ ""stat"": ""resourceGain""", @"""mark"": ""moonlight"", ""modifiers"": [{ ""stat"": ""resourceGain""", "unknown mark 'moonlight'")]
        [TestCase(@"""viking"": ""dd"", ", "", "class 'viking' has no role")]
        [TestCase(@"""marks"": [""fire"", ""poison""]", @"""marks"": [""fire"", ""fire""]", "two different marks")]
        public void BrokenSynergyIsReported(string from, string to, string message)
        {
            var json = TestData.ReadGameDataFile(SynergyRules.FileName).Replace(from, to);

            var error = Assert.Throws<ContentException>(() => SynergyRules.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void GemWithUnknownMarkIsReported()
        {
            var gems = Game.Core.Content.Gems.GemCatalog.Load(TestData.ReadGameDataFile("gems.json").Replace(@"""marks"": [""curse""]", @"""marks"": [""frost""]"));

            var error = Assert.Throws<ContentException>(() => Rules.RequireGemMarks(gems))!;

            Assert.That(error.Message, Does.Contain("unknown mark 'frost'"));
        }

        private static int IndexOfReaction(string id)
        {
            for (var i = 0; i < Rules.Reactions.Count; i++)
                if (Rules.Reactions[i].Id == id)
                    return i;
            throw new System.ArgumentException(id);
        }

        private static int IndexOfConstellation(string id)
        {
            for (var i = 0; i < Rules.Constellations.Count; i++)
                if (Rules.Constellations[i].Id == id)
                    return i;
            throw new System.ArgumentException(id);
        }
    }
}
