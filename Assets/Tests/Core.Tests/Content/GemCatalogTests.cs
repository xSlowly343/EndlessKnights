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
        public void MvpGemsArePrototypeSkillsClassSkillsAndTriggers()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.All.Count, Is.EqualTo(66));
            foreach (var id in new[] { "shield_bash", "odins_fury", "song", "guitar_solo" })
                Assert.That(gems[id].Kind, Is.EqualTo(GemKind.Skill), id);
            Assert.That(gems["stenography"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["march"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["lull"].Kind, Is.EqualTo(GemKind.Skill));
            Assert.That(gems["voodoo_needle"].Skill!.Effect, Is.InstanceOf<EnemyDebuffEffect>());
            Assert.That(gems["cast_on_timer"].Kind, Is.EqualTo(GemKind.Trigger));
        }

        [Test]
        public void StenographyMatchesContentDraft()
        {
            var gem = GemCatalog.Load(RealFile())["stenography"];
            var effect = (ChainHitsEffect)gem.Skill!.Effect;

            Assert.That(gem.Color, Is.EqualTo(GemColor.Red));
            Assert.That(gem.NameRu, Is.EqualTo("Стенография"));
            Assert.That(gem.Tags, Is.EqualTo(StatTags.Keyboard | StatTags.Skill));
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
        public void VikingStartingSkillsMatchSkillsDoc()
        {
            var gems = GemCatalog.Load(RealFile());
            var bash = gems["shield_bash"].Skill!;
            var fury = gems["odins_fury"].Skill!;

            Assert.That(bash.ResourceCost, Is.EqualTo(15));
            Assert.That(bash.CooldownSeconds, Is.EqualTo(4));
            Assert.That(((StrikeEffect)bash.Effect).Multiplier, Is.EqualTo(4));
            Assert.That(fury.ResourceCost, Is.EqualTo(50));
            Assert.That(fury.CooldownSeconds, Is.EqualTo(25));
            Assert.That(((StrikeEffect)fury.Effect).Multiplier, Is.EqualTo(5));
        }

        [Test]
        public void MetalheadStartingSkillsMatchSkillsDoc()
        {
            var gems = GemCatalog.Load(RealFile());
            var song = gems["song"].Skill!;
            var solo = gems["guitar_solo"].Skill!;

            Assert.That(song.ResourceCost, Is.EqualTo(30));
            Assert.That(song.CooldownSeconds, Is.EqualTo(5));
            Assert.That(song.DurationSeconds, Is.EqualTo(20));
            Assert.That(((AuraEffect)song.Effect).Modifiers, Is.EqualTo(new[]
            {
                new Modifier(new StatId("damage"), ModifierKind.Increased, 0.15),
                new Modifier(new StatId("gold"), ModifierKind.Increased, 0.10)
            }));
            Assert.That(solo.ResourceCost, Is.EqualTo(80));
            Assert.That(solo.CooldownSeconds, Is.EqualTo(35));
            Assert.That(solo.DurationSeconds, Is.EqualTo(10));
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
        public void TimerTriggerCastsEveryThirtySecondsWithLongerCooldown()
        {
            var trigger = GemCatalog.Load(RealFile())["cast_on_timer"].Trigger!;

            Assert.That(trigger.Condition, Is.EqualTo(TriggerCondition.Every));
            Assert.That(trigger.Value, Is.EqualTo(30));
            Assert.That(trigger.CooldownMultiplier, Is.EqualTo(1.5));
        }

        [Test]
        public void FourteenTriggersAndNoneListensToKeys()
        {
            var gems = GemCatalog.Load(RealFile());
            var triggers = 0;
            foreach (var gem in gems.All)
            {
                if (gem.Kind != GemKind.Trigger)
                    continue;
                triggers++;
                Assert.That(gem.Trigger!.CooldownMultiplier, Is.EqualTo(1.5), gem.Id);
            }

            Assert.That(triggers, Is.EqualTo(14));
            Assert.That(System.Enum.GetNames(typeof(TriggerCondition)), Has.No.Member("Enter"));
        }

        [Test]
        public void OldCastOnEnterGemIsReadAsTheTimerTrigger()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(gems.TryGet("cast_on_enter", out var gem), Is.True);
            Assert.That(gem.Id, Is.EqualTo("cast_on_timer"));
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

        [Test]
        public void VikingAndMetalheadHaveSevenClassSkillsEachOnTheirWeapon()
        {
            var gems = GemCatalog.Load(RealFile());
            var axe = new[] { "shield_bash", "odins_fury", "battle_cry", "throwing_axe", "blood_harvest", "shield_wall", "ragnarok_roar" };
            var guitar = new[] { "song", "guitar_solo", "power_chord", "ballad", "mosh_pit", "amp_eleven", "stage_dive" };

            foreach (var id in axe)
                Assert.That(gems[id].Skill!.Weapon, Is.EqualTo("axe"), id);
            foreach (var id in guitar)
                Assert.That(gems[id].Skill!.Weapon, Is.EqualTo("electric_guitar"), id);
        }

        [Test]
        public void NewVikingSkillsMatchSkillsDoc()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(Numbers(gems["battle_cry"].Skill!), Is.EqualTo((30d, 30d, 15d)));
            Assert.That(Numbers(gems["throwing_axe"].Skill!), Is.EqualTo((10d, 3d, 0d)));
            Assert.That(Numbers(gems["shield_wall"].Skill!), Is.EqualTo((40d, 35d, 10d)));
            Assert.That(Numbers(gems["ragnarok_roar"].Skill!), Is.EqualTo((70d, 45d, 8d)));
            Assert.That(gems["blood_harvest"].Skill!.IsPassive, Is.True);
            Assert.That(((StrikeEffect)gems["throwing_axe"].Skill!.Effect).SkipCurrent, Is.True);
            Assert.That(((StoreHitsEffect)gems["shield_wall"].Skill!.Effect).ReleaseShare, Is.EqualTo(0.5));
            Assert.That(((QueueHitsEffect)gems["ragnarok_roar"].Skill!.Effect).Share, Is.EqualTo(0.4));
            Assert.That(gems["shield_bash"].Skill!.Native.HoldDecaySeconds, Is.EqualTo(5));
            Assert.That(gems["odins_fury"].Skill!.Native.SpendAllMax, Is.EqualTo(10));
            Assert.That(gems["blood_harvest"].Skill!.Native.HoldStacksAboveResource, Is.EqualTo(50));
        }

        [Test]
        public void NewMetalheadSkillsMatchSkillsDoc()
        {
            var gems = GemCatalog.Load(RealFile());

            Assert.That(Numbers(gems["power_chord"].Skill!), Is.EqualTo((15d, 4d, 0d)));
            Assert.That(Numbers(gems["ballad"].Skill!), Is.EqualTo((30d, 30d, 15d)));
            Assert.That(Numbers(gems["mosh_pit"].Skill!), Is.EqualTo((50d, 30d, 10d)));
            Assert.That(Numbers(gems["stage_dive"].Skill!), Is.EqualTo((60d, 40d, 0d)));
            Assert.That(((AuraEffect)gems["amp_eleven"].Skill!.Effect).Reserve, Is.EqualTo(25));
            Assert.That(gems["amp_eleven"].Skill!.IsPassive, Is.True);
            Assert.That(gems["song"].Skill!.Family, Is.EqualTo("song"));
            Assert.That(gems["ballad"].Skill!.Family, Is.EqualTo("song"));
            Assert.That(((StrikeEffect)gems["stage_dive"].Skill!.Effect).KillRefundShare, Is.EqualTo(0.5));
        }

        private static (double Cost, double Cooldown, double Duration) Numbers(SkillDef skill) =>
            (skill.ResourceCost, skill.CooldownSeconds, skill.DurationSeconds);

        [TestCase("{ \"id\": \"lull\", \"kind\": \"trigger\", \"color\": \"white\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"condition\": { \"type\": \"every\", \"value\": 1 }, \"cooldownMultiplier\": 1 }", "gems[44].id: gem id 'lull' is used twice")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"spell\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].kind: unknown value 'spell'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"pink\", \"name\": { \"ru\": \"x\", \"en\": \"x\" } }", "gems[0].color: unknown value 'pink'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"aura\", \"modifiers\": [] } }", "gems[0]: an aura needs durationSeconds or reserve")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"storeHits\", \"releaseShare\": 0.5 } }", "gems[0]: a storeHits skill needs durationSeconds")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"strike\", \"multiplier\": 1 }, \"native\": { \"holdDecaySeconds\": 1 } }", "gems[0].native: a native trait needs a weapon")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"stacks\", \"on\": \"sneeze\", \"perStack\": { \"stat\": \"damage\", \"kind\": \"increased\", \"value\": 0.1 }, \"max\": 1 } }", "gems[0].effect.on: unknown value 'sneeze'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"skill\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"effect\": { \"type\": \"fireball\" } }", "gems[0].effect.type: unknown skill effect 'fireball'")]
        [TestCase("{ \"id\": \"x\", \"kind\": \"trigger\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"condition\": { \"type\": \"sneeze\" }, \"cooldownMultiplier\": 1 }", "gems[0].condition.type: unknown value 'sneeze'")]
        public void BrokenGemsAreReportedByPath(string gem, string message)
        {
            var error = Assert.Throws<ContentException>(() => GemCatalog.Load(WithExtraGem(gem)))!;

            Assert.That(error.Message, Does.Contain("gems.json: " + message));
        }
    }
}
