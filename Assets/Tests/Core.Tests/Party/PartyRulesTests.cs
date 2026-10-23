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
  ""packets"": { ""damageBatchSeconds"": 0.5, ""stateBroadcastSeconds"": 0.5, ""presenceSeconds"": 5 },
  ""lateDamageSeconds"": 1.0,
  ""maxEnemyDamageTaken"": 1.0,
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
    }
}
