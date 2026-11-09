using System.Linq;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PierceSourceTests
    {
        [Test]
        public void FourRiftArtifactsCarryOnePierceMechanicEach()
        {
            var catalog = TestWorld.Real.Artifacts;
            var stats = new[] { "pierceShare", "splashShare", "stageSkipChance", "bossPierce" };
            foreach (var group in new[] { 1, 3, 4, 5 })
            {
                var pierce = catalog.All.Where(a => a.Group == group && a.PerLevel.Any(m => stats.Contains(m.Stat.Name))).ToList();
                Assert.That(pierce.Count, Is.EqualTo(1), "group " + group);
                Assert.That(pierce[0].MaxLevel, Is.GreaterThan(0), "a pierce artifact stops at its max level");
            }
        }

        [Test]
        public void ThePiercingSupportSplashesIntoTheNextTwoEnemies()
        {
            Assert.That(TestWorld.Real.Gems.TryGet("piercing", out var gem), Is.True);
            Assert.That(gem.Support!.Kind, Is.EqualTo(SupportKind.Piercing));
            Assert.That(gem.Support.QueueTargets, Is.EqualTo(2));
            Assert.That(gem.Support.QueueShare, Is.EqualTo(0.1));
        }

        [Test]
        public void TheClanTreeAndTheSlatesOfferPierce()
        {
            Assert.That(TestWorld.Real.ClanTree.All.Any(n => n.Modifiers.Any(m => m.Stat.Name == "pierceShare")), Is.True);
            var theme = TestWorld.Real.Slates.Themes.Single(t => t.Id == "pierce");
            Assert.That(theme.Rule!.Modifiers.Single().Stat, Is.EqualTo(CombatStats.EnemySkip), "the rare legendary rule skips an enemy");
        }
    }
}
