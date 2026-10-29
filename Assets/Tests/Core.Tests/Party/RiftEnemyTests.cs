using System;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class RiftEnemyTests
    {
        private const ulong HostId = 1;

        [Test]
        public void ARegeneratingEnemyHealsAShareOfItsHpEverySecond()
        {
            var loop = TestWorld.Loop();
            loop.Hero.Add(new Modifier(CombatStats.EnemyRegen, ModifierKind.Flat, 0.01));
            loop.Tick();
            var max = loop.State.EnemyMaxHp;
            loop.State.EnemyHp = max * 0.5;

            for (var tick = 0; tick < GameLoop.TicksPerSecond; tick++)
                loop.Tick();

            Assert.That((loop.State.EnemyHp / max).ToDouble(), Is.EqualTo(0.51).Within(1e-9));
        }

        [Test]
        public void TheColossusShieldIsTheTopOfTheBossHpAndFallsWithTheMarks()
        {
            var loop = TestWorld.Loop();
            var shield = new Modifier(CombatStats.BossShield, ModifierKind.Flat, 0.5);
            loop.Hero.Add(shield);
            loop.Tick();
            loop.State.Farming = true;
            Assert.That(loop.Stages.RequestBossFight(), Is.True);
            var max = loop.State.EnemyMaxHp;
            var floor = loop.State.EnemyShieldFloor;
            Assert.That((max / floor).ToDouble(), Is.EqualTo(1.5).Within(1e-9));
            loop.State.EnemyHp = max * 0.9;

            loop.Hero.Remove(shield);
            loop.Tick();

            Assert.That((loop.State.EnemyHp / floor).ToDouble(), Is.EqualTo(1).Within(1e-9), "the shield is gone, the hp under it stays");
            Assert.That(loop.State.EnemyShieldFloor.Sign, Is.Zero);
        }

        [Test]
        public void AHordeBringsHalfAsManyEnemiesAgainBeforeTheBoss()
        {
            var input = new Game.Core.Input.FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.Hero.Add(new Modifier(CombatStats.StageEnemies, ModifierKind.Increased, 0.5));
            loop.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e9));
            var normal = TestWorld.Data.Stages.EnemiesOnStage(1);
            var kills = 0;
            while (loop.State.EnemyType == EnemyType.Normal && kills < 100)
            {
                input.PressKeys(1);
                loop.Tick();
                kills += loop.Events.CountOf(GameEventKind.EnemyKilled);
                loop.Events.Clear();
            }

            Assert.That(kills, Is.EqualTo((int)System.Math.Floor(normal * 1.5)));
        }

        [Test]
        public void ThePartyHostUsesTheRiftModifiersUntilTheMarksOfThePartyLiftThem()
        {
            var rift = TestWorld.Data.Rift;
            var synergy = TestWorld.Data.Synergy;
            var start = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            long riftId = 0;
            for (var week = 0; week < 200 && riftId == 0; week++)
            {
                foreach (var offset in new[] { 0L, 4L })
                {
                    var window = rift.Schedule.WindowAt(start + (week * 7 + offset) * 86_400_000L + 3_600_000);
                    if (window == null)
                        continue;
                    var picked = rift.ModifiersFor(window.Value.Id);
                    if (picked.Count == 2 && Has(picked, "regeneration") && Has(picked, "horde"))
                        riftId = window.Value.Id + 60;
                }
            }
            Assert.That(riftId, Is.Not.Zero, "a window with regeneration and a horde");

            var network = new LoopbackNetwork(3, 1, 1, 0, 4096);
            var host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 5), network.Connect(HostId), TestWorld.Data.Party, rift, synergy)
            {
                ClockUnixMs = riftId * 1000
            };
            var member = network.Connect(10);
            var buffer = new byte[PartyCodec.MaxPacketBytes];
            member.Send(HostId, buffer, PartyCodec.Encode(buffer, new HelloMessage { HeroClass = 0, Stage = 5 }));
            network.Advance(10);
            host.Tick();
            host.Encounter.SwitchWorld(PartyWorld.Rift, riftId);
            host.Tick();

            Assert.That(host.Encounter.EnemyRegen, Is.EqualTo(0.01).Within(1e-12));
            Assert.That(host.Encounter.ExtraStageEnemies, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(host.Encounter.EnemiesOnStage, Is.EqualTo((int)System.Math.Floor(TestWorld.Data.Stages.EnemiesOnStage(1) * 1.5)));

            var poison = synergy.MaskOf(new[] { "poison" });
            member.Send(HostId, buffer, PartyCodec.Encode(buffer, new MarksMessage { Marks = (uint)poison }));
            network.Advance(10);
            host.Tick();

            Assert.That(host.Encounter.EnemyRegen, Is.Zero, "poison stops the regeneration");
            Assert.That(host.Encounter.ExtraStageEnemies, Is.EqualTo(0.5).Within(1e-12), "a horde has no mark against it");

            host.Encounter.SwitchWorld(PartyWorld.Home, 0);
            host.Tick();
            Assert.That(host.Encounter.ExtraStageEnemies, Is.Zero);
        }

        private static bool Has(System.Collections.Generic.IReadOnlyList<Game.Core.Content.Rift.RiftModifier> picked, string id)
        {
            foreach (var modifier in picked)
            {
                if (modifier.Id == id)
                    return true;
            }
            return false;
        }
    }
}
