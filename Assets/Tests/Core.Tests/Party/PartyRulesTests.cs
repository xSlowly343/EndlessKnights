using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyRulesTests
    {
        private const string Valid = @"{
  ""version"": 1,
  ""maxMembers"": 4,
  ""hpPerExtraPlayer"": 1.0,
  ""presence"": { ""restAfterSeconds"": 180, ""disconnectGraceSeconds"": 30, ""rejoinWindowSeconds"": 300, ""silenceSeconds"": 6 },
  ""packets"": { ""damageBatchSeconds"": 0.5, ""stateBroadcastSeconds"": 0.5, ""presenceSeconds"": 5, ""maxMessagesPerSecond"": 40 },
  ""lateDamageSeconds"": 1.0,
  ""maxEnemyDamageTaken"": 1.0,
  ""contribution"": { ""minShare"": 0.1, ""activeInputSeconds"": 30, ""restBankMinutes"": 60, ""idlePortionShare"": 1.5, ""typingReserveShare"": 0.5 },
  ""caps"": { ""barsPerSecond"": 3, ""burstBars"": 1, ""hitBurstSeconds"": 2, ""flagsBeforeReport"": 20 },
  ""buffs"": { ""selfShare"": 1.0, ""offerSeconds"": 1.0, ""stats"": [{ ""from"": ""partyDamage"", ""to"": ""damage"", ""kind"": ""increased"" }] }
}";

        [Test]
        public void RealDataFileLoads()
        {
            var rules = PartyRules.Load(TestData.ReadGameDataFile(PartyRules.FileName));

            Assert.That(rules.MaxMembers, Is.EqualTo(4));
            Assert.That(rules.RestAfterSeconds, Is.EqualTo(180));
            Assert.That(rules.DisconnectGraceSeconds, Is.EqualTo(30));
        }

        [Test]
        public void EnemyHpGrowsLinearlyWithFighters()
        {
            var rules = PartyRules.Load(Valid);

            Assert.That(rules.HpMultiplier(1), Is.EqualTo(1));
            Assert.That(rules.HpMultiplier(2), Is.EqualTo(2));
            Assert.That(rules.HpMultiplier(4), Is.EqualTo(4));
            Assert.That(rules.HpMultiplier(0), Is.EqualTo(1));
        }

        [Test]
        public void MoreThanFourMembersIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PartyRules.Load(Valid.Replace(@"""maxMembers"": 4", @"""maxMembers"": 5")));

            Assert.That(error!.Message, Does.Contain("maxMembers"));
        }

        [Test]
        public void RejoinWindowShorterThanGraceIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PartyRules.Load(Valid.Replace(@"""rejoinWindowSeconds"": 300", @"""rejoinWindowSeconds"": 10")));

            Assert.That(error!.Message, Does.Contain("rejoinWindowSeconds"));
        }

        [Test]
        public void ContributionAndCapsAreRead()
        {
            var rules = PartyRules.Load(Valid);

            Assert.That(rules.Contribution.MinShare, Is.EqualTo(0.1));
            Assert.That(rules.Contribution.ActiveInputSeconds, Is.EqualTo(30));
            Assert.That(rules.Contribution.RestBankSeconds, Is.EqualTo(3600));
            Assert.That(rules.Contribution.IdlePortionShare, Is.EqualTo(1.5));
            Assert.That(rules.Contribution.TypingReserveShare, Is.EqualTo(0.5));
            Assert.That(rules.Caps.BarsPerSecond, Is.EqualTo(3));
            Assert.That(rules.Caps.BurstBars, Is.EqualTo(1));
            Assert.That(rules.Caps.HitBurstSeconds, Is.EqualTo(2));
            Assert.That(rules.Caps.FlagsBeforeReport, Is.EqualTo(20));
        }

        [Test]
        public void ActiveInputLongerThanRestIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PartyRules.Load(Valid.Replace(@"""activeInputSeconds"": 30", @"""activeInputSeconds"": 600")));

            Assert.That(error!.Message, Does.Contain("activeInputSeconds"));
        }

        [Test]
        public void BurstBelowOneBarIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PartyRules.Load(Valid.Replace(@"""burstBars"": 1", @"""burstBars"": 0.5")));

            Assert.That(error!.Message, Does.Contain("burstBars"));
        }

        [Test]
        public void IdlePortionBelowOneIsRejected()
        {
            var error = Assert.Throws<ContentException>(() => PartyRules.Load(Valid.Replace(@"""idlePortionShare"": 1.5", @"""idlePortionShare"": 0.8")));

            Assert.That(error!.Message, Does.Contain("idlePortionShare"));
        }
    }
}
