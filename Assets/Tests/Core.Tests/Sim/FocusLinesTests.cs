using System.Linq;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class FocusLinesTests
    {
        private static readonly FocusRules Rules = new FocusRules(1, 3, 0.02, 1500, 3, 0.05, 60, 5, 10);
        private static readonly EffectiveInput Pause = Input(0, 0);
        private static readonly EffectiveInput Mouse = Input(0, 300);

        private static EffectiveInput Input(double keys, double mousePx) =>
            new EffectiveInput(keys, 0, 0, 0, 0, 0, 0, mousePx, 0, 0, 0);

        private static FocusSystem Focus(GameState state, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            return new FocusSystem(Rules, state, hero);
        }

        private static double Grown(FocusSystem focus, GameState state, EffectiveInput input, int ticks)
        {
            var before = state.Focus;
            for (var i = 0; i < ticks; i++)
                focus.Tick(input);
            return state.Focus - before;
        }

        [Test]
        public void WithoutTheLineFocusDoesNotGrowInAPause()
        {
            var state = new GameState();
            var focus = Focus(state);

            Assert.That(Grown(focus, state, Pause, 20), Is.EqualTo(0));
        }

        [Test]
        public void TheLineGrowsFocusOnlyForTheFirstSecondsOfAPause()
        {
            var state = new GameState();
            var focus = Focus(state, new Modifier(CombatStats.FocusInPauseSeconds, ModifierKind.Flat, 2));

            Assert.That(Grown(focus, state, Pause, 20), Is.EqualTo(20 * 5 * 0.02 * GameLoop.TickSeconds).Within(1e-9));
            Assert.That(Grown(focus, state, Pause, 5), Is.EqualTo(0));
        }

        [Test]
        public void MovingTheMouseDoesNotEndThePause()
        {
            var state = new GameState();
            var focus = Focus(state, new Modifier(CombatStats.FocusInPauseSeconds, ModifierKind.Flat, 60));

            var grown = Grown(focus, state, Mouse, 10);

            Assert.That(grown, Is.EqualTo(10 * (5 * GameLoop.TickSeconds + 300.0 / 1500) * 0.02).Within(1e-9));
        }

        [Test]
        public void TheLineWaitsForItsCondition()
        {
            var state = new GameState();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.FocusInPauseSeconds, ModifierKind.Flat, 3, StatTags.None, ModifierPool.General, ModifierCondition.Fire));
            var focus = new FocusSystem(Rules, state, hero);

            Assert.That(Grown(focus, state, Pause, 5), Is.EqualTo(0));
            state.Focus = 1;
            state.FocusPauseTicks = 0;
            hero.SetConditions(ModifierConditions.Bit(ModifierCondition.Fire));
            Assert.That(Grown(focus, state, Pause, 5), Is.GreaterThan(0));
        }

        [Test]
        public void AurasMinionsPoisonAndResourceRaiseTheCap()
        {
            var state = new GameState { ActiveAuras = 2, PoisonStacks = 20, ResourceFill = 0.5 };
            state.Minions[0].TicksLeft = 10;
            var focus = Focus(state,
                new Modifier(CombatStats.FocusMaxPerAura, ModifierKind.Flat, 1),
                new Modifier(CombatStats.FocusMaxPerMinion, ModifierKind.Flat, 2),
                new Modifier(CombatStats.FocusMaxPerPoisonStack, ModifierKind.Flat, 0.1),
                new Modifier(CombatStats.FocusMaxPerResourceFill, ModifierKind.Flat, 2));

            Assert.That(focus.Max, Is.EqualTo(3 + 2 + 2 + 2 + 1).Within(1e-9));
        }

        [Test]
        public void TheCountBonusStopsAtItsCap()
        {
            var state = new GameState { PoisonStacks = 500 };
            var focus = Focus(state, new Modifier(CombatStats.FocusMaxPerPoisonStack, ModifierKind.Flat, 0.1));

            Assert.That(focus.Max, Is.EqualTo(3 + 10));
        }

        [Test]
        public void ReadingAndResourceAreConditionsButNotAllyMarks()
        {
            Assert.That(ModifierConditions.IsAlly(ModifierCondition.Reading), Is.False);
            Assert.That(ModifierConditions.IsAlly(ModifierCondition.HasResource), Is.False);
            Assert.That(ModifierConditions.IsAlly(ModifierCondition.AllyBlood), Is.True);
        }

        [Test]
        public void OnlyTheLinesWithoutStacksStillWait()
        {
            var data = GameData.Load(TestData.ReadGameDataFile);
            var waiting = data.ClassTrees.Values
                .SelectMany(tree => tree.Nodes)
                .SelectMany(node => new[] { node.Template.First, node.Template.Second, node.Template.Third })
                .Select(line => line.Waits ?? string.Empty)
                .Where(name => name.StartsWith("focusInPause") || name.StartsWith("focusMaxPer") || name.StartsWith("focusMaxOn"));

            Assert.That(waiting, Is.SubsetOf(new[] { "focusMaxPerBlood", "focusMaxPerCold", "focusMaxPerCurse" }));
        }
    }
}
