using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Slates;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SlateTests
    {
        private static SlateRules Rules => TestWorld.Data.Slates;

        private static SlateShape Shape(string id) => Rules.Shapes.Single(shape => shape.Id == id);

        private static SlateTheme Theme(string id) => Rules.Themes.Single(theme => theme.Id == id);

        private static GameLoop Hero(HeroClass heroClass = HeroClass.Viking, StatSheet? hero = null) =>
            TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero ?? new StatSheet());

        private static SlateItem Give(GameLoop loop, string shape, string theme, SlateRarity rarity = SlateRarity.Common, int line = 0)
        {
            var themeDef = Theme(theme);
            var slate = new SlateItem(Shape(shape), themeDef, rarity, new[] { themeDef.Lines[line] }, false);
            loop.Slates.Give(slate);
            return slate;
        }

        private static double Sum(GameLoop loop, string stat) =>
            loop.Slates.Modifiers.Where(modifier => modifier.Stat.Name == stat).Sum(modifier => modifier.Value);

        [Test]
        public void TheCatalogHasSevenShapesFourRaritiesTwelveThemesAndTwoLinesPerClass()
        {
            Assert.That(Rules.Shapes.Select(shape => shape.Size), Is.EqualTo(new[] { 1, 2, 3, 3, 4, 4, 4 }));
            Assert.That(Rules.Rarities.Count, Is.EqualTo(4));
            Assert.That(Rules.Themes.Count, Is.EqualTo(12));
            foreach (var heroClass in HeroClassNames.ByName.Values)
                Assert.That(Rules.ClassLines(heroClass).Count, Is.EqualTo(2), heroClass.ToString());
        }

        [Test]
        public void TheBoardStartsAtFourByFourAndGrowsWithEachLimitToEightByEight()
        {
            var loop = Hero();
            Assert.That(loop.Slates.Size, Is.EqualTo((4, 4)));

            loop.State.GatesCleared.Add(5000);
            Assert.That(loop.Slates.Size, Is.EqualTo((5, 4)));
            loop.State.GatesCleared.Add(10000);
            Assert.That(loop.Slates.Size, Is.EqualTo((5, 5)));
            for (var gate = 3; gate <= 20; gate++)
                loop.State.GatesCleared.Add(gate * 5000);
            Assert.That(loop.Slates.Size, Is.EqualTo((8, 8)));
        }

        [Test]
        public void ShapesTurnInQuartersAndStayInTheirCorner()
        {
            var corner = Shape("corner");
            for (var rotation = 0; rotation < SlateShape.Rotations; rotation++)
            {
                var cells = corner.Cells(rotation);
                Assert.That(cells.Count, Is.EqualTo(3));
                Assert.That(cells.Min(cell => cell.X), Is.Zero);
                Assert.That(cells.Min(cell => cell.Y), Is.Zero);
                Assert.That(cells.Max(cell => cell.X), Is.EqualTo(1));
                Assert.That(cells.Max(cell => cell.Y), Is.EqualTo(1));
            }
            Assert.That(Shape("bar").Cells(1).Max(cell => cell.Y), Is.EqualTo(2));
        }

        [Test]
        public void AGuardianOfARecordStageDropsASlateOfRareOrBetter()
        {
            var loop = Hero();
            loop.Events.Clear();

            loop.Drops.OnBossDefeated(50, EnemyType.Guardian, 40, true);
            Assert.That(loop.Slates.Pending, Is.EqualTo(1));
            loop.Drops.Flush();

            Assert.That(loop.Slates.Bag.Count, Is.EqualTo(1));
            Assert.That(loop.Slates.Bag[0].Rarity, Is.GreaterThanOrEqualTo(SlateRarity.Rare));
            Assert.That(loop.State.Slates.Found, Is.EqualTo(1));
            Assert.That(Enumerable.Range(0, loop.Events.Count).Any(i => loop.Events[i].Kind == GameEventKind.SlateDropped), Is.True);
        }

        [Test]
        public void SlatesWaitForTheirStage()
        {
            var loop = TestWorld.Loop(state: new GameState { RecordStage = 100 });
            Assert.That(TestWorld.Data.Unlocks.For(Feature.Slates).Stage, Is.Zero);
            var real = new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { RecordStage = 100 }, new StatSheet());

            real.Drops.OnBossDefeated(100, EnemyType.Guardian, 100, true);
            real.Drops.Flush();

            Assert.That(real.Slates.Open, Is.False);
            Assert.That(real.Slates.Bag, Is.Empty);
            Assert.That(TestWorld.Real.Unlocks.For(Feature.Slates).Stage, Is.EqualTo(150));
            Assert.That(loop.Slates.Open, Is.True);
        }

        [Test]
        public void APlacedSlateGivesItsLineAndTakingItOffRemovesIt()
        {
            var loop = Hero();
            var slate = Give(loop, "pebble", "keys");
            var before = loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard);

            Assert.That(loop.Slates.Place(slate, 1, 1, 0), Is.EqualTo(SlateResult.Done));
            Assert.That(loop.Slates.Bag, Is.Empty);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.GreaterThan(before));
            Assert.That(Sum(loop, "damage"), Is.EqualTo(0.06).Within(1e-9));

            Assert.That(loop.Slates.Remove(slate), Is.EqualTo(SlateResult.Done));
            Assert.That(loop.Slates.Modifiers, Is.Empty);
            Assert.That(loop.Slates.Bag, Does.Contain(slate));
        }

        [Test]
        public void SlatesMustFitOnTheBoardWithoutOverlapping()
        {
            var loop = Hero();
            var square = Give(loop, "square", "keys");
            var bar = Give(loop, "bar", "keys");

            Assert.That(loop.Slates.Place(square, 3, 0, 0), Is.EqualTo(SlateResult.OutOfBoard));
            Assert.That(loop.Slates.Place(square, 0, 0, 0), Is.EqualTo(SlateResult.Done));
            Assert.That(loop.Slates.Place(bar, 1, 1, 0), Is.EqualTo(SlateResult.Overlaps));
            Assert.That(loop.Slates.Place(bar, 2, 0, 1), Is.EqualTo(SlateResult.Done));
            Assert.That(loop.Slates.At(2, 2)!.Slate, Is.SameAs(bar));
            Assert.That(loop.Slates.Place(bar, 0, 3, 0), Is.EqualTo(SlateResult.NotInBag));
        }

        [Test]
        public void BiggerShapesAndRarerSlatesHaveStrongerLines()
        {
            var loop = Hero();
            var square = Give(loop, "square", "keys", SlateRarity.Rare);
            loop.Slates.Place(square, 0, 0, 0);

            Assert.That(Sum(loop, "damage"), Is.EqualTo(0.06 * 2 * 1.25).Within(1e-9));
        }

        [Test]
        public void TouchingSlatesOfOneThemeStrengthenEachOtherUpToThirtyPercent()
        {
            var loop = Hero();
            var middle = Give(loop, "pebble", "keys");
            loop.Slates.Place(middle, 1, 1, 0);
            var mouse = Give(loop, "pebble", "mouse");
            loop.Slates.Place(mouse, 1, 0, 0);
            Assert.That(Sum(loop, "damage"), Is.EqualTo(0.12).Within(1e-9), "different themes do not help each other");

            foreach (var (x, y) in new[] { (0, 1), (2, 1), (1, 2) })
                loop.Slates.Place(Give(loop, "pebble", "keys"), x, y, 0);

            var placement = loop.Slates.PlacementOf(middle)!;
            Assert.That(loop.Slates.SameThemeNeighbours(placement), Is.EqualTo(3));
            Assert.That(loop.Slates.AdjacencyBonus(placement), Is.EqualTo(0.3).Within(1e-9));
            loop.Slates.Place(Give(loop, "pebble", "keys"), 3, 3, 0);
            Assert.That(loop.Slates.AdjacencyBonus(loop.Slates.PlacementOf(loop.Slates.At(3, 3)!.Slate)!), Is.Zero);
        }

        [Test]
        public void FourSlatesOfOneThemeInOnePieceGiveThePatternLine()
        {
            var loop = Hero();
            var keys = Theme("keys");
            foreach (var x in new[] { 0, 1, 2 })
                loop.Slates.Place(Give(loop, "pebble", "keys"), x, 0, 0);
            loop.Slates.Place(Give(loop, "pebble", "keys"), 3, 3, 0);
            Assert.That(loop.Slates.HasPattern(keys), Is.False);

            loop.Slates.Place(Give(loop, "pebble", "keys"), 3, 0, 0);

            Assert.That(loop.Slates.HasPattern(keys), Is.True);
            Assert.That(loop.Slates.Modifiers.Any(modifier => modifier.Kind == ModifierKind.More && modifier.Tags == StatTags.Keyboard), Is.True);
        }

        [Test]
        public void AClassLineWorksOnlyForItsClass()
        {
            var loop = Hero(HeroClass.Viking);
            var line = Rules.ClassLines(HeroClass.Viking).Single(classLine => classLine.Id == "viking_shield_damage");
            var slate = new SlateItem(Shape("pebble"), Theme("keys"), SlateRarity.Common, new[] { line }, false);
            loop.Slates.Give(slate);
            loop.Slates.Place(slate, 0, 0, 0);
            Assert.That(Sum(loop, "shieldDamage"), Is.EqualTo(0.1).Within(1e-9));

            Assert.That(loop.ChangeClassFree(HeroClass.Ronin), Is.EqualTo(ClassChangeResult.Changed));

            Assert.That(Sum(loop, "shieldDamage"), Is.Zero);
            Assert.That(loop.Slates.PlacementOf(slate), Is.Not.Null, "the slate stays on the board");
        }

        [Test]
        public void WholeLinesStayWholeNumbers()
        {
            var loop = Hero(HeroClass.Viking);
            var line = Rules.ClassLines(HeroClass.Viking).Single(classLine => classLine.Id == "viking_shield_sooner");
            var slate = new SlateItem(Shape("corner"), Theme("keys"), SlateRarity.Common, new[] { line }, false);
            loop.Slates.Give(slate);
            loop.Slates.Place(slate, 0, 0, 0);

            Assert.That(Sum(loop, "shieldEvery"), Is.EqualTo(-2));
        }

        [Test]
        public void RaritiesDecideHowManyLinesASlateHas()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { ClassChosen = false });
            var square = Shape("square");
            var keys = Theme("keys");

            Assert.That(loop.Slates.Roll(square, keys, SlateRarity.Common).Lines.Count, Is.EqualTo(1));
            Assert.That(loop.Slates.Roll(square, keys, SlateRarity.Rare).Lines.Count, Is.EqualTo(2));
            Assert.That(loop.Slates.Roll(Shape("pebble"), keys, SlateRarity.Epic).Lines.Count, Is.EqualTo(2));
            var legendary = loop.Slates.Roll(square, keys, SlateRarity.Legendary);
            Assert.That(legendary.Lines.Count, Is.EqualTo(3));
            Assert.That(legendary.Lines[0], Is.SameAs(keys.Rule));
            Assert.That(legendary.Lines.Skip(1).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void TemperingCostsShardsAndWorksOnce()
        {
            var loop = Hero();
            var outcomes = new System.Collections.Generic.List<SlateResult>();
            loop.State.Drops.Shards = Rules.TemperShards * 20;
            for (var i = 0; i < 20; i++)
            {
                var slate = Give(loop, "pebble", "keys");
                var result = loop.Slates.Temper(slate);
                outcomes.Add(result);
                Assert.That(slate.Lines.Count, Is.EqualTo(result == SlateResult.Done ? 2 : 1));
                Assert.That(slate.Tempered, Is.True);
                Assert.That(loop.Slates.Temper(slate), Is.EqualTo(SlateResult.AlreadyTempered));
            }

            Assert.That(loop.State.Drops.Shards, Is.Zero);
            Assert.That(outcomes, Has.Member(SlateResult.Done).And.Member(SlateResult.TemperFailed));
            Assert.That(loop.Slates.Temper(Give(loop, "pebble", "keys")), Is.EqualTo(SlateResult.NotEnoughShards));
        }

        [Test]
        public void DismantlingAndAFullBagGiveDust()
        {
            var loop = Hero();
            var slate = Give(loop, "pebble", "keys", SlateRarity.Epic);

            Assert.That(loop.Slates.Dismantle(slate), Is.EqualTo(SlateResult.Done));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(Rules.Rarity(SlateRarity.Epic).Dust));

            for (var i = 0; i < Rules.BagSize; i++)
                Give(loop, "pebble", "keys");
            Give(loop, "pebble", "keys", SlateRarity.Legendary);

            Assert.That(loop.Slates.Bag.Count, Is.EqualTo(Rules.BagSize));
            Assert.That(loop.State.Drops.Dust, Is.EqualTo(Rules.Rarity(SlateRarity.Epic).Dust + Rules.Rarity(SlateRarity.Legendary).Dust));
        }

        [Test]
        public void TheSlateFacetArtifactStrengthensSlates()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.SlateEffect, ModifierKind.Increased, 0.5));
            var loop = Hero(hero: hero);
            loop.Slates.Place(Give(loop, "pebble", "keys"), 0, 0, 0);

            Assert.That(Sum(loop, "damage"), Is.EqualTo(0.09).Within(1e-9));
            Assert.That(TestWorld.Data.Artifacts.All.Single(artifact => artifact.Id == "jewel_facet").PerLevel[0].Stat, Is.EqualTo(CombatStats.SlateEffect));
        }

        [Test]
        public void TheBagAndTheBoardAreSaved()
        {
            var loop = Hero();
            var placed = Give(loop, "corner", "crit", SlateRarity.Rare);
            loop.Slates.Place(placed, 2, 1, 3);
            placed.Tempered = true;
            Give(loop, "zigzag", "loot", SlateRarity.Legendary, 2);

            var state = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;
            var loaded = TestWorld.Loop(state: state);

            Assert.That(state.Slates.Found, Is.EqualTo(2));
            Assert.That(state.Slates.Bag.Single().Shape.Id, Is.EqualTo("zigzag"));
            Assert.That(state.Slates.Bag.Single().Lines.Single().Id, Is.EqualTo(Theme("loot").Lines[2].Id));
            var board = state.Slates.Board.Single();
            Assert.That((board.X, board.Y, board.Rotation, board.Slate.Rarity, board.Slate.Tempered), Is.EqualTo((2, 1, 3, SlateRarity.Rare, true)));
            Assert.That(loaded.Slates.Modifiers.Select(modifier => modifier.Value), Is.EqualTo(loop.Slates.Modifiers.Select(modifier => modifier.Value)));
            Assert.That(state.Slates.Rng.State, Is.EqualTo(loop.State.Slates.Rng.State));
        }
        [Test]
        public void EverySlateStatHasAMechanic()
        {
            var known = typeof(CombatStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(StatId))
                .Select(field => ((StatId)field.GetValue(null)!).Name)
                .ToHashSet();
            var lines = Rules.Themes.SelectMany(theme => theme.Lines.Append(theme.Pattern).Append(theme.Rule))
                .Concat(HeroClassNames.ByName.Values.SelectMany(heroClass => Rules.ClassLines(heroClass)));

            Assert.That(lines.SelectMany(line => line.Modifiers).Select(modifier => modifier.Stat.BaseName).Where(stat => !known.Contains(stat)).Distinct(), Is.Empty);
        }

        [Test]
        public void SlateChanceAndSlateRarityFromTheTreeWork()
        {
            var plain = Hero();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.SlateChance, ModifierKind.Increased, 1));
            hero.Add(new Modifier(CombatStats.SlateRarity, ModifierKind.Increased, 4));
            var lucky = Hero(hero: hero);

            Assert.That(lucky.Slates.DropBonus, Is.EqualTo(2 * plain.Slates.DropBonus));
            int Rare(GameLoop loop) => Enumerable.Range(0, 400).Count(_ => loop.Slates.Roll(SlateRarity.Common).Rarity > SlateRarity.Common);
            Assert.That(Rare(lucky), Is.GreaterThan(Rare(plain) * 2));
        }
    }
}
