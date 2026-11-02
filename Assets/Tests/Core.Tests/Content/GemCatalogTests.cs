using System.Collections.Generic;
using System.Linq;
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
        public void MvpGemsArePrototypeSkillsAndClassSkills()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.All.Count(gem => gem.Skill?.Weapon == null && gem.Kind != GemKind.Support), Is.EqualTo(29));
            Assert.That(gems.All.Where(gem => gem.Kind == GemKind.Support).Select(gem => gem.Support), Is.All.Not.Null);
            Assert.That(gems["resonance"].Support!.AllyPower, Is.EqualTo(1.2));
            Assert.That(gems["resonance"].Support!.AfterSeconds, Is.EqualTo(5));
            Assert.That(gems.All.Count(gem => gem.Kind == GemKind.Support), Is.EqualTo(28));
            foreach (var id in new[] { "runedancer_hit_1", "runedancer_strong_1", "minstrel_aura_1", "minstrel_aura_2" })
                Assert.That(gems[id].Kind, Is.EqualTo(GemKind.Skill), id);
            Assert.That(gems["through_stride"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["march"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["lull"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems.All.Any(gem => gem.Id.StartsWith("cast_on_") || gem.Id.StartsWith("cast_with_")), Is.False);
        }

        [Test]
        public void ThroughStrideMatchesContentDraft()
        {
            var gem = GemCatalog.Load(RealFile())["through_stride"];
            var effect = (ChainHitsEffect)gem.Skill!.Effect;

            Assert.That(gem.Color, Is.EqualTo(GemColor.Red));
            Assert.That(gem.NameRu, Is.EqualTo("Сквозной Ход"));
            Assert.That(gem.Tags, Is.EqualTo(StatTags.Strike | StatTags.Skill));
            Assert.That(gem.Skill.ResourceCost, Is.EqualTo(40));
            Assert.That(gem.Skill.CooldownSeconds, Is.EqualTo(20));
            Assert.That(effect.Hits, Is.EqualTo(60));
            Assert.That(effect.NextEnemyShare, Is.EqualTo(0.5));
        }

        [Test]
        public void MarchIsAnAuraPaidWithResource()
        {
            var skill = GemCatalog.Load(RealFile())["march"].Skill!;
            var aura = (AuraEffect)skill.Effect;

            Assert.That(skill.ResourceCost, Is.EqualTo(30));
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

        [TestCase("stenography", "through_stride")]
        [TestCase("typist", "quick_stride")]
        [TestCase("touch_typing", "steady_pace")]
        [TestCase("coffee_break", "caravan_halt")]
        [TestCase("call", "blade_on_watch")]
        [TestCase("heros_diary", "prokhors_ledger")]
        [TestCase("caps_rage", "no_breather")]
        public void RenamedSkillFromASaveIsReadAsItsLoreSkill(string oldId, string newId)
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.TryGet(oldId, out var gem), Is.True);
            Assert.That(gem.Id, Is.EqualTo(newId));
        }

        [Test]
        public void SupportGemsAlreadyFitTheFormat()
        {
            var gems = GemCatalog.Load(WithExtraGem(
                "{ \"id\": \"echo_keys\", \"kind\": \"support\", \"supports\": [\"Skill\"], \"damageMult\": 1, \"manaMult\": 1, \"level\": 1, \"color\": \"red\", \"name\": { \"ru\": \"Эхо клавиш\", \"en\": \"Key Echo\" }, \"effect\": { \"type\": \"repeatEveryKeys\", \"keys\": 50 } }"));

            Assert.That(gems["echo_keys"].Kind, Is.EqualTo(GemKind.Support));
            Assert.That(gems["echo_keys"].SupportEffect, Is.EqualTo("repeatEveryKeys"));
            Assert.That(gems["echo_keys"].Support, Is.Null);
            Assert.That(gems["chain"].Support!.QueueShare, Is.EqualTo(0.3));
        }

        [Test]
        public void LevelsGiveTwoPercentPerLevel()
        {
            var levels = GemCatalog.Load(RealFile()).Levels;

            Assert.That(levels.Max, Is.EqualTo(25));
            Assert.That(levels.EffectMultiplier(1), Is.EqualTo(1));
            Assert.That(levels.EffectMultiplier(25), Is.EqualTo(1.48).Within(1e-9));
            Assert.That(levels.Experience.At(2).ToDouble(), Is.GreaterThan(levels.Experience.At(1).ToDouble()));
        }

        private static (double Cost, double Cooldown, double Duration) Numbers(SkillDef skill) =>
            (skill.ResourceCost, skill.CooldownSeconds, skill.DurationSeconds);

        [TestCase("{ \"id\": \"lull\", \"kind\": \"active\", \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"strike\", \"multiplier\": 1 } }", "gems[21].id: gem id 'lull' is used twice")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"spell\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].kind: unknown value 'spell'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"active\", \"color\": \"pink\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].color: unknown value 'pink'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"aura\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }", "gems[0]: an aura needs durationSeconds or reserve")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"keyRepeat\", \"every\": 3, \"multiplier\": 2 } }", "gems[0]: a keyRepeat skill needs durationSeconds")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"strike\", \"multiplier\": 1 }, \"native\": { \"holdDecaySeconds\": 1 } }", "gems[0].native: a native trait needs a weapon")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"fireball\" } }", "gems[0].effect.type: unknown skill effect 'fireball'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"trigger\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].kind: unknown value 'trigger'")]
        public void BrokenGemsAreReportedByPath(string gem, string message)
        {
            var error = Assert.Throws<ContentException>(() => GemCatalog.Load(WithExtraGem(gem)))!;

            Assert.That(error.Message, Does.Contain("skills.json: " + message));
        }
    }
}
