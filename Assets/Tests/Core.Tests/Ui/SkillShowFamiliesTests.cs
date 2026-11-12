using Game.Core.Content.Gems;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class SkillShowFamiliesTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        [TestCase("runedancer_hit_1", SkillShowFamily.Hit)]
        [TestCase("runedancer_queue_1", SkillShowFamily.Queue)]
        [TestCase("runedancer_auto_1", SkillShowFamily.Auto)]
        [TestCase("runedancer_strong_1", SkillShowFamily.Strong)]
        public void TheFamilyIsReadFromTheRootSkillId(string id, SkillShowFamily family)
        {
            Assert.That(SkillShowFamilies.Of(Gem(id)), Is.EqualTo(family));
        }

        [Test]
        public void EveryFormOfAFamilyShowsAsItsRoot()
        {
            foreach (var gem in TestWorld.Data.Gems.All)
            {
                if (!gem.Evolves)
                    continue;
                Assert.That(SkillShowFamilies.Of(gem), Is.EqualTo(SkillShowFamilies.Of(gem.Root)), gem.Id);
            }
        }

        [Test]
        public void EveryClassFamilyHasAShowFamilyOfItsOwn()
        {
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var gem in TestWorld.Data.Gems.All)
            {
                if (!gem.Evolves || !ReferenceEquals(gem.Root, gem))
                    continue;
                var owner = gem.Id.Substring(0, gem.Id.IndexOf('_'));
                seen.TryGetValue(owner, out var mask);
                seen[owner] = mask | 1 << (int)SkillShowFamilies.Of(gem);
            }

            Assert.That(seen, Is.Not.Empty);
            foreach (var pair in seen)
                Assert.That(pair.Value, Is.EqualTo((1 << SkillShowFamilies.Count) - 1), pair.Key);
        }

        [Test]
        public void ASkillOutsideTheFamiliesWithALongCooldownShowsAsHeavy()
        {
            var gem = Gem("through_stride");

            Assert.That(gem.Skill!.CooldownSeconds, Is.GreaterThanOrEqualTo(20));
            Assert.That(SkillShowFamilies.Of(gem), Is.EqualTo(SkillShowFamily.Strong));
        }
    }
}
