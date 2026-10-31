using System;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyCodecVectorTests
    {
        private static readonly string VectorPath = Path.Combine("Server", "test-vectors", "party-codec.json");
        private readonly byte[] _buffer = new byte[PartyCodec.MaxPacketBytes];

        [Test]
        public void ServerCodecVectorsMatchTheCoreCodec()
        {
            var json = Build();
            var path = PartyVectorTests.RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, json);
                Assert.Pass("vectors written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(json), $"vectors are stale, run the tests with {PartyVectorTests.UpdateVariable}=1");
        }

        private string Build()
        {
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 777);
            encounter.Join(76561198000000001UL);
            encounter.Join(76561198000000002UL);
            encounter.SetHeroClass(0, 9);
            encounter.SetHeroClass(1, 6);
            encounter.ApplyDamage(0, 1, 0.75);
            encounter.ApplyEnemyEffect(-123456, 0.15, 10, 1);
            encounter.ApplyEnemyEffect(42, 0.25, 4);
            for (var i = 0; i < 32; i++)
                encounter.Advance();
            encounter.WeakenNext(2, 0.2, 0.25);
            encounter.ApplyDamage(1, 1, 5);
            encounter.SetPeaceful(1, true);
            encounter.JoinGuest(76561198000000003UL, 30);

            var builder = new StringBuilder("{\n  \"version\": 1,\n");
            builder.Append("  \"seed\": \"777\",\n");
            builder.Append("  \"script\": [\"join 76561198000000001\", \"join 76561198000000002\", \"class 0 9\", \"class 1 6\", \"damage 0 1 0.75\", \"effect -123456 0.15 10 1\", \"effect 42 0.25 4\", \"advance 32\", \"weaken 2 0.2 0.25\", \"damage 1 1 5\", \"peaceful 1 true\", \"guest 76561198000000003 30\"],\n");
            builder.Append("  \"encode\": {\n");
            builder.Append("    \"encounterState\": { \"sequence\": 5, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, EncounterStateMessage.Of(encounter, 5)))).Append("\" },\n");
            var busy = EncounterStateMessage.Of(encounter, 6);
            busy.SetRecent(1, 0.25f);
            busy.SetHits(0, 7);
            busy.SetHits(2, 300);
            busy.SetMarks(1, 0x21);
            builder.Append("    \"busyState\": { \"sequence\": 6, \"recent\": [0, 0.25, 0, 0], \"hits\": [7, 0, 300, 0], \"marks\": [0, 33, 0, 0], \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, busy))).Append("\" },\n");
            builder.Append("    \"roster\": { \"version\": 3, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, RosterMessage.Of(encounter, 3)))).Append("\" },\n");
            var killed = new PartyEvent(PartyEventKind.EnemyKilled, 12, 4, PartyEnemyKind.Boss, 0.625, 0.375, 0, 0, 5, 0.25, 6.5);
            builder.Append("    \"enemyKilled\": { \"tick\": 12, \"enemyId\": 4, \"enemyKind\": 1, \"shares\": [0.625, 0.375, 0, 0], \"rewarded\": 5, \"loop\": 0.25, \"saved\": 6.5, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, EnemyKilledMessage.Of(killed)))).Append("\" },\n");
            builder.Append("    \"moment\": { \"kind\": 4, \"tick\": 300, \"enemyId\": 11, \"enemyKind\": 1, \"bossTicksLeft\": 0, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new EncounterMomentMessage { Kind = PartyEventKind.BossFailed, Tick = 300, EnemyId = 11, BossTicksLeft = 0 }))).Append("\" }\n");
            builder.Append("  },\n  \"decode\": {\n");
            builder.Append("    \"hello\": { \"heroClass\": 9, \"stage\": 1234567890123, \"sharePerSecondCap\": 2.5, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new HelloMessage { HeroClass = 9, Stage = 1234567890123, SharePerSecondCap = 2.5 }))).Append("\" },\n");
            builder.Append("    \"damageBatch\": { \"sequence\": 17, \"enemyId\": 3, \"keys\": 0.5, \"mouse\": 0.25, \"skills\": 0.125, \"dot\": 0.0625, \"summons\": 0.03125, \"hits\": 9, \"crits\": 2, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new DamageBatchMessage { Sequence = 17, EnemyId = 3, Keys = 0.5, Mouse = 0.25, Skills = 0.125, Dot = 0.0625, Summons = 0.03125, Hits = 9, Crits = 2 }))).Append("\" },\n");
            builder.Append("    \"castEffect\": { \"effectId\": -99, \"onEnemy\": true, \"strength\": 0.15, \"seconds\": 10, \"timerSlow\": 0.3, \"repeatSeconds\": 0, \"weakenEnemies\": 0, \"weakenShare\": 0, \"weakenGold\": 0, \"end\": false, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new CastEffectMessage { EffectId = -99, OnEnemy = true, Strength = 0.15, Seconds = 10, TimerSlow = 0.3 }))).Append("\" },\n");
            builder.Append("    \"repeatCast\": { \"effectId\": 12, \"onEnemy\": true, \"strength\": 0, \"seconds\": 0, \"timerSlow\": 0, \"repeatSeconds\": 7, \"weakenEnemies\": 0, \"weakenShare\": 0, \"weakenGold\": 0, \"end\": false, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new CastEffectMessage { EffectId = 12, OnEnemy = true, RepeatSeconds = 7 }))).Append("\" },\n");
            builder.Append("    \"weakenCast\": { \"effectId\": 31, \"onEnemy\": true, \"strength\": 0.15, \"seconds\": 4, \"timerSlow\": 0, \"repeatSeconds\": 0, \"weakenEnemies\": 5, \"weakenShare\": 0.25, \"weakenGold\": 0.2, \"end\": false, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new CastEffectMessage { EffectId = 31, OnEnemy = true, WeakenEnemies = 5, WeakenShare = 0.25, WeakenGold = 0.2, Strength = 0.15, Seconds = 4 }))).Append("\" },\n");
            builder.Append("    \"endCast\": { \"effectId\": 33, \"onEnemy\": true, \"strength\": 0, \"seconds\": 0, \"timerSlow\": 0, \"repeatSeconds\": 0, \"weakenEnemies\": 0, \"weakenShare\": 0, \"weakenGold\": 0, \"end\": true, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new CastEffectMessage { EffectId = 33, OnEnemy = true, End = true }))).Append("\" },\n");
            builder.Append("    \"bossRequest\": { \"hex\": \"").Append(Hex(PartyCodec.EncodeBossRequest(_buffer))).Append("\" },\n");
            builder.Append("    \"marks\": { \"marks\": 384, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new MarksMessage { Marks = 384 }))).Append("\" },\n");
            builder.Append("    \"worldRequest\": { \"world\": 1, \"cancel\": false, \"riftId\": 1761652800, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new WorldRequestMessage { World = PartyWorld.Rift, RiftId = 1761652800 }))).Append("\" },\n");
            builder.Append("    \"presence\": { \"presence\": 2, \"stage\": 77, \"resource\": 12.5, \"contribution\": 0.125, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new PresenceMessage { Presence = PartyPresence.Peaceful, Stage = 77, Resource = 12.5f, Contribution = 0.125f }))).Append("\" },\n");
            var offer = new PartyOfferMessage { Slot = 0 };
            offer.SetStat(0, 0.3);
            offer.SetAura(0, new OfferedAura { GemNetId = 5, Strength = 1.5f, TicksLeft = 200 });
            offer.SetAura(8, new OfferedAura { GemNetId = 7, Strength = 0.5f, TicksLeft = 350 });
            var relayed = offer;
            relayed.Slot = 2;
            builder.Append("    \"offerRelay\": { \"slot\": 2, \"auraTicks\": 350, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, offer)))
                .Append("\", \"relayedHex\": \"").Append(Hex(PartyCodec.Encode(_buffer, relayed))).Append("\" },\n");
            builder.Append("    \"reactionRelay\": { \"slot\": 1, \"reaction\": 4, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new PartyReactionMessage { Slot = 0, Reaction = 4 })))
                .Append("\", \"relayedHex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new PartyReactionMessage { Slot = 1, Reaction = 4 }))).Append("\" },\n");
            builder.Append("    \"synergy\": { \"reactions\": 5, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new SynergyMessage { Reactions = 5 }))).Append("\" },\n");
            builder.Append("    \"allyCast\": { \"slot\": 2, \"gem\": 4242, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new AllyCastMessage { GemNetId = 4242 })))
                .Append("\", \"relayedHex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new AllyCastMessage { Slot = 2, GemNetId = 4242 }))).Append("\" },\n");
            var loadout = new PartyLoadoutMessage();
            loadout.Set(0, new LoadoutSkill { GemNetId = 4242, Level = 7, Combo = 13 });
            loadout.Set(PartyLoadoutMessage.PositionOf(Game.Core.Sim.SkillSlotRole.Auto, 1), new LoadoutSkill { GemNetId = 99, Level = 25, Combo = 26 });
            loadout.Set(PartyLoadoutMessage.PositionOf(Game.Core.Sim.SkillSlotRole.Aura, 3), new LoadoutSkill { GemNetId = 5, Level = 1, Combo = 13 });
            var loadoutRelayed = loadout;
            loadoutRelayed.Slot = 3;
            builder.Append("    \"loadoutRelay\": { \"slot\": 3, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, loadout)))
                .Append("\", \"relayedHex\": \"").Append(Hex(PartyCodec.Encode(_buffer, loadoutRelayed))).Append("\" },\n");
            builder.Append("    \"allyResource\": { \"slot\": 3, \"amount\": 20, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new PartyResourceMessage { Amount = 20 })))
                .Append("\", \"relayedHex\": \"").Append(Hex(PartyCodec.Encode(_buffer, new PartyResourceMessage { Slot = 3, Amount = 20 }))).Append("\" },\n");
            builder.Append("    \"focusDropped\": { \"from\": 255, \"to\": 255, \"focus\": 1.75, \"hex\": \"").Append(Hex(PartyCodec.Encode(_buffer, FocusRelayMessage.Dropped(1.75)))).Append("\" },\n");
            builder.Append("    \"focusRelay\": { \"from\": 2, \"to\": 0, \"focus\": 0.875, \"hex\": \"")
                .Append(Hex(PartyCodec.Encode(_buffer, new FocusRelayMessage { From = 2, To = 0, Focus = 0.875f }))).Append("\" }\n");
            builder.Append("  }\n}\n");
            return builder.ToString();
        }

        private string Hex(int length)
        {
            var text = new StringBuilder(length * 2);
            for (var i = 0; i < length; i++)
                text.Append(_buffer[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }
}
