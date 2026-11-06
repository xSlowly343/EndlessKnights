using Game.Core.Party;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyPierceTests
    {
        private static PartyRules Rules => TestWorld.WithPierce.Party;

        private static PartyEncounter Encounter(int players)
        {
            var encounter = new PartyEncounter(Rules, TestWorld.WithPierce.Stages, 7);
            for (var i = 0; i < players; i++)
                encounter.Join(100UL + (ulong)i);
            encounter.Advance();
            for (var i = 0; i < players; i++)
                encounter.ReportInput(i);
            return encounter;
        }

        private static int Kills(PartyEncounter encounter) => CountKills(encounter, out _);

        private static int CountKills(PartyEncounter encounter, out bool bossStarted)
        {
            var kills = 0;
            bossStarted = false;
            for (var i = 0; i < encounter.Events.Count; i++)
            {
                if (encounter.Events[i].Kind == PartyEventKind.EnemyKilled)
                    kills++;
                if (encounter.Events[i].Kind == PartyEventKind.BossStarted)
                    bossStarted = true;
            }
            encounter.Events.Clear();
            return kills;
        }

        [Test]
        public void TheHostCarriesHalfOfTheOverflowToTheNextEnemy()
        {
            var encounter = Encounter(1);
            Kills(encounter);
            encounter.ApplyDamage(0, encounter.EnemyId, 1.6);
            Assert.That(Kills(encounter), Is.EqualTo(1));
            Assert.That(encounter.BarLeft, Is.EqualTo(encounter.BarFull - 0.6 * Rules.PierceShare).Within(1e-9));
        }

        [Test]
        public void AHugeHitChainsUpToTheTickLimitAndNeverIntoTheBoss()
        {
            var encounter = Encounter(1);
            Kills(encounter);
            encounter.ApplyDamage(0, encounter.EnemyId, 1e9);
            var first = CountKills(encounter, out var bossStarted);
            Assert.That(first, Is.EqualTo(System.Math.Min(1 + Rules.PierceKillsPerTick, TestWorld.WithPierce.Stages.EnemiesOnStage(1))));
            Assert.That(first, Is.LessThanOrEqualTo(EnemyHistory.Depth / 2 + 1), "the kill history of the state still covers every kill");
            for (var tick = 0; tick < 10 && !encounter.FightingBoss; tick++)
            {
                encounter.Advance();
                encounter.ReportInput(0);
                encounter.ApplyDamage(0, encounter.EnemyId, 1e9);
                CountKills(encounter, out bossStarted);
            }
            Assert.That(encounter.FightingBoss, Is.True);
            Assert.That(encounter.BarLeft, Is.EqualTo(encounter.BarFull), "the overflow stops at the boss");
        }

        [Test]
        public void AnIdleMemberDoesNotPierce()
        {
            var encounter = Encounter(2);
            Kills(encounter);
            for (var tick = 0; tick < (Rules.Contribution.ActiveInputSeconds + 1) * 10; tick++)
            {
                encounter.Advance();
                encounter.ReportInput(0);
            }
            Assert.That(encounter.Typing(1), Is.False);
            encounter.ApplyDamage(1, encounter.EnemyId, 1e9);
            Assert.That(Kills(encounter), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TheDamageCapGrowsWithTheEnemiesOnTheStage()
        {
            var caps = Rules.Caps;
            Assert.That(caps.BarsPerSecondAt(10), Is.EqualTo(caps.BarsPerSecond), "early stages keep the old cap");
            Assert.That(caps.BurstAt(10), Is.EqualTo(caps.BurstBars));
            Assert.That(caps.BarsPerSecondAt(200), Is.EqualTo(200 * caps.StageBarsShare).Within(1e-9));
            Assert.That(caps.BurstAt(200), Is.EqualTo(caps.BurstBars * 200 * caps.StageBarsShare / caps.BarsPerSecond).Within(1e-9));
        }

        [Test]
        public void SetStatsAreCutByTheHostCaps()
        {
            var encounter = Encounter(1);
            encounter.SetPierce(0, new PartyPierceStats(5, 99, 9, 9, 9, 9));
            var member = encounter.Member(0);
            Assert.That(member.PierceShare, Is.EqualTo(Rules.PierceCaps.MaxShare));
            Assert.That(member.EnemySkip, Is.EqualTo(Rules.PierceCaps.MaxEnemySkip));
            Assert.That(member.SplashTargets, Is.EqualTo(Rules.PierceCaps.MaxSplashTargets));
            Assert.That(member.BossPierce, Is.EqualTo(Rules.PierceCaps.MaxBossPierce));
        }

        [Test]
        public void AKillSkipsTheNextEnemiesOfTheParty()
        {
            var encounter = Encounter(1);
            encounter.Events.Clear();
            encounter.SetPierce(0, new PartyPierceStats(0, 2, 0, 0, 0, 0));
            encounter.ApplyDamage(0, encounter.EnemyId, encounter.BarLeft);
            Assert.That(Kills(encounter), Is.EqualTo(3), "one kill and two skipped enemies");
        }

        [Test]
        public void SplashWoundsTheNextEnemiesWhenTheyAppear()
        {
            var encounter = Encounter(1);
            encounter.SetPierce(0, new PartyPierceStats(0, 0, 0.5, 2, 0, 0));
            var half = encounter.BarLeft / 2;
            encounter.ApplyDamage(0, encounter.EnemyId, half);
            Assert.That(encounter.SplashQueued(0, 0), Is.EqualTo(half * 0.5).Within(1e-12));
            Assert.That(encounter.SplashQueued(0, 1), Is.EqualTo(half * 0.5).Within(1e-12));
            var full = encounter.BarFull;
            encounter.ApplyDamage(0, encounter.EnemyId, encounter.BarLeft);
            Assert.That(encounter.BarLeft, Is.LessThan(full), "the next enemy appears already wounded");
        }
    }
}
