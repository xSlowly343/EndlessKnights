using System.Linq;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WorldSetLineTests
    {
        private const long Hour = 3_600_000;

        private static ItemCatalog Items => TestWorld.Data.Items;

        private static GameLoop Runedancer(FakeInputSource? input = null) =>
            TestWorld.Loop(input, new GameState(7) { Class = HeroClass.Runedancer });

        private static bool Holds(GameLoop loop, ModifierCondition condition) => (loop.Hero.Conditions & ModifierConditions.Bit(condition)) != 0;

        private static void Collect(GameLoop loop, string setId)
        {
            var set = Items.Sets.Single(s => s.Id == setId);
            foreach (var piece in set.Pieces)
                loop.State.SetCollection.Add(SetDef.PieceKey(setId, piece));
            loop.Gear.Rebuild();
        }

        [Test]
        public void FourNewLinesHaveASetInEveryRarity()
        {
            foreach (var line in new[] { "clock_shift", "biome_crossroads", "input_harmony", "fate_stake" })
                Assert.That(Items.Sets.Count(s => s.Theme == line), Is.EqualTo(6), line);
        }

        [Test]
        public void DayAndNightFollowServerTime()
        {
            var rules = TestWorld.Data.Combat.Conditions;
            Assert.That(rules.ServerHour(9 * Hour), Is.EqualTo(12), "the server runs at UTC+3");
            Assert.That(rules.ServerHour(-1 * Hour), Is.EqualTo(2));

            var loop = Runedancer();
            Collect(loop, "clock_shift_rare");
            loop.Tick();
            Assert.That(Holds(loop, ModifierCondition.Day) || Holds(loop, ModifierCondition.Night), Is.False, "no clock yet");
            var plain = loop.Hero.Value(CombatStats.Damage, 100);

            loop.SetServerTime(9 * Hour);
            loop.Tick();
            Assert.That(Holds(loop, ModifierCondition.Day), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100), Is.EqualTo(plain).Within(1e-9));

            loop.SetServerTime(20 * Hour);
            loop.Tick();
            Assert.That(Holds(loop, ModifierCondition.Night), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) - plain, Is.EqualTo(6).Within(1e-9));
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.Night], Is.EqualTo(0.5), "offline counts half the day as night");
        }

        [Test]
        public void TheRotationBiomeTurnsItsPieceOn()
        {
            var loop = Runedancer();
            loop.Tick();
            var index = BiomeRotation.Index(TestWorld.Data.Stages, loop.State, loop.State.Stage);
            Assert.That(index, Is.InRange(0, 4));
            Assert.That(Holds(loop, (ModifierCondition)((int)ModifierCondition.InBiome1 + index)), Is.True);
        }

        [Test]
        public void TheTurnMarksTheBiomeLeftLongestAgo()
        {
            var stages = TestWorld.Data.Stages;
            for (var seed = 1UL; seed < 30; seed++)
            {
                var state = new GameState(seed) { Biome = "green_hills", Tick = 10_000 };
                state.BiomeLeftTicks[1] = 500;
                state.BiomeLeftTicks[2] = 50;
                state.BiomeLeftTicks[3] = 900;
                state.BiomeLeftTicks[4] = 700;
                state.Tick = 10_000 + BiomeRotation.TicksPerTurn(stages);

                Assert.That(BiomeRotation.TryTurn(stages, state), Is.True);

                Assert.That(state.BiomeFresh, Is.EqualTo(state.Biome == "ice_peak"), state.Biome);
                Assert.That(state.BiomeLeftTicks[0], Is.EqualTo(state.Tick));
            }
        }

        [Test]
        public void TheRarestInputThisMinuteGetsTheBonus()
        {
            var input = new FakeInputSource();
            var loop = Runedancer(input);
            for (var tick = 0; tick < 600; tick++)
            {
                if (tick % 3 == 0)
                {
                    input.PressKeys(1);
                    input.ClickLeft();
                }
                input.MoveMouse(40);
                loop.Tick();
            }
            Assert.That(Holds(loop, ModifierCondition.LeastPause), Is.True);

            for (var tick = 0; tick < 1200; tick++)
            {
                if (tick % 40 < 20 && tick % 3 == 0)
                {
                    input.MoveMouse(40);
                    input.ClickLeft();
                }
                loop.Tick();
            }
            Assert.That(Holds(loop, ModifierCondition.LeastKeys), Is.True);
            Assert.That(Holds(loop, ModifierCondition.LeastPause), Is.False);
        }

        [Test]
        public void TheDailyStakeIsOwnForEveryPlayerAndKnownAhead()
        {
            var fates = Enumerable.Range(0, 300).Select(day => GameLoop.FateOf(123, day)).ToList();
            Assert.That(fates.Distinct().OrderBy(f => f), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(Enumerable.Range(0, 300).Count(day => GameLoop.FateOf(123, day) != GameLoop.FateOf(456, day)), Is.GreaterThan(100));

            var loop = Runedancer();
            loop.State.StartedUnixMs = 123;
            Collect(loop, "fate_stake_rare");
            var plain = loop.Hero.Value(CombatStats.Damage, 100);
            var day = Enumerable.Range(0, 300).First(d => GameLoop.FateOf(123, d) == 0);

            loop.SetLocalDay(day);
            loop.Tick();

            Assert.That(loop.FateOn(day + 1), Is.EqualTo(GameLoop.FateOf(123, day + 1)));
            Assert.That(Holds(loop, ModifierCondition.FateDamage), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100) - plain, Is.EqualTo(8).Within(1e-9));
        }
    }
}
