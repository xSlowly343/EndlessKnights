using System;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyEncounterTests
    {
        private static PartyRules Rules => TestWorld.Data.Party;
        private static StageTable Stages => TestWorld.Data.Stages;
        private static int RestTicks => (int)System.Math.Round(Rules.RestAfterSeconds * GameLoop.TicksPerSecond);
        private static int GraceTicks => (int)System.Math.Round(Rules.DisconnectGraceSeconds * GameLoop.TicksPerSecond);

        private static PartyEncounter WithPlayers(int players)
        {
            var encounter = new PartyEncounter(Rules, Stages, 42);
            for (var i = 0; i < players; i++)
                Assert.That(encounter.Join(100UL + (ulong)i), Is.EqualTo(i));
            encounter.RequestBossFight();
            return encounter;
        }

        private static void Respawn(PartyEncounter encounter)
        {
            for (var i = 0; i < PartyRules.MaxSlots; i++)
            {
                if (encounter.Member(i).Joined)
                    encounter.ApplyDamage(i, encounter.EnemyId, encounter.BarFull);
            }
        }

        private static void KillCurrent(PartyEncounter encounter) => encounter.ApplyDamage(0, encounter.EnemyId, encounter.BarLeft / encounter.DamageTakenMultiplier + 1e-9);

        private static PartyEncounter AtBoss()
        {
            var encounter = new PartyEncounter(Rules, Stages, 42);
            encounter.Join(100);
            while (!encounter.FightingBoss)
                KillCurrent(encounter);
            for (var i = 0; i <= PartyEncounter.MaxRepeatSeconds * GameLoop.TicksPerSecond; i++)
                encounter.Advance();
            return encounter;
        }

        [Test]
        public void TimerSlowsStackDownToTheFloorAndCountSavedSeconds()
        {
            var encounter = AtBoss();
            var start = encounter.BossTicksLeft;
            Assert.That(encounter.ApplyEnemyEffect(1, 0, 3, 0, 0.5), Is.True, "a pure timer effect is taken");

            for (var i = 0; i < 20; i++)
                encounter.Advance();

            Assert.That(start - encounter.BossTicksLeft, Is.EqualTo(10));
            Assert.That(encounter.SavedBossTicks, Is.EqualTo(10).Within(1e-9));
            encounter.ApplyEnemyEffect(2, 0.1, 5, 0, 0.6);
            encounter.ApplyEnemyEffect(3, 0, 5, 0, 0.9);
            Assert.That(encounter.BossTimerRate, Is.EqualTo(Rules.Timer.MinBossRate));
            Assert.That(encounter.ApplyEnemyEffect(4, 0, 5, 0, 0), Is.False, "an effect needs damage or a timer slow");
        }

        [Test]
        public void TheBossKillReportsTheLoopAndTheSavedSeconds()
        {
            var encounter = AtBoss();
            encounter.Events.Clear();
            encounter.ApplyEnemyEffect(1, 0, 5, 0, 0.5);
            encounter.ApplyDamage(0, encounter.EnemyId, 0.3);
            for (var i = 0; i < 30; i++)
                encounter.Advance();

            Assert.That(encounter.RepeatRecent(0, 5), Is.EqualTo(0.3).Within(1e-9), "the chimes repeat the last seconds of the party");
            KillCurrent(encounter);

            var killed = encounter.Events[0];
            for (var i = 0; i < encounter.Events.Count && killed.Kind != PartyEventKind.EnemyKilled; i++)
                killed = encounter.Events[i];
            Assert.That(killed.Kind, Is.EqualTo(PartyEventKind.EnemyKilled));
            Assert.That(killed.Loop, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(killed.SavedSeconds, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(encounter.LoopShare, Is.Zero);
            Assert.That(encounter.SavedBossTicks, Is.Zero);
        }

        [Test]
        public void RepeatingNormalDamageOnTheBossIsScaledToTheBoss()
        {
            var encounter = new PartyEncounter(Rules, Stages, 42);
            encounter.Join(100);
            while (encounter.KillsOnStage < Stages.EnemiesPerStage - 1 || encounter.FightingBoss)
                KillCurrent(encounter);
            for (var i = 0; i < 60; i++)
                encounter.Advance();
            KillCurrent(encounter);
            Assert.That(encounter.FightingBoss, Is.True);

            var repeated = encounter.RepeatRecent(0, 5);

            Assert.That(repeated, Is.EqualTo(1 / Stages.BossHpMultiplier).Within(1e-6));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(4, 4)]
        public void BarGrowsWithFighters(int players, double expected)
        {
            var encounter = WithPlayers(players);
            Respawn(encounter);

            Assert.That(encounter.Fighters, Is.EqualTo(players));
            Assert.That(encounter.BarFull, Is.EqualTo(expected));
        }

        [Test]
        public void FifthPlayerCannotJoin()
        {
            var encounter = WithPlayers(4);

            Assert.That(encounter.Join(999), Is.EqualTo(PartyEncounter.NoSlot));
            Assert.That(encounter.Join(101), Is.EqualTo(1));
        }

        [Test]
        public void FourBotsOnDifferentStagesShareOneEnemyEqually()
        {
            var encounter = WithPlayers(4);
            Respawn(encounter);
            encounter.Events.Clear();
            long[] stages = { 30, 100, 250, 400 };
            const double secondsToKillAlone = 2.0;
            var enemyId = encounter.EnemyId;
            var ticks = 0;

            while (encounter.EnemyId == enemyId)
            {
                for (var slot = 0; slot < stages.Length; slot++)
                {
                    var hp = Stages.EnemyHp(stages[slot]);
                    var damagePerTick = hp * (GameLoop.TickSeconds / secondsToKillAlone);
                    encounter.ApplyDamage(slot, enemyId, (damagePerTick / hp).ToDouble());
                }
                encounter.Advance();
                ticks++;
            }

            Assert.That(ticks * GameLoop.TickSeconds, Is.EqualTo(secondsToKillAlone).Within(0.11));
            Assert.That(encounter.Events.CountOf(PartyEventKind.EnemyKilled), Is.EqualTo(1));
            var killed = encounter.Events[0];
            for (var slot = 0; slot < stages.Length; slot++)
                Assert.That(killed.Contribution(slot), Is.EqualTo(0.25).Within(1e-6));
        }

        [Test]
        public void TenKillsBringTheBossAndItsKillClearsTheStage()
        {
            var encounter = WithPlayers(2);
            for (var i = 0; i < Stages.EnemiesPerStage; i++)
                KillCurrent(encounter);

            Assert.That(encounter.FightingBoss, Is.True);
            Assert.That(encounter.BossSecondsLeft, Is.EqualTo(Stages.BossTimerSeconds).Within(1e-9));
            Assert.That(encounter.Events.CountOf(PartyEventKind.BossStarted), Is.EqualTo(1));

            KillCurrent(encounter);

            Assert.That(encounter.Events.CountOf(PartyEventKind.StageCleared), Is.EqualTo(1));
            Assert.That(encounter.KillsOnStage, Is.EqualTo(0));
            Assert.That(encounter.FightingBoss, Is.False);
        }

        [Test]
        public void BossTimeoutSendsThePartyToFarmAndBossReturnsAfterRetryKills()
        {
            var encounter = WithPlayers(2);
            for (var i = 0; i < Stages.EnemiesPerStage; i++)
                KillCurrent(encounter);
            var ticks = encounter.BossTicksLeft;
            for (var i = 0; i < ticks; i++)
            {
                encounter.ReportInput(0);
                encounter.ReportInput(1);
                encounter.Advance();
            }

            Assert.That(encounter.Events.CountOf(PartyEventKind.BossFailed), Is.Zero, "two fighters share the timer, it runs at half speed");
            for (var i = 0; i < ticks; i++)
            {
                encounter.ReportInput(0);
                encounter.ReportInput(1);
                encounter.Advance();
            }
            Assert.That(encounter.Events.CountOf(PartyEventKind.BossFailed), Is.EqualTo(1));
            Assert.That(encounter.Farming, Is.True);
            Assert.That(encounter.FightingBoss, Is.False);

            for (var i = 0; i < Stages.BossRetryAfterKills; i++)
                KillCurrent(encounter);

            Assert.That(encounter.FightingBoss, Is.True);
        }

        [Test]
        public void RestingPlayerStopsCountingFromTheNextEnemy()
        {
            var encounter = WithPlayers(2);
            Respawn(encounter);
            for (var i = 0; i <= RestTicks; i++)
            {
                encounter.ReportInput(0);
                encounter.Advance();
            }

            Assert.That(encounter.Presence(1), Is.EqualTo(PartyPresence.Resting));
            Assert.That(encounter.BarFull, Is.EqualTo(2));

            KillCurrent(encounter);

            Assert.That(encounter.Fighters, Is.EqualTo(1));
            Assert.That(encounter.BarFull, Is.EqualTo(1));
        }

        [Test]
        public void EveryoneRestingStillCountsAsOne()
        {
            var encounter = WithPlayers(3);
            for (var i = 0; i <= RestTicks; i++)
                encounter.Advance();
            KillCurrent(encounter);

            Assert.That(encounter.Fighters, Is.EqualTo(1), "only the one who dealt the killing damage counts");
            Assert.That(encounter.BarFull, Is.EqualTo(1));
        }

        [Test]
        public void PeacefulPlayerHasNoWeight()
        {
            var encounter = WithPlayers(2);
            encounter.SetPeaceful(1, true);
            KillCurrent(encounter);

            Assert.That(encounter.Presence(1), Is.EqualTo(PartyPresence.Peaceful));
            Assert.That(encounter.BarFull, Is.EqualTo(1));
        }

        [Test]
        public void NewcomerDoesNotGrowTheCurrentEnemy()
        {
            var encounter = WithPlayers(1);
            var full = encounter.BarFull;

            encounter.Join(500);

            Assert.That(encounter.BarFull, Is.EqualTo(full));
            KillCurrent(encounter);
            Assert.That(encounter.BarFull, Is.EqualTo(2));
        }

        [Test]
        public void LongDisconnectDuringBossShrinksTheRest()
        {
            var encounter = WithPlayers(3);
            for (var i = 0; i < Stages.EnemiesPerStage - 1; i++)
                KillCurrent(encounter);
            encounter.SetConnected(2, false);
            Fight(encounter, GraceTicks - 50);
            KillCurrent(encounter);
            Assert.That(encounter.FightingBoss, Is.True);
            Assert.That(encounter.BarFull, Is.EqualTo(3));
            encounter.ApplyDamage(0, encounter.EnemyId, 1.5);

            Fight(encounter, 51);

            Assert.That(encounter.FightingBoss, Is.True);
            Assert.That(encounter.Fighters, Is.EqualTo(2));
            Assert.That(encounter.BarFull, Is.EqualTo(2));
            Assert.That(encounter.BarLeft, Is.EqualTo(1.5 * 2 / 3).Within(1e-9));
            Assert.That(encounter.Presence(2), Is.EqualTo(PartyPresence.Offline));
        }

        private static void Fight(PartyEncounter encounter, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                encounter.ReportInput(0);
                encounter.ReportInput(1);
                encounter.Advance();
            }
        }

        [Test]
        public void ShortDisconnectKeepsWeight()
        {
            var encounter = WithPlayers(2);
            encounter.SetConnected(1, false);
            for (var i = 0; i < GraceTicks / 2; i++)
            {
                encounter.ReportInput(0);
                encounter.Advance();
            }
            KillCurrent(encounter);

            Assert.That(encounter.Fighters, Is.EqualTo(2));
        }

        [Test]
        public void LateDamageForTheKilledEnemyMovesToTheNextOne()
        {
            var encounter = WithPlayers(2);
            Respawn(encounter);
            var killed = encounter.EnemyId;
            KillCurrent(encounter);
            encounter.Advance();

            var applied = encounter.ApplyDamage(1, killed, 0.5);

            Assert.That(applied, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(encounter.BarLeft, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void VeryLateOrUnknownDamageIsDropped()
        {
            var encounter = WithPlayers(2);
            Respawn(encounter);
            var killed = encounter.EnemyId;
            KillCurrent(encounter);
            for (var i = 0; i < 20; i++)
                encounter.Advance();

            Assert.That(encounter.ApplyDamage(1, killed, 0.5), Is.EqualTo(0));
            Assert.That(encounter.ApplyDamage(1, encounter.EnemyId + 5, 0.5), Is.EqualTo(0));
            Assert.That(encounter.ApplyDamage(1, encounter.EnemyId, double.NaN), Is.EqualTo(0));
            Assert.That(encounter.ApplyDamage(1, encounter.EnemyId, -1), Is.EqualTo(0));
        }

        [Test]
        public void LateNormalDamageShrinksOnTheBoss()
        {
            var encounter = WithPlayers(1);
            for (var i = 0; i < Stages.EnemiesPerStage - 1; i++)
                KillCurrent(encounter);
            var lastNormal = encounter.EnemyId;
            KillCurrent(encounter);
            Assert.That(encounter.FightingBoss, Is.True);

            var applied = encounter.ApplyDamage(0, lastNormal, 0.7);

            Assert.That(applied, Is.EqualTo(0.7 / Stages.BossHpMultiplier).Within(1e-9));
        }

        [Test]
        public void EnemyEffectsMultiplyEveryShareUpToTheCap()
        {
            var encounter = WithPlayers(2);
            Respawn(encounter);

            Assert.That(encounter.ApplyEnemyEffect(1, 0.3, 5), Is.True);
            Assert.That(encounter.ApplyEnemyEffect(1, 0.2, 5), Is.False);
            Assert.That(encounter.ApplyEnemyEffect(2, 0.25, 5), Is.True);
            Assert.That(encounter.DamageTakenMultiplier, Is.EqualTo(1.55).Within(1e-9));
            Assert.That(encounter.ApplyDamage(0, encounter.EnemyId, 0.2), Is.EqualTo(0.31).Within(1e-9));

            encounter.ApplyEnemyEffect(3, 5, 5);
            Assert.That(encounter.DamageTakenMultiplier, Is.EqualTo(1 + Rules.MaxEnemyDamageTaken).Within(1e-9));
        }

        [Test]
        public void EnemyEffectsExpire()
        {
            var encounter = WithPlayers(1);
            encounter.ApplyEnemyEffect(1, 0.5, 1);
            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
                encounter.Advance();

            Assert.That(encounter.DamageTakenMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void EnemySeedIsTheSameForEveryoneWithTheSameSeed()
        {
            var a = new PartyEncounter(Rules, Stages, 7);
            var b = new PartyEncounter(Rules, Stages, 7);
            var c = new PartyEncounter(Rules, Stages, 8);

            Assert.That(a.SeedFor(3), Is.EqualTo(b.SeedFor(3)));
            Assert.That(a.SeedFor(3), Is.Not.EqualTo(a.SeedFor(4)));
            Assert.That(a.SeedFor(3), Is.Not.EqualTo(c.SeedFor(3)));
        }

        [Test]
        public void LeavingDuringBossShrinksTheRest()
        {
            var encounter = WithPlayers(2);
            for (var i = 0; i < Stages.EnemiesPerStage; i++)
                KillCurrent(encounter);
            Assert.That(encounter.FightingBoss, Is.True);

            encounter.Leave(1);

            Assert.That(encounter.MemberCount, Is.EqualTo(1));
            Assert.That(encounter.BarFull, Is.EqualTo(1));
            Assert.That(encounter.BarLeft, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void FightingDoesNotAllocate()
        {
            var probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var encounter = WithPlayers(4);
            void Fight(int ticks)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    for (var slot = 0; slot < 4; slot++)
                    {
                        encounter.ReportInput(slot);
                        encounter.ApplyDamage(slot, encounter.EnemyId, 0.05);
                    }
                    if (tick % 50 == 0)
                        encounter.ApplyEnemyEffect(tick % 3, 0.2, 2);
                    encounter.Advance();
                    encounter.Events.Clear();
                }
            }
            Fight(500);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Fight(3000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0));
        }
    }
}
