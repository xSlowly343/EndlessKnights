using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyColossusTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyGrowthRules Growth => TestWorld.Data.Party.Growth;
        private static PartyColossusRules Rules => Growth.Colossus;

        private static BigNumber Boss(long stage) => TestWorld.Data.Stages.BossHp(stage);

        private static (GameLoop Loop, FakeInputSource Input) Member()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyColossusOpen(true);
            return (loop, input);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.PressKeys(3);
                loop.Tick();
            }
        }

        [Test]
        public void ColossusRulesAreRead()
        {
            Assert.That(Rules.PointsPerBoss, Is.EqualTo(100));
            Assert.That(Rules.Background, Is.EqualTo(0.1));
            Assert.That(Rules.Offline, Is.EqualTo(0.25));
            Assert.That(Rules.Hp(1), Is.EqualTo(15000));
            Assert.That(Rules.Hp(3), Is.EqualTo(15000 * 1.45 * 1.45).Within(1e-6));
            Assert.That(Rules.Ceiling(Growth, 9), Is.EqualTo(0));
            Assert.That(Rules.Ceiling(Growth, 10), Is.EqualTo(10));
            Assert.That(Rules.Ceiling(Growth, 15), Is.EqualTo(20));
            Assert.That(Rules.Ceiling(Growth, 95), Is.EqualTo(70));
        }

        [Test]
        public void DamageBecomesPointsByTheBossAtTheRecordStage()
        {
            var meter = new PartyColossusMeter(Rules);
            var stages = TestWorld.Data.Stages;
            meter.Note(BigNumber.Zero, stages, 50, false, true, true, 1);
            meter.Note(Boss(50), stages, 50, true, true, true, 1);
            Assert.That(meter.Direct, Is.EqualTo(100).Within(1e-9), "a boss of the record stage is a hundred points");
            meter.Note(Boss(50) * 3.0, stages, 50, false, true, true, 1);
            Assert.That(meter.Background, Is.EqualTo(20).Within(1e-9), "the background hit is a tenth");
            meter.Note(Boss(50) * 4.0, stages, 50, true, true, true, 1.5);
            Assert.That(meter.Direct, Is.EqualTo(250).Within(1e-9), "colossus damage bonuses count");
            meter.Note(Boss(50) * 5.0, stages, 50, true, false, true, 1);
            meter.Note(Boss(50) * 6.0, stages, 50, true, true, false, 1);
            Assert.That(meter.Pending, Is.EqualTo(270).Within(1e-9), "no input or no open colossus, no points");
            Assert.That(meter.TakePending(), Is.EqualTo(270).Within(1e-9));
            Assert.That(meter.Pending, Is.Zero);
        }

        [Test]
        public void OfflineKillsGiveAQuarter()
        {
            var meter = new PartyColossusMeter(Rules);
            var stages = TestWorld.Data.Stages;
            var result = new OfflineResult(3600, 70, BigNumber.Zero, 50, 50, 0, false, false, false);
            meter.NoteOffline(result, stages, 50, true, 1);
            var expected = (stages.NormalHp(50) * 70.0 / Boss(50)).ToDouble() * 100 * 0.25;
            var taken = meter.TakeOffline();
            Assert.That(taken.Points, Is.EqualTo(expected).Within(1e-9));
            Assert.That(taken.Seconds, Is.EqualTo(3600));
            meter.NoteOffline(result, stages, 50, false, 1);
            Assert.That(meter.TakeOffline().Points, Is.Zero, "without a party nothing goes to the colossus");
        }

        [Test]
        public void TheColossusTabNeedsAPartyWithTheColossusOpen()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.EnterPartyColossus(), Is.False);
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.EnterPartyColossus(), Is.False, "the colossus opens on party level 10");
            loop.SetPartyColossusOpen(true);
            Assert.That(loop.EnterPartyColossus(), Is.True);
            loop.SetPartyColossusOpen(false);
            Assert.That(loop.AtPartyColossus, Is.False);
        }

        [Test]
        public void FightingTheColossusStopsTheStageAndEveryHitCounts()
        {
            var (loop, input) = Member();
            Type(loop, input, 30);
            var stage = loop.State.Stage;
            var kills = loop.State.Kills;
            loop.PartyColossus.TakePending();
            Assert.That(loop.EnterPartyColossus(), Is.True);
            Type(loop, input, 600);
            Assert.That(loop.State.Stage, Is.EqualTo(stage));
            Assert.That(loop.State.Kills, Is.EqualTo(kills), "the colossus never dies in the tab");
            Assert.That(loop.State.EnemyAlive, Is.True);
            Assert.That(loop.PartyColossus.Direct, Is.GreaterThan(0));
            Assert.That(loop.PartyColossus.Background, Is.Zero);
            loop.LeavePartyColossus();
            Type(loop, input, 600);
            Assert.That(loop.State.Kills, Is.GreaterThan(kills));
            Assert.That(loop.PartyColossus.Background, Is.GreaterThan(0), "a tenth of the normal fight goes to the colossus");
        }

        [Test]
        public void AnIdleTwinkGivesTheColossusNothing()
        {
            var (loop, input) = Member();
            Type(loop, input, 10);
            for (var i = 0; i < 400; i++)
                loop.Tick();
            loop.PartyColossus.TakePending();
            for (var i = 0; i < 1200; i++)
                loop.Tick();
            Assert.That(loop.PartyColossus.Pending, Is.Zero);
        }

        [Test]
        public void ColossusLinesOfThePartyTreeRaiseColossusDamageOnly()
        {
            var (loop, _) = Member();
            loop.SetPartyTree(new Dictionary<int, int> { [6] = 1, [17] = 1 }, new string[0]);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.11).Within(1e-12));
            Assert.That(loop.Hero.Value(PartyGrowthLayer.ColossusDamage, 0), Is.Zero, "the line stays out of the hero's sheet");
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1));
        }
    }
}
