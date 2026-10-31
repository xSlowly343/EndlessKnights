using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PartyNodeScoreTests
    {
        private static double Score(bool inParty, Modifier modifier) =>
            new NodeScore(StatTags.Keyboard, Game.Core.Content.Tree.ClassAttributeDef.None, inParty).Of(new[] { modifier });

        [Test]
        public void AllyMarkLinesCountOnlyInAParty()
        {
            var line = new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, ModifierCondition.AllyFire);

            Assert.That(Score(false, line), Is.EqualTo(0));
            Assert.That(Score(true, line), Is.GreaterThan(0));
        }

        [Test]
        public void PartyAndAnyAllyMarkLinesCountOnlyInAParty()
        {
            foreach (var condition in new[] { ModifierCondition.InParty, ModifierCondition.AnyAllyMark })
            {
                var line = new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, condition);

                Assert.That(Score(false, line), Is.EqualTo(0), condition.ToString());
                Assert.That(Score(true, line), Is.GreaterThan(0), condition.ToString());
            }
        }

        [Test]
        public void ReactionUnlocksAndTheSecondResponseCountOnlyInAParty()
        {
            var unlock = new Modifier(CombatStats.ReactionSteam, ModifierKind.Flat, 1);
            var response = new Modifier(CombatStats.SecondResponse, ModifierKind.Flat, 1);

            Assert.That(Score(false, unlock), Is.EqualTo(0));
            Assert.That(Score(true, unlock), Is.GreaterThan(0));
            Assert.That(Score(true, response), Is.GreaterThan(0));
        }
    }
}
