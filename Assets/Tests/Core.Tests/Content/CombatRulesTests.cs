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
            Assert.That(rules.ClickDamage, Is.EqualTo(6));
            Assert.That(rules.MouseDamage, Is.EqualTo(4));
            Assert.That(rules.MousePxPerHit, Is.EqualTo(400));
        }

        [TestCase("\"version\": 1", "\"version\": 2", "version: version 2 is not supported, expected 1")]
        [TestCase("\"damage\": 10", "\"damage\": -1", "hits.keyboard.damage: -1 is outside the allowed range")]
        [TestCase("\"pxPerHit\": 400", "\"pxPerHit\": 0", "hits.mouse.pxPerHit: 0 is outside the allowed range")]
        public void BrokenRulesAreReportedByPath(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => CombatRules.Load(RealFile().Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("combat.json: " + message));
        }
    }
}
