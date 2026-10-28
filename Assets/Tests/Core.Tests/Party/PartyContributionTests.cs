using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyContributionTests
    {
        private static PartyRules Rules => TestWorld.Data.Party;
        private static StageTable Stages => TestWorld.Data.Stages;
        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
        private static int RestTicks => Ticks(Rules.RestAfterSeconds);
        private static int IdleTicks => Ticks(Rules.Contribution.ActiveInputSeconds) + 1;

        private static PartyEncounter WithPlayers(int players)
        {
            var encounter = new PartyEncounter(Rules, Stages, 42);
            for (var i = 0; i < players; i++)
                Assert.That(encounter.Join(100UL + (ulong)i), Is.EqualTo(i));
            return encounter;
        }

        private static void Advance(PartyEncounter encounter, int ticks, params int[] typing)
        {
            for (var i = 0; i < ticks; i++)
            {
                foreach (var slot in typing)
                    encounter.ReportInput(slot);
                encounter.Advance();
            }
        }

        private static PartyEvent KillWith(PartyEncounter encounter, int slot)
        {
            encounter.Events.Clear();
            encounter.ApplyDamage(slot, encounter.EnemyId, encounter.BarLeft / encounter.DamageTakenMultiplier + 1e-9);
            for (var i = 0; i < encounter.Events.Count; i++)
            {
                if (encounter.Events[i].Kind == PartyEventKind.EnemyKilled)
                    return encounter.Events[i];
            }
            Assert.Fail("the enemy did not die");
            return default;
        }

        private static void NextEnemy(PartyEncounter encounter, params int[] typing)
        {
            foreach (var slot in typing)
                encounter.ReportInput(slot);
            KillWith(encounter, typing.Length > 0 ? typing[0] : 0);
        }

        [Test]
        public void TypingFriendsAreNotCapped()
        {
            var encounter = WithPlayers(2);
            NextEnemy(encounter, 0, 1);
            Assert.That(encounter.Fighters, Is.EqualTo(2));

            var applied = encounter.ApplyDamage(1, encounter.EnemyId, 100);

            Assert.That(applied, Is.EqualTo(2));
            Assert.That(encounter.EnemyId, Is.EqualTo(3u), "one typing friend may finish the whole enemy");
        }

        [Test]
        public void IdleDamageLeavesHalfAPortionToTheTypingPlayer()
        {
            var encounter = WithPlayers(2);
            NextEnemy(encounter, 0, 1);
            Advance(encounter, IdleTicks, 0);

            var applied = encounter.ApplyDamage(1, encounter.EnemyId, 100);

            Assert.That(encounter.Typing(0), Is.True);
            Assert.That(encounter.Typing(1), Is.False);
            Assert.That(applied, Is.EqualTo(1.5).Within(1e-12));
            Assert.That(encounter.BarLeft, Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void ThreeIdleHelpersCannotCarryATypingPlayer()
        {
            var encounter = WithPlayers(4);
            NextEnemy(encounter, 0, 1, 2, 3);
            Assert.That(encounter.BarFull, Is.EqualTo(4));
            Advance(encounter, IdleTicks, 0);

            for (var slot = 1; slot < 4; slot++)
                encounter.ApplyDamage(slot, encounter.EnemyId, 100);

            Assert.That(encounter.BarLeft, Is.EqualTo(0.5).Within(1e-12), "the typing player still has to deal half a portion");
            Assert.That(encounter.ApplyDamage(0, encounter.EnemyId, 0.5), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(encounter.EnemyId, Is.EqualTo(3u));
        }

        [Test]
        public void IdleDamageCountsAsFightingAndGrowsTheEnemy()
        {
            var encounter = WithPlayers(2);
            Advance(encounter, RestTicks + 1, 0);
            Assert.That(encounter.Presence(1), Is.EqualTo(PartyPresence.Resting));

            encounter.ApplyDamage(1, encounter.EnemyId, 0.3);
            KillWith(encounter, 0);

            Assert.That(encounter.Presence(1), Is.EqualTo(PartyPresence.Fighting));
            Assert.That(encounter.Fighters, Is.EqualTo(2));
            Assert.That(encounter.BarFull, Is.EqualTo(2));
        }

        [Test]
        public void TinyIdleDamageDoesNotCountAsFighting()
        {
            var encounter = WithPlayers(2);
            Advance(encounter, RestTicks + 1, 0);

            encounter.ApplyDamage(1, encounter.EnemyId, Rules.Contribution.MinShare * 0.5);
            KillWith(encounter, 0);

            Assert.That(encounter.Presence(1), Is.EqualTo(PartyPresence.Resting));
            Assert.That(encounter.Fighters, Is.EqualTo(1));
        }

        [Test]
        public void TypingPlayersAreRewardedAndFillTheirRestBank()
        {
            var encounter = WithPlayers(2);
            Advance(encounter, 50, 0, 1);

            var killed = KillWith(encounter, 0);

            Assert.That(killed.RewardedFor(0), Is.True);
            Assert.That(killed.RewardedFor(1), Is.True, "a typing friend is paid even without damage");
            Assert.That(encounter.Member(1).RestBankTicks, Is.EqualTo(50));
        }

        [Test]
        public void IdlePlayerIsPaidFromTheBankUntilItRunsOut()
        {
            var encounter = WithPlayers(2);
            Advance(encounter, 100, 0, 1);
            KillWith(encounter, 0);
            Assert.That(encounter.Member(1).RestBankTicks, Is.EqualTo(100));

            Advance(encounter, IdleTicks, 0);
            var paid = KillWith(encounter, 0);
            Assert.That(paid.RewardedFor(1), Is.True);
            Assert.That(encounter.Member(1).RestBankTicks, Is.EqualTo(0));

            Advance(encounter, 10, 0);
            var unpaid = KillWith(encounter, 0);
            Assert.That(unpaid.RewardedFor(0), Is.True);
            Assert.That(unpaid.RewardedFor(1), Is.False, "the bank is empty");
        }

        [Test]
        public void IdleAccountWithoutPlayGetsNothing()
        {
            var encounter = WithPlayers(2);
            Advance(encounter, IdleTicks, 0);
            encounter.ApplyDamage(1, encounter.EnemyId, 0.5);

            var killed = KillWith(encounter, 0);

            Assert.That(killed.RewardedFor(1), Is.False, "idle damage alone does not pay");
            Assert.That(encounter.Member(1).RestBankTicks, Is.EqualTo(0));
        }

        [Test]
        public void TheRestBankStopsAtItsLimit()
        {
            var encounter = WithPlayers(1);
            var limit = Ticks(Rules.Contribution.RestBankSeconds);
            Advance(encounter, limit + 500, 0);

            KillWith(encounter, 0);

            Assert.That(encounter.Member(0).RestBankTicks, Is.EqualTo(limit));
        }

        [Test]
        public void PeacefulAndOfflinePlayersAreNotPaid()
        {
            var encounter = WithPlayers(3);
            encounter.SetPeaceful(1, true);
            encounter.SetConnected(2, false);

            var killed = KillWith(encounter, 0);

            Assert.That(killed.RewardedFor(0), Is.True);
            Assert.That(killed.RewardedFor(1), Is.False);
            Assert.That(killed.RewardedFor(2), Is.False);
        }

        [Test]
        public void AnIdlePartyCanStillFinishItsEnemy()
        {
            var encounter = WithPlayers(2);
            NextEnemy(encounter, 0, 1);
            Advance(encounter, RestTicks + 1);

            encounter.ApplyDamage(0, encounter.EnemyId, 100);

            Assert.That(encounter.EnemyId, Is.EqualTo(3u), "after the rest window nobody holds a portion, so nothing stalls");
        }

        [Test]
        public void RosterCarriesTheRestBankInSeconds()
        {
            var encounter = WithPlayers(1);
            Advance(encounter, 123, 0);
            KillWith(encounter, 0);

            var roster = Game.Core.Party.Wire.RosterMessage.Of(encounter, 1);

            Assert.That(roster.RestBankSeconds(0), Is.EqualTo(12u));
        }
    }
}
