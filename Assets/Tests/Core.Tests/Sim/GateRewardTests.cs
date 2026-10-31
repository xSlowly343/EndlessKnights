using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GateRewardTests
    {
        private static (GameLoop Loop, FakeInputSource Input) AtTheLimit()
        {
            var input = new FakeInputSource();
            var state = new GameState(9) { Class = HeroClass.Viking, Stage = 50000, HighestStage = 50000, RecordStage = 50000 };
            var loop = TestWorld.Loop(input, state);
            return (loop, input);
        }

        private static void KillTheKeeper(GameLoop loop, FakeInputSource input)
        {
            for (var i = 0; i < 400 && loop.State.Stage == 50000; i++)
            {
                loop.State.EnemyHp = BigNumber.FromDouble(1e-6);
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.Stage, Is.EqualTo(50001));
        }

        [Test]
        public void FirstKillOfTheKeeperGivesTheSealOfTheLimit()
        {
            var (loop, input) = AtTheLimit();
            var damage = loop.Hero.Value(CombatStats.Damage, 100);
            var gold = loop.Hero.Value(CombatStats.Gold, 100, StatTags.Gold);
            var points = loop.Levels.MilestonePoints;
            loop.Events.Clear();

            KillTheKeeper(loop, input);

            Assert.That(loop.State.GatesCleared, Does.Contain(50000L));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) / damage, Is.EqualTo(2).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Gold, 100, StatTags.Gold), Is.GreaterThan(gold));
            Assert.That(loop.Hero.Value(CombatStats.EchoGain, 1), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.Levels.MilestonePoints, Is.EqualTo(points + 1));
            var events = Enumerable.Range(0, loop.Events.Count).Select(i => loop.Events[i]).ToList();
            Assert.That(events.Count(e => e.Kind == GameEventKind.GateCleared), Is.EqualTo(1));
            var limitDrops = events.Where(e => e.Kind == GameEventKind.ItemDropped && e.Drop!.Source == DropSource.Limit).ToList();
            Assert.That(limitDrops.Count, Is.EqualTo(1));
            var drop = limitDrops[0].Drop!;
            Assert.That(drop.Gear?.Unique != null || drop.Weapon?.Unique != null, Is.True);
        }

        [Test]
        public void TheRewardComesOnlyOnceAndSurvivesPrestigeAndSave()
        {
            var (loop, input) = AtTheLimit();
            KillTheKeeper(loop, input);
            loop.State.Stage = 50000;
            loop.State.KillsOnStage = 0;
            loop.Events.Clear();
            KillTheKeeper(loop, input);

            var events = Enumerable.Range(0, loop.Events.Count).Select(i => loop.Events[i]).ToList();
            Assert.That(events.Count(e => e.Kind == GameEventKind.GateCleared), Is.Zero);
            Assert.That(events.Count(e => e.Drop?.Source == DropSource.Limit), Is.Zero);

            loop.State.RunGoldEarned = BigNumber.FromDouble(1e9);
            loop.Prestige.TryPrestige();
            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.State.GatesCleared, Does.Contain(50000L));
            Assert.That(loaded.Hero.Value(CombatStats.EchoGain, 1), Is.EqualTo(1.1).Within(1e-9));
        }
    }
}
