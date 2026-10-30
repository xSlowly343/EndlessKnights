using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Party;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyVectorTests
    {
        public const string UpdateVariable = "UPDATE_PARTY_VECTORS";
        private static readonly string VectorPath = Path.Combine("Server", "test-vectors", "party-encounter.json");

        [Test]
        public void ServerVectorsMatchTheCoreEncounter()
        {
            var json = Build();
            var path = RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            {
                File.WriteAllText(path, json);
                Assert.Pass("vectors written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(json), $"vectors are stale, run the tests with {UpdateVariable}=1");
        }

        private static string Build()
        {
            var scenarios = new List<string>
            {
                SoloStage(),
                LateDamage(),
                BossTimeout(),
                Effects(),
                Disconnects(),
                RestAndPeace(),
                Contribution(),
                Clockmaker(),
                Worlds(),
                FocusRelay(),
                HelpCall(),
                WeakenNext(),
                WeakenedRewards(),
                MedianWaves(),
                AllyCredit(),
                RiftEnemies(),
                SharedTimer()
            };
            var rules = TestWorld.Data.Party;
            var stages = TestWorld.Data.Stages;
            return "{\n  \"version\": 1,\n  \"rules\": {\n" +
                   $"    \"maxMembers\": {rules.MaxMembers},\n" +
                   $"    \"hpPerExtraPlayer\": {N(rules.HpPerExtraPlayer)},\n" +
                   $"    \"restAfterSeconds\": {N(rules.RestAfterSeconds)},\n" +
                   $"    \"disconnectGraceSeconds\": {N(rules.DisconnectGraceSeconds)},\n" +
                   $"    \"lateDamageSeconds\": {N(rules.LateDamageSeconds)},\n" +
                   $"    \"maxEnemyDamageTaken\": {N(rules.MaxEnemyDamageTaken)},\n" +
                   $"    \"maxWeakenedSpawns\": {rules.MaxWeakenedSpawns},\n" +
                   $"    \"maxWeakenShare\": {N(rules.MaxWeakenShare)},\n" +
                   $"    \"maxWeakenGold\": {N(rules.MaxWeakenGold)},\n" +
                   $"    \"minBossTimerRate\": {N(rules.Timer.MinBossRate)},\n" +
                   $"    \"sharedBossTimerRate\": {N(rules.Timer.SharedRate)},\n" +
                   $"    \"sharedTimerFighters\": {rules.Timer.SharedFighters},\n" +
                   $"    \"worldSwitchSeconds\": {N(rules.WorldSwitchSeconds)},\n" +
                   $"    \"minShare\": {N(rules.Contribution.MinShare)},\n" +
                   $"    \"activeInputSeconds\": {N(rules.Contribution.ActiveInputSeconds)},\n" +
                   $"    \"restBankSeconds\": {N(rules.Contribution.RestBankSeconds)},\n" +
                   $"    \"idlePortionShare\": {N(rules.Contribution.IdlePortionShare)},\n" +
                   $"    \"typingReserveShare\": {N(rules.Contribution.TypingReserveShare)},\n" +
                   $"    \"allyCreditMax\": {N(rules.Contribution.AllyCreditMax)},\n" +
                   $"    \"relaySenderSeconds\": {N(rules.Relay.SenderSeconds)},\n" +
                   $"    \"relayRecipientSeconds\": {N(rules.Relay.RecipientSeconds)},\n" +
                   $"    \"waveCount\": {stages.Waves.Count},\n" +
                   $"    \"waveFirstEnemies\": {stages.Waves.FirstEnemies},\n" +
                   $"    \"waveEnemiesPerWave\": {stages.Waves.EnemiesPerWave},\n" +
                   $"    \"waveStagesPerExtraEnemy\": {stages.Waves.StagesPerExtraEnemy},\n" +
                   $"    \"waveMaxEnemies\": {stages.Waves.MaxEnemies},\n" +
                   $"    \"bossRetryAfterKills\": {stages.BossRetryAfterKills},\n" +
                   $"    \"bossTimerSeconds\": {N(stages.BossTimerSeconds)},\n" +
                   $"    \"bossHpMultiplier\": {N(stages.BossHpMultiplier)}\n" +
                   "  },\n  \"scenarios\": [\n" + string.Join(",\n", scenarios) + "\n  ]\n}\n";
        }

        private static string WeakenNext()
        {
            var r = Recorder("weakened enemies come from the host one after another", 515);
            r.Weaken(3, 0.25);
            r.Join(1);
            r.Join(2);
            r.Weaken(3, 0.25);
            r.Hit(0, 2);
            r.Weaken(2, 0.1);
            r.Weaken(50, 0.9);
            for (var kill = 0; kill < 11; kill++)
                r.Hit(1, 2);
            r.Weaken(0, 0.5);
            r.Weaken(2, -1);
            r.Weaken(2, 0.2);
            r.SwitchWorld(PartyWorld.Rift, 1761652800);
            r.Hit(0, 2);
            return r.ToJson();
        }

        private static string RiftEnemies()
        {
            var r = Recorder("rift enemies regenerate, colossi carry a shield and hordes are bigger", 2424);
            r.Join(1);
            r.Join(2);
            r.RiftEnemy(0.05, 0.5, 0.5);
            r.Hit(0, 0.6);
            r.Advance(10);
            r.Hit(1, 0.2);
            r.Advance(100);
            while (!r.Encounter.FightingBoss)
            {
                r.Input(0);
                r.Input(1);
                r.Hit(0, 1);
                r.Hit(1, 1.5);
            }
            r.Hit(0, 0.4);
            r.RiftEnemy(0.05, 0, 0.5);
            r.Hit(1, 0.3);
            r.Leave(1);
            r.Advance(5);
            r.RiftEnemy(-1, 0.5, 0);
            r.Hit(0, 5);
            r.Hit(0, 5);
            return r.ToJson();
        }

        private static string AllyCredit()
        {
            var r = Recorder("a booster gets a share of the damage of the allies it boosts", 3131);
            r.Join(1);
            r.Join(2);
            r.Join(3);
            r.Credit(1, 0.1);
            r.Credit(2, 5);
            r.Credit(0, -1);
            r.Hit(0, 0.5);
            r.Auras(1, 20);
            r.Hit(0, 0.5);
            r.Effect(77, 0.1, 3, 0, 2);
            r.Hit(0, 0.6);
            r.Hit(1, 0.4);
            r.Hit(2, 1.5);
            r.Advance(30);
            r.Hit(0, 2);
            r.Hit(0, 3);
            r.JoinGuest(4, 30);
            r.Credit(3, 0.2);
            r.Auras(3, 50);
            r.Hit(0, 3);
            return r.ToJson();
        }

        private static string MedianWaves()
        {
            var r = Recorder("waves follow the median stage of the fighters and the depth in the rift", 909);
            r.Join(1);
            r.Stage(0, 5);
            r.Join(2);
            r.Stage(1, 95);
            r.Join(3);
            r.Stage(2, 41);
            r.Stage(2, -3);
            r.JoinGuest(4, 30);
            r.Stage(3, 900);
            while (!r.Encounter.FightingBoss)
            {
                r.Input(0);
                r.Input(1);
                r.Input(2);
                r.Hit(0, 1.5);
            }
            r.Peaceful(2, true);
            r.Hit(1, 50);
            r.Leave(1);
            r.Hit(0, 2);
            r.Advance(1900);
            r.Hit(0, 1);
            r.SwitchWorld(PartyWorld.Rift, 1761652800);
            while (r.Encounter.RiftStagesCleared < 2)
            {
                r.Input(0);
                r.Hit(0, 1.5);
            }
            r.Hit(0, 1);
            return r.ToJson();
        }

        private static string WeakenedRewards()
        {
            var r = Recorder("the host tells which enemy is weakened, with gold and vulnerability", 616);
            r.Join(1);
            r.Join(2);
            r.Weaken(2, 0.15, 0.2, 0.1, 4, 501, 1);
            r.Weaken(1, 0.1, 5, 0.05, 9, 502, 0);
            r.Hit(0, 2);
            r.Advance(10);
            r.Hit(1, 2);
            r.Advance(50);
            r.Hit(0, 2);
            r.Weaken(1, 0.2);
            r.Hit(1, 2);
            r.Effect(600, 0.15, 20, 0, 1);
            r.EndEffect(600, 0);
            r.Advance(3);
            r.EndEffect(600, 1);
            r.EndEffect(600, 1);
            r.Weaken(3, 0.2, 0.2, 0.1, 4, 501, 1);
            r.SwitchWorld(PartyWorld.Rift, 1761652800);
            r.Hit(0, 2);
            return r.ToJson();
        }

        private static string SharedTimer()
        {
            var r = Recorder("two fighters share the boss timer, a resting one and a guest do not", 5150);
            r.Join(1);
            r.Join(2);
            while (!r.Encounter.FightingBoss)
            {
                r.Input(1);
                r.Hit(0, 1.5);
            }
            r.Input(1);
            r.Advance(20);
            r.Effect(9, 0, 10, 0.7, 0);
            r.Input(0);
            r.Input(1);
            r.Advance(10);
            r.Peaceful(1, true);
            r.Advance(10);
            r.JoinGuest(70, 30);
            r.Input(2);
            r.Advance(10);
            r.Peaceful(1, false);
            r.Input(1);
            r.Advance(10);
            return r.ToJson();
        }

        private static string HelpCall()
        {
            var r = Recorder("a called friend hits the boss for a while without weight or reward", 4242);
            r.JoinGuest(70, 30);
            r.Join(1);
            r.Join(2);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 1.5);
            r.JoinGuest(1, 30);
            r.JoinGuest(70, 0);
            r.JoinGuest(70, 3);
            r.Advance(2);
            r.Hit(2, 0.4);
            r.Hit(0, 0.5);
            r.Hit(1, 0.4);
            r.Advance(10);
            r.Hit(2, 0.2);
            r.Advance(18);
            r.JoinGuest(71, 30);
            r.Hit(0, 0.3);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 1.5);
            r.Hit(2, 5);
            r.SwitchWorld(PartyWorld.Rift, 1761652800);
            return r.ToJson();
        }

        private static string FocusRelay()
        {
            var r = Recorder("focus goes to the one who typed last", 31);
            for (ulong player = 1; player <= 4; player++)
                r.Join(player);
            r.Advance(5);
            r.Input(1);
            r.Input(2);
            r.RelayTarget(0);
            r.Advance(3);
            r.Input(2);
            r.RelayTarget(0);
            r.Peaceful(2, true);
            r.RelayTarget(0);
            r.Connected(1, false);
            r.RelayTarget(0);
            r.Advance(25);
            r.RelayTarget(0);
            r.Input(3);
            r.RelayTarget(0);
            r.RelayTarget(3);
            r.Advance(100);
            r.Input(3);
            r.RelayTarget(0);
            r.RelayTarget(9);
            return r.ToJson();
        }

        private static PartyVectorRecorder Recorder(string name, ulong seed) =>
            new PartyVectorRecorder(name, TestWorld.Data.Party, TestWorld.Data.Stages, seed);

        private static string SoloStage()
        {
            var r = Recorder("solo player clears a stage", 7);
            r.Join(1001);
            for (var enemy = 0; enemy <= TestWorld.Data.Stages.EnemiesPerStage; enemy++)
            {
                r.Advance(3);
                r.Hit(0, 0.4);
                r.Hit(0, 0.7);
            }
            r.Advance(5);
            r.Hit(0, 2);
            return r.ToJson();
        }

        private static string LateDamage()
        {
            var r = Recorder("late damage moves to the next enemy", 12345678901234567UL);
            r.Join(1);
            r.Join(2);
            r.Hit(0, 1.5);
            r.Hit(1, 0.25);
            var killed = r.Encounter.EnemyId;
            r.Advance(1);
            r.Hit(0, 0.5);
            r.Damage(1, killed, 0.4);
            r.Advance(15);
            r.Damage(1, killed, 0.4);
            r.Damage(0, 999, 0.4);
            r.Damage(0, r.Encounter.EnemyId, -0.5);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 3);
            var beforeBoss = r.Encounter.EnemyId - 1;
            r.Damage(1, beforeBoss, 0.6);
            r.Join(3);
            return r.ToJson();
        }

        private static string BossTimeout()
        {
            var r = Recorder("boss timer runs out and the party farms", ulong.MaxValue);
            r.Join(42);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 1);
            r.RequestBoss();
            r.Hit(0, 0.5);
            r.Advance(r.Encounter.BossTicksLeft - 1);
            r.Advance(1);
            r.RequestBoss();
            for (var kill = 0; kill < TestWorld.Data.Stages.BossRetryAfterKills; kill++)
                r.Hit(0, 1);
            r.Hit(0, 1);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 1);
            r.Advance(r.Encounter.BossTicksLeft);
            r.RequestBoss();
            r.Input(0);
            return r.ToJson();
        }

        private static string Effects()
        {
            var r = Recorder("enemy effects stack up to the cap", 99);
            r.Effect(5, 0.5, 1);
            r.Join(7);
            r.Join(8);
            r.Effect(5, 0.15, 10);
            r.Effect(5, 0.1, 20);
            r.Effect(5, 0.2, 2);
            r.Effect(6, 0.5, 3);
            r.Effect(-17, 0.9, 1);
            r.Effect(8, 0, 5);
            r.Hit(0, 0.3);
            r.Advance(10);
            r.Hit(1, 0.3);
            r.Advance(25);
            r.Hit(1, 0.3);
            r.Advance(80);
            r.Hit(0, 0.3);
            return r.ToJson();
        }

        private static string Disconnects()
        {
            var r = Recorder("disconnects shrink the boss bar", 2024);
            r.Join(11);
            r.Join(12);
            r.Join(13);
            r.Join(14);
            r.Join(15);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 2);
            r.Hit(1, 0.8);
            r.Connected(2, false);
            r.Advance(100);
            r.Connected(2, true);
            r.Connected(3, false);
            r.Advance(301);
            r.Leave(1);
            r.Hit(0, 0.5);
            r.Leave(1);
            r.Join(16);
            r.Hit(0, 10);
            r.Leave(0);
            r.Hit(2, 0.2);
            return r.ToJson();
        }

        private static string RestAndPeace()
        {
            var r = Recorder("resting and peaceful members get no weight", 31337);
            r.Join(21);
            r.Join(22);
            r.Join(23);
            r.Peaceful(2, true);
            r.Hit(0, 3);
            r.Advance(1700);
            r.Input(0);
            r.Advance(150);
            r.Hit(0, 2);
            r.Peaceful(2, false);
            r.Input(2);
            r.Hit(0, 2);
            r.Peaceful(5, true);
            r.Input(-1);
            return r.ToJson();
        }

        private static string Contribution()
        {
            var r = Recorder("idle damage is capped and the rest bank pays the idle", 777);
            r.Join(31);
            r.Join(32);
            r.Join(33);
            for (var i = 0; i < 40; i++)
            {
                r.Input(0);
                r.Input(1);
                r.Input(2);
                r.Advance(1);
            }
            r.Hit(0, 3);
            r.Hit(0, 0.2);
            for (var i = 0; i < 310; i++)
            {
                r.Input(0);
                r.Advance(1);
            }
            r.Hit(1, 10);
            r.Hit(2, 10);
            r.Hit(0, 0.3);
            r.Hit(0, 0.3);
            r.Advance(1500);
            r.Hit(1, 0.5);
            r.Hit(0, 10);
            r.Hit(0, 10);
            r.Hit(1, 0.05);
            r.Hit(0, 10);
            r.Input(2);
            r.Hit(2, 10);
            return r.ToJson();
        }

        private static string Clockmaker()
        {
            var r = Recorder("timer slows and the chimes repeat party damage", 4242);
            r.Join(51);
            r.Join(52);
            r.Repeat(0, 5);
            r.Hit(0, 0.6);
            r.Advance(3);
            r.Hit(1, 0.5);
            r.Advance(2);
            r.Repeat(1, 5);
            r.Repeat(1, 0);
            r.Repeat(3, 5);
            while (!r.Encounter.FightingBoss)
            {
                r.Input(0);
                r.Input(1);
                r.Hit(0, 1.5);
                r.Advance(1);
            }
            r.Effect(77, 0.1, 3, 0.3);
            r.Effect(78, 0, 2, 0.5);
            r.Effect(79, 0, 2, -0.5);
            r.Effect(80, 0, 2, 0);
            r.Effect(81, 0, 5, 0.9);
            r.Hit(0, 0.2);
            r.Advance(7);
            r.Hit(1, 0.3);
            r.Advance(25);
            r.Hit(0, 0.4);
            r.Advance(30);
            r.Repeat(0, 3);
            r.Repeat(1, 20);
            r.Hit(0, 50);
            r.Advance(1);
            r.Repeat(0, 7);
            return r.ToJson();
        }

        private static string Worlds()
        {
            var r = Recorder("the party moves to the rift and back", 8080);
            r.ProposeWorld(PartyWorld.Rift, 1761652800);
            r.Join(61);
            r.Join(62);
            r.ProposeWorld(PartyWorld.Home, 0);
            r.ProposeWorld(PartyWorld.Rift, 0);
            r.ProposeWorld(PartyWorld.Home, 5);
            r.Hit(0, 0.4);
            r.Effect(90, 0.2, 30);
            r.ProposeWorld(PartyWorld.Rift, 1761652800);
            r.ProposeWorld(PartyWorld.Rift, 1761998400);
            r.Advance(50);
            r.CancelWorld();
            r.CancelWorld();
            r.Advance(30);
            r.ProposeWorld(PartyWorld.Rift, 1761652800);
            r.Hit(1, 0.3);
            r.Advance(99);
            r.Advance(1);
            r.Hit(0, 0.2);
            r.Damage(1, r.Encounter.EnemyId - 1, 0.3);
            r.ProposeWorld(PartyWorld.Rift, 1761652800);
            while (!r.Encounter.FightingBoss)
                r.Hit(0, 1);
            r.SwitchWorld(PartyWorld.Home, 0);
            r.Advance(1);
            return r.ToJson();
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static string RepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Server")) && Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                    return Path.Combine(directory.FullName, relativePath);
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Server folder not found above " + Directory.GetCurrentDirectory());
        }
    }
}
