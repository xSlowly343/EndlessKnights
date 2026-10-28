using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class EncounterTests
    {
        private static EncounterRules Always(string encounter)
        {
            var json = TestData.ReadGameDataFile(CombatRules.FileName);
            json = encounter == "wanderer"
                ? json.Replace("\"wanderer\": { \"chance\": 0.05", "\"wanderer\": { \"chance\": 1").Replace("\"golden\": { \"chance\": 0.002", "\"golden\": { \"chance\": 0")
                : json.Replace("\"golden\": { \"chance\": 0.002", "\"golden\": { \"chance\": 1").Replace("\"wanderer\": { \"chance\": 0.05", "\"wanderer\": { \"chance\": 0");
            return CombatRules.Load(json).Encounters;
        }

        private static (GameLoop Loop, FakeInputSource Input) Hero(long stage, long record, double damage, string encounter)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, damage));
            var state = TestWorld.Classless(new GameState(9) { Class = HeroClass.Viking, Stage = stage, HighestStage = stage, RecordStage = record });
            var loop = TestWorld.Loop(input, state, hero);
            loop.Stages.Encounters = Always(encounter);
            return (loop, input);
        }

        private static void Kill(GameLoop loop, FakeInputSource input)
        {
            loop.State.EnemyHp = BigNumber.FromDouble(1e-6);
            input.PressKeys(1);
            loop.Tick();
        }

        [Test]
        public void TheWandererTakesTheBossPlaceAndJumpsTowardTheRecord()
        {
            var (loop, input) = Hero(40, 300, 1e12, "wanderer");
            long jumped = 0;
            for (var tick = 0; tick < 3000 && jumped == 0; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                {
                    if (loop.Events[i].Kind == GameEventKind.WandererJumped)
                        jumped = (long)loop.Events[i].Amount.ToDouble();
                }
                loop.Events.Clear();
            }

            Assert.That(jumped, Is.InRange(10, 50));
            Assert.That(loop.State.Stage, Is.InRange(51, 91));
            Assert.That(loop.State.HighestStage, Is.EqualTo(loop.State.Stage));
        }

        [Test]
        public void TheWandererNeverJumpsPastTheRecord()
        {
            var (loop, input) = Hero(40, 52, 1e12, "wanderer");
            for (var tick = 0; tick < 3000 && loop.State.Stage < 52; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
                loop.Events.Clear();
            }

            Assert.That(loop.State.Stage, Is.EqualTo(52));
        }

        [Test]
        public void NoWandererWithoutRoomBelowTheRecordOrBeforeItsStage()
        {
            Assert.That(Comes(40, 45), Is.False);
            Assert.That(Comes(20, 300), Is.False);
            Assert.That(Comes(40, 300), Is.True);
        }

        private static bool Comes(long stage, long record)
        {
            var (loop, input) = Hero(stage, record, 1e12, "wanderer");
            for (var tick = 0; tick < 2000 && !loop.State.FightingBoss; tick++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            return loop.State.WandererBoss;
        }

        [Test]
        public void TheGoldenColossusHasHugeHealthAndTurnsDamageIntoGoldForFifteenSeconds()
        {
            var (loop, input) = Hero(20, 20, 0, "golden");
            Kill(loop, input);
            Assert.That(loop.State.GoldenTicks, Is.GreaterThan(0));
            var normalHp = loop.State.GoldenNormalHp.ToDouble();
            Assert.That(loop.State.EnemyMaxHp.ToDouble() / normalHp, Is.EqualTo(1000).Within(1e-6));
            var kills = loop.State.KillsOnStage;
            var gold = loop.State.Gold;
            loop.Events.Clear();

            for (var tick = 0; tick < 15 * GameLoop.TicksPerSecond; tick++)
            {
                if (tick % 2 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }

            Assert.That((loop.State.Gold - gold).ToDouble(), Is.GreaterThan(0));
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(kills));
            Assert.That(Enumerable.Range(0, loop.Events.Count).Count(i => loop.Events[i].Kind == GameEventKind.GoldenColossusAppeared), Is.EqualTo(1));
        }

        [Test]
        public void ContentmentChestRaisesTheColossusGold()
        {
            double Gold(double bonus)
            {
                var (loop, input) = Hero(20, 20, 0, "golden");
                if (bonus > 0)
                    loop.Hero.Add(new Modifier(CombatStats.EncounterGold, ModifierKind.Increased, bonus));
                Kill(loop, input);
                var before = loop.State.Gold;
                loop.State.GoldenDamage = loop.State.GoldenNormalHp * 10;
                loop.Tick();
                return (loop.State.Gold - before).ToDouble();
            }

            Assert.That(Gold(0.15) / Gold(0), Is.EqualTo(1.15).Within(1e-6));
        }

        [Test]
        public void EncountersSurviveSaving()
        {
            var (loop, input) = Hero(20, 20, 0, "golden");
            Kill(loop, input);
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.GoldenTicks, Is.EqualTo(loop.State.GoldenTicks));
            Assert.That(loaded.GoldenNormalHp.ToDouble(), Is.EqualTo(loop.State.GoldenNormalHp.ToDouble()).Within(1e-6));
        }

        [Test]
        public void EncounterChancesAreSmallInTheRealData()
        {
            var rules = TestWorld.Data.Combat.Encounters;

            Assert.That(rules.WandererChance, Is.InRange(0.001, 0.2));
            Assert.That(rules.GoldenChance, Is.InRange(0.0001, 0.05));
            Assert.That(rules.JumpMin, Is.EqualTo(10));
            Assert.That(rules.JumpMax, Is.EqualTo(50));
        }
    }
}
