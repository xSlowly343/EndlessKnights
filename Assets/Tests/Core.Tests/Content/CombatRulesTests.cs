using Game.Core.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class CombatRulesTests
    {
        private static string RealFile() => TestData.ReadGameDataFile(CombatRules.FileName);

        [Test]
        public void RealDataFileMatchesContentDraft()
        {
            var rules = CombatRules.Load(RealFile());

            Assert.That(rules.KeyDamage, Is.EqualTo(10));
            Assert.That(rules.MouseDamage, Is.EqualTo(3.4));
            Assert.That(rules.MousePxPerHit, Is.EqualTo(1000));
            Assert.That(rules.Focus.Min, Is.EqualTo(1));
            Assert.That(rules.Focus.Max, Is.EqualTo(3));
            Assert.That(rules.Focus.PerEvent, Is.EqualTo(0.02));
            Assert.That(rules.Focus.ResetAfterIdleSeconds, Is.EqualTo(3));
            Assert.That(rules.Resources.Max, Is.EqualTo(100));
            Assert.That(rules.Resources.For("viking").PerKey, Is.EqualTo(2));
            Assert.That(rules.Resources.For("viking").DecayPerSecond, Is.EqualTo(10));
            Assert.That(rules.Resources.For("viking").DecayAfterKeylessSeconds, Is.EqualTo(3));
            Assert.That(rules.Resources.For("viking").MouseHoldsDecay, Is.True);
            Assert.That(rules.Resources.For("metalhead").PerKey, Is.EqualTo(1));
            Assert.That(rules.Resources.For("metalhead").DecayPerSecond, Is.EqualTo(0));
            Assert.That(rules.CritChance, Is.EqualTo(0.05));
            Assert.That(rules.CritMultiplier, Is.EqualTo(2));
            Assert.That(rules.Offline.CapHours, Is.EqualTo(8));
            Assert.That(rules.Offline.DpsShare, Is.EqualTo(0.3));
        }

        [Test]
        public void EveryResourceClassIsACharacter()
        {
            var rules = CombatRules.Load(RealFile());

            foreach (var name in rules.Resources.Classes)
                Assert.That(Game.Core.Sim.HeroClassNames.ByName.ContainsKey(name), Is.True, name);
        }

        [Test]
        public void CharacterWithoutOwnResourceUsesTheDefault()
        {
            var rules = CombatRules.Load(RealFile());

            Assert.That(rules.Resources.For("gravedigger"), Is.SameAs(rules.Resources.Fallback));
        }

        [Test]
        public void FastestMouseGivesAboutAsMuchFocusAsFastestClicking()
        {
            var rules = CombatRules.Load(RealFile());
            var caps = InputCaps.Load(TestData.ReadGameDataFile(InputCaps.FileName));

            Assert.That(caps.MaxMousePxPerSecond / rules.Focus.MousePxPerEvent, Is.EqualTo(caps.Clicks.MaxEffectivePerSecond).Within(0.5));
        }

        [TestCase("\"version\": 2", "\"version\": 3", "version: version 3 is not supported, expected 2")]
        [TestCase("\"damage\": 10", "\"damage\": -1", "hits.keyboard.damage: -1 is outside the allowed range")]
        [TestCase("\"pxPerHit\": 1000", "\"pxPerHit\": 0", "hits.mouse.pxPerHit: 0 is outside the allowed range")]
        [TestCase("\"max\": 3", "\"max\": 0.5", "focus.max: must not be lower than min (1)")]
        [TestCase("\"chance\": 0.05", "\"chance\": 1.5", "crit.chance: 1.5 is outside the allowed range")]
        public void BrokenRulesAreReportedByPath(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => CombatRules.Load(RealFile().Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("combat.json: " + message));
        }
    }
}
