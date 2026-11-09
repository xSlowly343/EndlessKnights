using System;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftTests
    {
        private static readonly long TuesdayNoon = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private static readonly long Inside = TuesdayNoon + 3_600_000;

        private static RiftRules Rules => TestWorld.Data.Rift;

        private static GameLoop HomeAt(long stage)
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = stage;
            loop.State.HighestStage = stage;
            loop.State.RecordStage = stage;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            loop.Stages.EnsureEnemy();
            return loop;
        }

        private static void DefeatBoss(GameLoop loop)
        {
            loop.State.KillsOnStage = loop.Stages.Table.EnemiesOnStage(loop.State.Stage);
            loop.State.Farming = false;
            loop.Stages.OnEnemyKilled();
            Assert.That(loop.State.FightingBoss, Is.True);
            loop.Stages.OnEnemyKilled();
        }

        [Test]
        public void ClosedOutsideTheWindow()
        {
            var loop = HomeAt(40);

            Assert.That(loop.Rift.Enter(TuesdayNoon - 1), Is.EqualTo(RiftEntry.Closed));
            Assert.That(loop.State.Stage, Is.EqualTo(40));
        }

        [Test]
        public void EnteringStartsFromStageOneWithTenfoldHp()
        {
            var loop = HomeAt(40);

            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.Entered));

            Assert.That(loop.State.Stage, Is.EqualTo(1));
            Assert.That(loop.State.Rift.WindowId, Is.EqualTo(TuesdayNoon / 1000));
            var ratio = (loop.Stages.Table.EnemyHp(1) / TestWorld.Data.Stages.EnemyHp(1)).ToDouble();
            Assert.That(ratio, Is.EqualTo(Rules.HpMultiplier).Within(1e-9));
            Assert.That(loop.Stages.Table.BossTimer(30), Is.EqualTo(Rules.BossTimerSeconds));
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.AlreadyInside));
        }

        [Test]
        public void RiftDefenseFollowsTheHomeStageWithTheSameHp()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            var rift = loop.Stages.Table;
            var home = TestWorld.Data.Stages;

            foreach (var stage in new long[] { 1, 300, 2000, 9000 })
            {
                var equal = rift.DefenseStage(stage);
                Assert.That(equal, Is.GreaterThan(stage));
                Assert.That(home.EnemyHp(equal).Log10(), Is.GreaterThanOrEqualTo(rift.EnemyHp(stage).Log10()));
                Assert.That(home.EnemyHp(equal - 1).Log10(), Is.LessThan(rift.EnemyHp(stage).Log10()));
            }
            Assert.That(home.DefenseStage(700), Is.EqualTo(700));
        }

        [Test]
        public void ARiftEnemyGetsTheDefenseOfItsHomeEquivalent()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            loop.State.Stage = 600;
            loop.State.HighestStage = 600;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            loop.Stages.EnsureEnemy();

            var defense = TestWorld.Data.Combat.Defense;
            Assert.That(loop.State.EnemyDefense, Is.EqualTo(defense.Defense(loop.Stages.Table.DefenseStage(600), false)));
            Assert.That(loop.State.EnemyDefense, Is.GreaterThan(defense.Defense(600, false)));
        }

        [Test]
        public void SoftWallsComeEveryTwentyFiveStages()
        {
            var loop = HomeAt(1);
            loop.Rift.Enter(Inside);
            var table = loop.Stages.Table;

            var normalStep = (table.EnemyHp(20) / table.EnemyHp(19)).ToDouble();
            var wallStep = (table.EnemyHp(Rules.WallPeriod - Rules.WallZoneLength + 1) / table.EnemyHp(Rules.WallPeriod - Rules.WallZoneLength)).ToDouble();

            Assert.That(wallStep, Is.GreaterThan(normalStep));
        }

        [Test]
        public void LeavingRestoresHomeAndKeepsRiftProgress()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            DefeatBoss(loop);
            Assert.That(loop.State.Stage, Is.EqualTo(2));

            Assert.That(loop.Rift.Leave(), Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(40));
            Assert.That(loop.Stages.Table.EnemyHp(1).ToDouble(), Is.EqualTo(TestWorld.Data.Stages.EnemyHp(1).ToDouble()).Within(1e-9));

            loop.Rift.Enter(Inside + 1000);
            Assert.That(loop.State.Stage, Is.EqualTo(2));
        }

        [Test]
        public void RiftStagesDoNotRaiseTheRecord()
        {
            var loop = HomeAt(1);
            loop.Rift.Enter(Inside);

            DefeatBoss(loop);

            Assert.That(loop.State.RecordStage, Is.EqualTo(1));
            Assert.That(loop.State.Rift.Deepest, Is.EqualTo(2));
        }

        [Test]
        public void BossesDropShardsAndGuardiansDropMore()
        {
            var loop = HomeAt(1);
            loop.Rift.Enter(Inside);

            DefeatBoss(loop);
            Assert.That(loop.State.Rift.Shards.ToDouble(), Is.EqualTo(Rules.BossShards));

            loop.State.Stage = loop.Stages.Table.BiomeLength;
            loop.State.HighestStage = loop.State.Stage;
            DefeatBoss(loop);
            Assert.That(loop.State.Rift.Shards.ToDouble(), Is.EqualTo(Rules.BossShards + Rules.GuardianShards));
        }

        [Test]
        public void HomeBossesGiveNoShards()
        {
            var loop = HomeAt(1);

            DefeatBoss(loop);

            Assert.That(loop.State.Rift.Shards.ToDouble(), Is.EqualTo(0));
        }

        [Test]
        public void ArtifactsInsideAreBoughtWithShardsAndHomeOnesSleep()
        {
            var loop = HomeAt(1);
            loop.State.Echo = BigNumber.FromDouble(1e6);
            var home = loop.Artifacts.TryUnlock()!;
            var homeDamage = loop.Hero.Value(CombatStats.Damage, 1);

            loop.Rift.Enter(Inside);
            Assert.That(loop.Artifacts.Owned.Count, Is.EqualTo(0));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.LessThan(homeDamage));
            Assert.That(loop.Artifacts.TryUnlock(), Is.Null);

            loop.State.Rift.Shards = BigNumber.FromDouble(Rules.UnlockCost.At(1).ToDouble());
            var rift = loop.Artifacts.TryUnlock();
            Assert.That(rift, Is.Not.Null);
            Assert.That(loop.State.Rift.Shards.ToDouble(), Is.EqualTo(0));
            Assert.That(loop.State.Echo.ToDouble(), Is.EqualTo(1e6 - TestWorld.Data.Artifacts.UnlockCost.At(1).ToDouble()));

            loop.Rift.Leave();
            Assert.That(loop.Artifacts.Owned.ContainsKey(home.Id), Is.True);
            Assert.That(loop.Artifacts.Owned.Count, Is.EqualTo(1));
        }

        [Test]
        public void NoPrestigeAndNoOfflineInside()
        {
            var loop = HomeAt(200);
            Assert.That(loop.Prestige.Available, Is.True);
            loop.State.RecentDps = BigNumber.FromDouble(1e6);

            loop.Rift.Enter(Inside);

            Assert.That(loop.Prestige.Available, Is.False);
            var offline = new OfflineCalculator(TestWorld.Data, loop.Hero).Simulate(loop.State, 3600);
            Assert.That(offline.Seconds, Is.EqualTo(0));
        }

        [Test]
        public void WindowEndSendsTheHeroHomeAndWipesTheRift()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            DefeatBoss(loop);
            loop.State.Rift.Shards = BigNumber.FromDouble(5);

            Assert.That(loop.Rift.CloseIfEnded(loop.State.Rift.WindowEndUnixMs - 1), Is.False);
            Assert.That(loop.Rift.CloseIfEnded(loop.State.Rift.WindowEndUnixMs), Is.True);

            Assert.That(loop.Rift.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(40));
            Assert.That(loop.State.Rift.Shards.ToDouble(), Is.EqualTo(0));
            Assert.That(loop.State.Rift.OtherWorld, Is.Null);
            Assert.That(loop.State.Rift.Deepest, Is.EqualTo(2));
        }

        [Test]
        public void NextWindowStartsFromScratch()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            DefeatBoss(loop);
            loop.Rift.Leave();

            var saturday = Rules.Schedule.Next(Inside).StartUnixMs + 1000;
            loop.Rift.Enter(saturday);

            Assert.That(loop.State.Stage, Is.EqualTo(1));
            Assert.That(loop.State.Rift.Deepest, Is.EqualTo(1));
        }

        [Test]
        public void ModifierIsLiftedByItsRole()
        {
            var (loop, index) = LoopInWindowWith("stone_skin");
            var damageInside = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.Rift.ModifierActive(index), Is.True);

            loop.Synergy.SetAlly(0, true, HeroClass.Minstrel);
            loop.Tick();

            Assert.That(loop.Rift.ModifierActive(index), Is.False);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.GreaterThan(damageInside));
        }

        [Test]
        public void ABadgeTellsWhoseRoleLiftsTheModifier()
        {
            var (loop, index) = LoopInWindowWith("stone_skin");
            var herald = TestWorld.Data.Synergy.RoleMask("herald");
            Assert.That(loop.Rift.LiftedBy(index), Is.EqualTo(RiftSystem.NotLifted));
            Assert.That(loop.Rift.LiftingRoles(index), Is.Zero);

            loop.Synergy.SetAlly(2, true, HeroClass.Lanternbearer);
            loop.Tick();

            Assert.That(loop.Rift.LiftedBy(index), Is.EqualTo(2), "the herald in slot 2 lifts it");
            Assert.That(loop.Rift.LiftingRoles(index), Is.EqualTo(herald));
            loop.Synergy.ClearAllies();
            loop.Tick();
            Assert.That(loop.Rift.LiftedBy(index), Is.EqualTo(RiftSystem.NotLifted));
        }

        [Test]
        public void TheWaveBarCountsTheHordeAndTheBossBarShowsTheShieldInSolo()
        {
            var loop = TestWorld.Loop();
            var plain = loop.Stages.EnemiesOnThisStage;
            loop.Hero.Add(new Modifier(CombatStats.StageEnemies, ModifierKind.Flat, 0.5));

            Assert.That(loop.Stages.EnemiesOnThisStage, Is.EqualTo((int)System.Math.Floor(plain * 1.5 + 1e-9)));

            loop.Hero.Add(new Modifier(CombatStats.BossShield, ModifierKind.Flat, 0.5));
            loop.State.KillsOnStage = loop.Stages.EnemiesOnThisStage;
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            loop.Stages.EnsureEnemy();
            Assert.That(loop.State.FightingBoss, Is.True);
            Assert.That(loop.Stages.ShieldShare, Is.EqualTo(1 / 1.5).Within(1e-6), "the top third of the boss is shield");
        }

        [Test]
        public void SaveKeepsBothWorlds()
        {
            var loop = HomeAt(40);
            loop.Rift.Enter(Inside);
            DefeatBoss(loop);
            loop.State.Rift.Shards = BigNumber.FromDouble(3);
            loop.State.Rift.Artifacts["a"] = 2;
            var clock = ClockStamp.First(new ClockReading(Inside, 1000, 1));

            var text = SaveCodec.Encode(loop.State, clock);
            var restored = SaveCodec.Decode(text, TestWorld.Data).State;

            Assert.That(SaveCodec.Encode(restored, clock), Is.EqualTo(text));
            Assert.That(restored.Rift.Inside, Is.True);
            Assert.That(restored.Rift.OtherWorld!.Stage, Is.EqualTo(40));
            var reloaded = TestWorld.Loop(state: restored);
            Assert.That(reloaded.Stages.Table.EnemyHp(1).ToDouble(), Is.EqualTo(loop.Stages.Table.EnemyHp(1).ToDouble()).Within(1e-9));
            Assert.That(reloaded.State.RecordStage, Is.EqualTo(40));
        }

        private static (GameLoop Loop, int Index) LoopInWindowWith(string modifierId)
        {
            var time = Inside;
            for (var week = 0; week < 200; week++)
            {
                var window = Rules.Schedule.Next(time);
                var picked = Rules.ModifiersFor(window.Id);
                for (var i = 0; i < picked.Count; i++)
                {
                    if (picked[i].Id != modifierId)
                        continue;
                    var loop = HomeAt(1);
                    Assert.That(loop.Rift.Enter(window.StartUnixMs + 1000), Is.EqualTo(RiftEntry.Entered));
                    loop.Tick();
                    return (loop, i);
                }
                time = window.EndUnixMs;
            }
            throw new AssertionException("no window picks " + modifierId);
        }
    }
}
