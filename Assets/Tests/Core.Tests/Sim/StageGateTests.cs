using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StageGateTests
    {
        private static StageTable Stages => TestWorld.Data.Stages;

        private static (GameLoop Loop, FakeInputSource Input) Hero(long stage, long record, StatSheet? hero = null)
        {
            var input = new FakeInputSource();
            var state = TestWorld.Classless(new GameState(9) { Class = HeroClass.Viking, Stage = stage, HighestStage = stage, RecordStage = record });
            var loop = TestWorld.Loop(input, state, hero);
            return (loop, input);
        }

        private static void ReachBoss(GameLoop loop, FakeInputSource input)
        {
            for (var i = 0; i < 400 && !loop.State.FightingBoss; i++)
            {
                loop.State.EnemyHp = BigNumber.FromDouble(1e-6);
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.FightingBoss, Is.True);
        }

        [Test]
        public void TheFirstLimitStandsAtStageFiftyThousand()
        {
            var gate = Stages.GateAt(50000);

            Assert.That(gate, Is.Not.Null);
            Assert.That(gate!.NameRu, Is.EqualTo("Хранитель Предела"));
            Assert.That(Stages.GateAt(49999), Is.Null);
            Assert.That(Stages.NextGateFrom(1), Is.EqualTo(50000));
            Assert.That(Stages.NextGateFrom(50001), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void TheLimitBossIsMuchStrongerAndHasItsOwnTimer()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.BossTimerSeconds, ModifierKind.Flat, 60));
            var (gateLoop, gateInput) = Hero(50000, 50000, hero);
            var (plainLoop, plainInput) = Hero(49950, 50000);
            var gate = Stages.GateAt(50000)!;

            ReachBoss(gateLoop, gateInput);
            ReachBoss(plainLoop, plainInput);

            Assert.That(gateLoop.State.EnemyType, Is.EqualTo(EnemyType.Guardian));
            var expectedRatio = (Stages.BossHp(50000) / Stages.BossHp(49950)).ToDouble() * gate.HpMultiplier;
            Assert.That((gateLoop.State.EnemyMaxHp / plainLoop.State.EnemyMaxHp).ToDouble(), Is.EqualTo(expectedRatio).Within(expectedRatio * 1e-6));
            Assert.That(gateLoop.State.BossSecondsLeft, Is.EqualTo(gate.TimerSeconds).Within(0.11));
        }

        [Test]
        public void TheWandererNeverJumpsPastTheLimit()
        {
            var json = TestData.ReadGameDataFile(CombatRules.FileName)
                .Replace("\"wanderer\": { \"chance\": 0.05", "\"wanderer\": { \"chance\": 1")
                .Replace("\"golden\": { \"chance\": 0.002", "\"golden\": { \"chance\": 0");
            var (loop, input) = Hero(49960, 60000);
            loop.Stages.Encounters = CombatRules.Load(json).Encounters;

            for (var tick = 0; tick < 3000 && loop.State.Stage < 50000; tick++)
            {
                loop.State.EnemyHp = BigNumber.FromDouble(1e-6);
                input.PressKeys(1);
                loop.Tick();
                loop.Events.Clear();
            }

            Assert.That(loop.State.Stage, Is.EqualTo(50000));
        }

        [Test]
        public void OfflineProgressStopsAtTheLimit()
        {
            var state = TestWorld.Classless(new GameState(2) { Class = HeroClass.Viking, Stage = 49990, HighestStage = 49990, RecordStage = 49990 });
            var result = new OfflineCalculator(TestWorld.Data, new StatSheet()).Simulate(state, 3600 * 8, BigNumber.Create(1, 4000), fightGuardians: true);

            Assert.That(result.StageAfter, Is.EqualTo(50000));
            Assert.That(result.StoppedAtGuardian, Is.True);
        }

        [Test]
        public void SetsAboveTheCeilingCannotBeCrafted()
        {
            var (loop, _) = Hero(300, 300);
            loop.State.Drops.Ingots = 1000;

            Assert.That(loop.CraftSetPiece("mountain_hammer"), Is.EqualTo(CraftResult.AboveCeiling));
            Assert.That(loop.State.Drops.Ingots, Is.EqualTo(1000));
            Assert.That(loop.CraftSetPiece("jarl_forge"), Is.EqualTo(CraftResult.Crafted));

            loop.State.RecordStage = 50000;
            Assert.That(loop.CraftSetPiece("mountain_hammer"), Is.EqualTo(CraftResult.Crafted));
        }
    }
}
