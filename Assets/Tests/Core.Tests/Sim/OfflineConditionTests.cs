using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OfflineConditionTests
    {
        private static double[] Shares(ModifierCondition condition, double share)
        {
            var shares = new double[64];
            shares[(int)condition] = share;
            return shares;
        }

        [Test]
        public void ConditionalLinesCountByTheirShareOfTime()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.Increased, 0.5, StatTags.Gold));
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.Increased, 1.0, StatTags.Gold, when: ModifierCondition.Boss));
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.More, 1.0, StatTags.Gold, when: ModifierCondition.MouseMoving));

            Assert.That(hero.ExpectedValue(CombatStats.Gold, 1, StatTags.Gold, new double[64]), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(hero.ExpectedValue(CombatStats.Gold, 1, StatTags.Gold, Shares(ModifierCondition.Boss, 0.2)), Is.EqualTo(1.7).Within(1e-9));
            Assert.That(hero.ExpectedValue(CombatStats.Gold, 1, StatTags.Gold, Shares(ModifierCondition.MouseMoving, 0.5)), Is.EqualTo(1.5 * 1.5).Within(1e-9));
            Assert.That(hero.ConditionalFactor(CombatStats.Gold, 1, StatTags.Gold, Shares(ModifierCondition.Boss, 0.2)), Is.EqualTo(1.7 / 1.5).Within(1e-9));
            Assert.That(hero.ConditionalFactor(CombatStats.Gold, 1, StatTags.Gold, new double[64]), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void TheGameRemembersHowOftenEachConditionHoldsAndSavesIt()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            for (var i = 0; i < 600; i++)
            {
                input.MoveMouse(50);
                loop.Tick();
            }
            var moving = loop.State.ConditionShares[(int)ModifierCondition.MouseMoving];
            Assert.That(moving, Is.GreaterThan(0.01), "about 1.7% after one minute of a one-hour window");
            Assert.That(loop.State.ConditionShares[(int)ModifierCondition.FocusFull], Is.LessThan(moving));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.ConditionShares[(int)ModifierCondition.MouseMoving], Is.EqualTo(moving).Within(1e-12));
        }

        [Test]
        public void OfflineGoldUsesTheSharesInsteadOfTheLastTick()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.Increased, 1.0, StatTags.Gold, when: ModifierCondition.MouseMoving));
            var offline = new OfflineCalculator(TestWorld.Data, hero);
            var state = new GameState { Stage = 5, RecentDps = BigNumber.FromDouble(1e6) };
            var without = offline.Simulate(state, 600, BigNumber.FromDouble(1e6), false).Gold.ToDouble();
            state.ConditionShares[(int)ModifierCondition.MouseMoving] = 0.5;
            var with = offline.Simulate(state, 600, BigNumber.FromDouble(1e6), false).Gold.ToDouble();
            Assert.That(with / without, Is.EqualTo(1.5).Within(1e-6));
        }
    }
}
