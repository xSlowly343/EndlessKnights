using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PartyNodeScoreTests
    {
        private static double Score(bool inParty, Modifier modifier) =>
            new NodeScore(StatTags.Strike, Game.Core.Content.Tree.ClassAttributeDef.None, inParty).Of(new[] { modifier });

        [Test]
        public void AllyMarkLinesCountOnlyInAParty()
        {
            var line = new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, ModifierCondition.AllyPoison);

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
        public void ConstellationStrengthCountsOnlyInAParty()
        {
            var line = new Modifier(CombatStats.ConstellationEffect, ModifierKind.Increased, 0.1);

            Assert.That(Score(false, line), Is.EqualTo(0));
            Assert.That(Score(true, line), Is.GreaterThan(0));
        }
    }
}
