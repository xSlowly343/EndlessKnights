using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TempoTests
    {
        private static TempoRules Rules => TestWorld.Data.Combat.Tempo;

        private static (GameLoop Loop, FakeInputSource Input) Fighting(StatId stat, double value)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(stat, ModifierKind.Flat, value));
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(7), hero);
            loop.Tick();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 300));
            return (loop, input);
        }

        [Test]
        public void TempoCountsPressesAMinuteFromCountersOnly()
        {
            Assert.That(Rules.Steps(40), Is.Zero);
            Assert.That(Rules.Steps(59), Is.EqualTo(1));
            Assert.That(Rules.Steps(200), Is.EqualTo(16));
            Assert.That(Rules.Steps(900), Is.EqualTo(16), "presses above 200 a minute do not count");
        }

        [Test]
        public void FastTypingRaisesDamageUpToFifteenPercent()
        {
            var (loop, input) = Fighting(CombatStats.TempoDamage, 0.01);
            for (var i = 0; i < 3600; i++)
            {
                if (i % 5 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.TempoPerMinute, Is.EqualTo(120).Within(3));
            Assert.That(loop.Combat.TempoBonus, Is.EqualTo(0.01 * Rules.Steps(loop.State.TempoPerMinute)).Within(1e-9));
            Assert.That(loop.Combat.TempoBonus, Is.InRange(0.07, 0.08));
            for (var i = 0; i < 1200; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.Combat.TempoBonus, Is.EqualTo(Rules.MaxBonus).Within(1e-9));
        }

        [Test]
        public void WithoutTheStatTempoGivesNothing()
        {
            var (loop, input) = Fighting(CombatStats.CritChance, 0);
            for (var i = 0; i < 600; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.TempoPerMinute, Is.GreaterThan(200));
            Assert.That(loop.Combat.TempoBonus, Is.Zero);
        }

        [Test]
        public void ARestChargesTheNextTwentyStrikes()
        {
            var (loop, input) = Fighting(CombatStats.RestCharge, 0.1);
            input.PressKeys(1);
            loop.Tick();
            var plain = loop.Combat.LastTickDamage;
            var idle = (int)((Rules.RestAfterSeconds + 120) * GameLoop.TicksPerSecond);
            for (var i = 0; i < idle; i++)
                loop.Tick();
            Assert.That(loop.State.RestCharge, Is.EqualTo(0.2).Within(1e-6), "two minutes after the first thirty seconds");
            Assert.That(loop.State.RestHitsLeft, Is.EqualTo(Rules.RestHits));
            for (var i = 0; i < Rules.RestHits; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.RestHitsLeft, Is.Zero);
            Assert.That(loop.State.RestCharge, Is.Zero);
        }

        [Test]
        public void ARestedStrikeHitsHarder()
        {
            var (rested, restedInput) = Fighting(CombatStats.RestCharge, 0.1);
            var (plain, plainInput) = Fighting(CombatStats.RestCharge, 0.1);
            rested.State.RestCharge = 0.5;
            rested.State.RestHitsLeft = 3;
            restedInput.PressKeys(1);
            rested.Tick();
            plainInput.PressKeys(1);
            plain.Tick();
            Assert.That((rested.Combat.LastTickDamage / plain.Combat.LastTickDamage).ToDouble(), Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void TheChargeStopsAtTenMinutes()
        {
            var (loop, _) = Fighting(CombatStats.RestCharge, 0.1);
            for (var i = 0; i < 20 * 60 * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(loop.State.RestCharge, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void AShortPauseChargesNothing()
        {
            var (loop, input) = Fighting(CombatStats.RestCharge, 0.1);
            for (var i = 0; i < 600; i++)
            {
                if (i % 200 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(loop.State.RestCharge, Is.Zero);
        }
    }
}
