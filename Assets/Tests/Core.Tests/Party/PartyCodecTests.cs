using System;
using Game.Core.Party;
using Game.Core.Party.Wire;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyCodecTests
    {
        private readonly byte[] _buffer = new byte[PartyCodec.MaxPacketBytes];

        [Test]
        public void DamageBatchRoundTrips()
        {
            var sent = new DamageBatchMessage { Sequence = 9, EnemyId = 77, Keys = 0.25, Mouse = 0.01, Skills = 0.125, Dot = 0.003, Summons = 1e-9, Hits = 12, Crits = 2 };

            var length = PartyCodec.Encode(_buffer, sent);

            Assert.That(PartyCodec.KindOf(_buffer, length), Is.EqualTo(PartyMessageKind.DamageBatch));
            Assert.That(PartyCodec.TryDecode(_buffer, length, out DamageBatchMessage read), Is.True);
            Assert.That(read, Is.EqualTo(sent));
            Assert.That(read.Total, Is.EqualTo(sent.Total));
        }

        [Test]
        public void EveryMessageRoundTripsAndFitsThePacket()
        {
            AssertRoundTrip(new HelloMessage { HeroClass = 5, Stage = 412, SharePerSecondCap = 3.5 }, PartyCodec.Encode, Decode<HelloMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new CastEffectMessage { EffectId = 3, OnEnemy = true, Strength = 0.4, Seconds = 8 }, PartyCodec.Encode, Decode<CastEffectMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new PresenceMessage { Presence = PartyPresence.Peaceful, Stage = 30, Resource = 42.5f }, PartyCodec.Encode, Decode<PresenceMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new EncounterStateMessage { Sequence = 4, Tick = 1234, EnemyId = 11, EnemyKind = PartyEnemyKind.Boss, BarLeft = 1.25, BarFull = 3, BossTicksLeft = 250, KillsOnStage = 10, Farming = true, WeightMask = 0b1011, DamageTaken = 1.3, EnemySeed = ulong.MaxValue, Recent1 = 0.25f, Recent3 = 1.5f }, PartyCodec.Encode, Decode<EncounterStateMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new EnemyKilledMessage { Tick = 99, EnemyId = 12, EnemyKind = PartyEnemyKind.Normal, Share0 = 0.5, Share1 = 0.25, Share2 = 0.125, Share3 = 0.125, Rewarded = 9 }, PartyCodec.Encode, Decode<EnemyKilledMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new EncounterMomentMessage { Kind = PartyEventKind.BossFailed, Tick = 5, EnemyId = 3, BossTicksLeft = 0 }, PartyCodec.Encode, Decode<EncounterMomentMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new PartyReactionMessage { Slot = 3, Reaction = 5 }, PartyCodec.Encode, Decode<PartyReactionMessage>(PartyCodec.TryDecode));
            AssertRoundTrip(new RosterMessage { Version = 2, Player0 = 1, Player1 = 2, Player3 = ulong.MaxValue, Presence1 = (byte)PartyPresence.Resting, Presence2 = (byte)PartyPresence.Offline, RestBank1 = 3600, RestBank3 = 7 }, PartyCodec.Encode, Decode<RosterMessage>(PartyCodec.TryDecode));
        }

        [Test]
        public void TruncatedOrWrongPacketIsRejected()
        {
            var length = PartyCodec.Encode(_buffer, new DamageBatchMessage { Sequence = 1, EnemyId = 1, Keys = 0.1 });

            Assert.That(PartyCodec.TryDecode(_buffer, length - 1, out DamageBatchMessage _), Is.False);
            Assert.That(PartyCodec.TryDecode(_buffer, length, out HelloMessage _), Is.False);
            Assert.That(PartyCodec.TryDecode(_buffer, 0, out DamageBatchMessage _), Is.False);
            _buffer[0] = 200;
            Assert.That(PartyCodec.KindOf(_buffer, length), Is.EqualTo(PartyMessageKind.None));
        }

        [Test]
        public void EncodingAndDecodingDoNotAllocate()
        {
            var probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var batch = new DamageBatchMessage { Sequence = 1, EnemyId = 2, Keys = 0.5 };
            var state = new EncounterStateMessage { Sequence = 1, EnemyId = 2, BarLeft = 1, BarFull = 2 };
            RoundTrips(batch, state, 10);

            var before = GC.GetAllocatedBytesForCurrentThread();
            RoundTrips(batch, state, 10000);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void RoundTrips(DamageBatchMessage batch, EncounterStateMessage state, int times)
        {
            for (var i = 0; i < times; i++)
            {
                var length = PartyCodec.Encode(_buffer, batch);
                PartyCodec.TryDecode(_buffer, length, out DamageBatchMessage _);
                length = PartyCodec.Encode(_buffer, state);
                PartyCodec.TryDecode(_buffer, length, out EncounterStateMessage _);
            }
        }

        private delegate bool TryDecoder<T>(byte[] buffer, int length, out T message);

        private static Func<byte[], int, T> Decode<T>(TryDecoder<T> decoder) => (buffer, length) =>
        {
            Assert.That(decoder(buffer, length, out var message), Is.True, typeof(T).Name);
            return message;
        };

        private void AssertRoundTrip<T>(T sent, Func<byte[], T, int> encode, Func<byte[], int, T> decode)
        {
            var length = encode(_buffer, sent);

            Assert.That(length, Is.LessThanOrEqualTo(PartyCodec.MaxPacketBytes));
            Assert.That(decode(_buffer, length), Is.EqualTo(sent), typeof(T).Name);
        }
    }
}
