using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class GemCatalogTests
    {
        private static string RealFile() => TestData.ReadGameDataFile(GemCatalog.FileName);

        private static string WithExtraGem(string gemJson) => RealFile().Replace("\"gems\": [", "\"gems\": [\n    " + gemJson + ",");

        [Test]
        public void MvpGemsAreThreeSkillsAndOneTrigger()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.All.Count, Is.EqualTo(4));
            Assert.That(gems["stenography"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["march"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["lull"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["cast_on_enter"].Kind, Is.EqualTo(GemKind.Trigger));
        }

        [Test]
        public void StenographyMatchesContentDraft()
        {
            var gem = GemCatalog.Load(RealFile())["stenography"];
            var effect = (ChainHitsEffect)gem.Skill!.Effect;

            Assert.That(gem.Color, Is.EqualTo(GemColor.Red));
            Assert.That(gem.NameRu, Is.EqualTo("Стенография"));
            Assert.That(gem.Tags, Is.EqualTo(StatTags.Keyboard | StatTags.Skill));
            Assert.That(gem.Skill.EnergyCost, Is.EqualTo(40));
            Assert.That(gem.Skill.CooldownSeconds, Is.EqualTo(20));
            Assert.That(effect.Hits, Is.EqualTo(60));
            Assert.That(effect.NextEnemyShare, Is.EqualTo(0.5));
        }

        [Test]
        public void MarchIsANoteAura()
        {
            var skill = GemCatalog.Load(RealFile())["march"].Skill!;
            var aura = (AuraEffect)skill.Effect;

            Assert.That(skill.NoteCost, Is.EqualTo(4));
            Assert.That(skill.EnergyCost, Is.EqualTo(0));
            Assert.That(skill.DurationSeconds, Is.EqualTo(30));
            Assert.That(aura.Modifiers, Is.EqualTo(new[] { new Modifier(new StatId("damage"), ModifierKind.Increased, 0.25) }));
        }

        [Test]
        public void LullIsAPassiveIdleStrike()
        {
            var skill = GemCatalog.Load(RealFile())["lull"].Skill!;
            var effect = (IdleStrikeEffect)skill.Effect;

            Assert.That(skill.IsPassive, Is.True);
            Assert.That(skill.CooldownSeconds, Is.EqualTo(60));
            Assert.That(effect.ChargePerSecond, Is.EqualTo(0.05));
            Assert.That(effect.MaxChargeSeconds, Is.EqualTo(60));
        }

        [Test]
        public void CastOnEnterTriggerCostsLongerCooldown()
        {
            var trigger = GemCatalog.Load(RealFile())["cast_on_enter"].Trigger!;

            Assert.That(trigger.Condition, Is.EqualTo(TriggerCondition.Enter));
            Assert.That(trigger.CooldownMultiplier, Is.EqualTo(1.5));
        }

        [Test]
        public void SupportGemsAlreadyFitTheFormat()
        {
            var gems = GemCatalog.Load(WithExtraGem(
                "{ \"id\": \"echo_keys\", \"kind\": \"support\", \"color\": \"red\", \"name\": { \"ru\": \"Эхо клавиш\", \"en\": \"Key Echo\" }, \"effect\": { \"type\": \"repeatEveryKeys\", \"keys\": 50 } }"));

            Assert.That(gems["echo_keys"].Kind, Is.EqualTo(GemKind.Support));
            Assert.That(gems["echo_keys"].SupportEffect, Is.EqualTo("repeatEveryKeys"));
        }

        [Test]
        public void LevelsGiveTwoPercentPerLevel()
        {
            var levels = GemCatalog.Load(RealFile()).Levels;

            Assert.That(levels.Max, Is.EqualTo(20));
            Assert.That(levels.EffectMultiplier(1), Is.EqualTo(1));
            Assert.That(levels.EffectMultiplier(20), Is.EqualTo(1.38).Within(1e-9));
            Assert.That(levels.Experience.At(2).ToDouble(), Is.GreaterThan(levels.Experience.At(1).ToDouble()));
        }

        [TestCase("{ \"id\": \"lull\", \"kind\": \"trigger\", \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"condition\": { \"type\": \"enter\" }, \"cooldownMultiplier\": 1 }", "gems[3].id: gem id 'lull' is used twice")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"spell\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].kind: unknown value 'spell'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"pink\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].color: unknown value 'pink'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": { \"energy\": 5, \"notes\": 2 }, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }", "gems[0].cost: a skill costs either energy or notes")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }", "gems[0]: an aura needs durationSeconds")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"fireball\" } }", "gems[0].effect.type: unknown skill effect 'fireball'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"trigger\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"condition\": { \"type\": \"sneeze\" }, \"cooldownMultiplier\": 1 }", "gems[0].condition.type: unknown value 'sneeze'")]
        public void BrokenGemsAreReportedByPath(string gem, string message)
        {
            var error = Assert.Throws<ContentException>(() => GemCatalog.Load(WithExtraGem(gem)))!;

            Assert.That(error.Message, Does.Contain("gems.json: " + message));
        }

        [Test]
        public void NoteCostAboveMelodySizeIsRejected()
        {
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
                files[name] = TestData.ReadGameDataFile(name);
            files[GemCatalog.FileName] = files[GemCatalog.FileName].Replace("\"notes\": 4", "\"notes\": 9");

            var error = Assert.Throws<ContentException>(() => GameData.Load(name => files[name]))!;

            Assert.That(error.Message, Does.Contain("gems.json: march: costs 9 notes"));
        }
    }
}
