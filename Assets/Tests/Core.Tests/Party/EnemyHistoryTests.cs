using Game.Core.Party;
using Game.Core.Party.Wire;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class EnemyHistoryTests
    {
        [Test]
        public void TheNewestEnemyIsAgeZeroAndOldOnesFallOffAfterThirtyTwo()
        {
            var history = new EnemyHistory();
            history.Push(true, true, 0b0101);
            for (var i = 0; i < 9; i++)
                history.Push(i % 3 == 0, false, 0b0010);

            Assert.That(history.KilledAt(9), Is.True);
            Assert.That(history.BossAt(9), Is.True);
            Assert.That(history.RewardedAt(9), Is.EqualTo(0b0101));
            Assert.That(history.RewardedFor(9, 2), Is.True);
            Assert.That(history.RewardedFor(9, 1), Is.False);
            Assert.That(history.KilledAt(8), Is.True);
            Assert.That(history.KilledAt(7), Is.False);
            Assert.That(history.RewardedAt(7), Is.Zero, "an enemy that was not killed pays nobody");
            Assert.That(history.RewardedAt(2), Is.EqualTo(0b0010));

            for (var i = 0; i < 22; i++)
                history.Push(false, false, 0);
            Assert.That(history.KilledAt(31), Is.True);
            Assert.That(history.RewardedAt(31), Is.EqualTo(0b0101));
            history.Push(false, false, 0);
            Assert.That(history.KilledAt(31), Is.True, "the boss fell off, the next kill is the oldest now");
            Assert.That(history.BossAt(31), Is.False);
            Assert.That(history.RewardedAt(31), Is.EqualTo(0b0010));
            Assert.That(history.KilledAt(32), Is.False);
        }

        [Test]
        public void TheHistoryTravelsWithTheState()
        {
            var history = new EnemyHistory();
            for (var i = 0; i < 40; i++)
                history.Push(i % 2 == 0, i % 5 == 0, (byte)(i % 16));
            var buffer = new byte[PartyCodec.MaxPacketBytes];
            var length = PartyCodec.Encode(buffer, new EncounterStateMessage { History = history });

            Assert.That(PartyCodec.TryDecode(buffer, length, out EncounterStateMessage state), Is.True);
            Assert.That(state.History, Is.EqualTo(history));
            Assert.That(length, Is.LessThanOrEqualTo(PartyCodec.MaxPacketBytes));
        }
    }
}
