using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class TraitHudTests
    {
        private static readonly HeroClass[] Roster = (HeroClass[])System.Enum.GetValues(typeof(HeroClass));

        private static GameLoop Fighting(string pathId, HeroClass heroClass, int seconds)
        {
            var input = new FakeInputSource();
            var state = new GameState { Class = heroClass, HeroLevel = 60 };
            var loop = new GameLoop(TestWorld.Real, input, state, new StatSheet());
            foreach (var activity in TestWorld.Real.Ascendancy.Activities)
                loop.CompleteActivity(activity.Id);
            Assert.That(loop.ChooseSubclass(pathId), Is.EqualTo(Game.Core.Build.SubclassResult.Chosen), pathId);
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 60));
            loop.State.BossTicksLeft = 100_000;
            for (var tick = 0; tick < seconds * GameLoop.TicksPerSecond; tick++)
            {
                input.PressKeys(1);
                input.MoveMouse(400);
                if (tick % 10 == 0)
                    input.ClickLeft();
                loop.Tick();
            }
            return loop;
        }

        [Test]
        public void EveryOneOfTheFiftyFourTraitsHasOneToThreeLinesWithTexts()
        {
            var paths = TestWorld.Real.Ascendancy.Subclasses.Where(path => path.Trait != null).Select(path => path.Id).ToList();

            Assert.That(paths, Has.Count.EqualTo(54));
            Assert.That(TraitHud.Traits, Is.EquivalentTo(paths));
            foreach (var path in paths)
            {
                var lines = TraitHud.LinesOf(path);
                Assert.That(lines.Count, Is.InRange(1, TraitHud.MaxLines), path);
                foreach (var line in lines)
                    Assert.That(Texts.Has(line.LabelKey), Is.True, line.LabelKey);
            }
        }

        [TestCaseSource(nameof(Roster))]
        public void TheLinesOfEveryPathReadTheHeroState(HeroClass heroClass)
        {
            foreach (var path in TestWorld.Real.Ascendancy.SubclassesOf(HeroClassNames.Of(heroClass)))
            {
                var loop = Fighting(path.Id, heroClass, 20);
                var values = new TraitHudValue[TraitHud.MaxLines];
                var count = TraitHud.Fill(loop, values);

                Assert.That(count, Is.EqualTo(TraitHud.LinesOf(path.Id).Count), path.Id);
                for (var i = 0; i < count; i++)
                {
                    var line = TraitHud.LinesOf(path.Id)[i];
                    Assert.That(values[i].Key, Is.EqualTo(line.Key));
                    Assert.That(values[i].Label, Is.EqualTo(Texts.Get(line.LabelKey)));
                    Assert.That(values[i].Value, Is.EqualTo(System.Math.Max(0, line.Value(loop.State, loop.Combat, loop.Data.Combat.Classes))), line.Key);
                    Assert.That(double.IsFinite(values[i].Value) && double.IsFinite(values[i].Max), Is.True, line.Key);
                    Assert.That(values[i].Fill, Is.InRange(0, 1), line.Key);
                    if (values[i].Kind == TraitHudKind.Flag)
                        Assert.That(values[i].Value, Is.EqualTo(0).Or.EqualTo(1), line.Key);
                    else if (values[i].Kind != TraitHudKind.Count && values[i].Max > 0)
                        Assert.That(values[i].Value, Is.LessThanOrEqualTo(values[i].Max + 1e-9), $"{path.Id} {line.Key}");
                }
            }
        }

        [Test]
        public void TheCountersFollowTheMechanics()
        {
            var knots = Fighting("whisperer_trait_1", HeroClass.Whisperer, 30);
            Assert.That(Value(knots, "knots"), Is.EqualTo(knots.State.Whisperer.Knots).And.GreaterThan(0));
            Assert.That(Max(knots, "knots"), Is.EqualTo(WhispererState.MaxKnots));

            var shards = Fighting("shardcaller_trait_1", HeroClass.Shardcaller, 10);
            Assert.That(Value(shards, "crystals"), Is.EqualTo(shards.State.Shardcaller.Crystals).And.GreaterThan(0));
            Assert.That(Max(shards, "crystals"), Is.EqualTo(shards.Combat.Shardcaller.CrystalMax));

            var moths = Fighting("mothcaller_trait_1", HeroClass.Mothcaller, 5);
            Assert.That(Value(moths, "moths"), Is.EqualTo(moths.State.Mothcaller.Moths).And.GreaterThan(0));

            var golem = Fighting("golemancer_trait_2", HeroClass.Golemancer, 30);
            Assert.That(Value(golem, "golemRunes"), Is.EqualTo(golem.State.Golemancer.Runes));
            Assert.That(Max(golem, "golemRunes"), Is.EqualTo(golem.Combat.Golemancer.RunesMax));
        }

        [Test]
        public void WithoutAPathThereIsNothingToShow()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Duelist });

            Assert.That(TraitHud.Fill(loop, new TraitHudValue[TraitHud.MaxLines]), Is.Zero);
        }

        [Test]
        public void FillingTheHudDoesNotAllocate()
        {
            var loop = Fighting("cartomancer_trait_1", HeroClass.Cartomancer, 5);
            var values = new TraitHudValue[TraitHud.MaxLines];
            TraitHud.Fill(loop, values);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
                TraitHud.Fill(loop, values);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private static double Value(GameLoop loop, string key) => Read(loop, key).Value;

        private static double Max(GameLoop loop, string key) => Read(loop, key).Max;

        private static TraitHudValue Read(GameLoop loop, string key)
        {
            var values = new TraitHudValue[TraitHud.MaxLines];
            var count = TraitHud.Fill(loop, values);
            for (var i = 0; i < count; i++)
            {
                if (values[i].Key == key)
                    return values[i];
            }
            Assert.Fail($"no line {key}");
            return default;
        }
    }
}
