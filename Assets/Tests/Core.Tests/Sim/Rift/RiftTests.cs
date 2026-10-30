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
        public void ModifierIsLiftedByItsMarkAndThatMarkIsFavoured()
        {
            var (loop, index) = LoopInWindowWith("stone_skin");
            var damageInside = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.Rift.ModifierActive(index), Is.True);

            loop.Synergy.SetAlly(0, true, HeroClass.Voodoo, TestWorld.Data.Synergy.MaskOf(new[] { "curse" }));
            loop.Tick();

            Assert.That(loop.Rift.ModifierActive(index), Is.False);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.GreaterThan(damageInside));
            Assert.That(loop.Synergy.FavouredMarks & TestWorld.Data.Synergy.MaskOf(new[] { "curse" }), Is.Not.EqualTo(0));

            loop.Rift.Leave();
            Assert.That(loop.Synergy.FavouredMarks, Is.EqualTo(0));
        }

        [Test]
        public void FavouredMarkBoostsTheResponse()
        {
            var loop = TestWorld.Loop();
            loop.State.Class = HeroClass.Viking;
            var curse = TestWorld.Data.Synergy.MaskOf(new[] { "curse" });
            loop.Synergy.SetAlly(2, true, HeroClass.Voodoo, curse);
            loop.Tick();
            var rage = new StatId("resourceGain");
            var plain = loop.Hero.Value(rage, 1) - 1;

            loop.Synergy.SetFavoured(curse, Rules.MarkBonus);

            Assert.That(loop.Hero.Value(rage, 1) - 1, Is.EqualTo(plain * (1 + Rules.MarkBonus)).Within(1e-9));
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
