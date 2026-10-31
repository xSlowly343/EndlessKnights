using System.Collections.Generic;
using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AtlasTests
    {
        private static AtlasCatalog Catalog => TestWorld.Data.Atlas;

        private static GameLoop WithEcho(double echo) =>
            TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Echo = BigNumber.FromDouble(echo) });

        [Test]
        public void TheAtlasStartsFromOneNodeAndEveryNodeIsReachable()
        {
            Assert.That(Catalog.All.Count(node => node.Requires.Count == 0), Is.EqualTo(1));
            var reached = new HashSet<string>(Catalog.All.Where(node => node.Requires.Count == 0).Select(node => node.Id));
            for (var pass = 0; pass < Catalog.All.Count; pass++)
            {
                foreach (var node in Catalog.All.Where(node => node.Requires.Any(reached.Contains)))
                    reached.Add(node.Id);
            }
            Assert.That(reached.Count, Is.EqualTo(Catalog.All.Count));
        }

        [Test]
        public void TakingANodeCostsEchoAndAppliesItsEffect()
        {
            var loop = WithEcho(100);
            var gold = loop.Hero.Value(CombatStats.Gold, 1, StatTags.Gold);

            Assert.That(loop.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.Allocated));

            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(90).Within(1e-9));
            Assert.That(loop.Atlas.Taken("map_of_the_rim"), Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1, StatTags.Gold) - gold, Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void NodesNeedANeighbourEchoAndCannotBeTakenTwice()
        {
            var loop = WithEcho(30);

            Assert.That(loop.AllocateAtlasNode("nowhere"), Is.EqualTo(AtlasResult.UnknownNode));
            Assert.That(loop.AllocateAtlasNode("boss_purse"), Is.EqualTo(AtlasResult.NotConnected));
            Assert.That(loop.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.Allocated));
            Assert.That(loop.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.AlreadyTaken));
            Assert.That(loop.AllocateAtlasNode("boss_purse"), Is.EqualTo(AtlasResult.NotEnoughEcho));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void BossPurseAddsTenPercentGoldFromBosses()
        {
            var plain = WithEcho(0);
            var rich = WithEcho(0);
            rich.State.Atlas.Add("boss_purse");
            rich.Atlas.UseCatalog(Catalog);

            Assert.That(rich.Loot.GoldFor(10, EnemyType.Boss, null).ToDouble() / plain.Loot.GoldFor(10, EnemyType.Boss, null).ToDouble(), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(rich.Loot.GoldFor(10, EnemyType.Normal, null).ToDouble(), Is.EqualTo(plain.Loot.GoldFor(10, EnemyType.Normal, null).ToDouble()).Within(1e-9));
        }

        private static (double GuardianHp, List<GameEvent> Gold) Fight(params string[] atlas)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var state = new GameState(3) { Class = HeroClass.Viking };
            state.Atlas.AddRange(atlas);
            var loop = TestWorld.Loop(input, state, hero);
            var guardianHp = 0.0;
            var gold = new List<GameEvent>();
            var biome = TestWorld.Data.Stages.BiomeLength;
            for (var tick = 0; tick < 4000 && loop.State.Stage <= biome + 1; tick++)
            {
                input.PressKeys(3);
                loop.Advance(GameLoop.TickSeconds);
                if (guardianHp == 0 && loop.State.EnemyType == EnemyType.Guardian)
                    guardianHp = loop.State.EnemyMaxHp.ToDouble();
                for (var i = 0; i < loop.Events.Count; i++)
                {
                    if (loop.Events[i].Kind == GameEventKind.GoldGained && loop.Events[i].Stage == 5)
                        gold.Add(loop.Events[i]);
                }
                loop.Events.Clear();
            }
            return (guardianHp, gold);
        }

        [Test]
        public void TiredGuardiansComeWithTenPercentLessHealth()
        {
            var plain = Fight().GuardianHp;
            var tired = Fight("tired_guardians").GuardianHp;

            Assert.That(plain, Is.GreaterThan(0));
            Assert.That(tired / plain, Is.EqualTo(0.9).Within(1e-9));
        }

        [Test]
        public void ChestsOnTheRoadGiveThreeBossGoldsEveryFifthStage()
        {
            var plain = Fight().Gold;
            var chests = Fight("stage_chests").Gold;
            var expected = 3 * TestWorld.Loop(state: new GameState { Class = HeroClass.Viking }).Loot.GoldFor(5, EnemyType.Boss, null).ToDouble();

            Assert.That(chests.Count - plain.Count, Is.EqualTo(1));
            Assert.That(chests.Sum(e => e.Amount.ToDouble()) - plain.Sum(e => e.Amount.ToDouble()), Is.EqualTo(expected).Within(expected * 1e-6));
        }

        [Test]
        public void TheAtlasSurvivesSavingAndPrestige()
        {
            var loop = WithEcho(100);
            Assert.That(loop.AllocateAtlasNode("map_of_the_rim"), Is.EqualTo(AtlasResult.Allocated));
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;

            Assert.That(loaded.Atlas, Is.EquivalentTo(new[] { "map_of_the_rim" }));
            loop.State.HighestStage = 500;
            Assert.That(loop.Prestige.TryPrestige(), Is.True);
            Assert.That(loop.Atlas.Taken("map_of_the_rim"), Is.True);
        }
    }
}
