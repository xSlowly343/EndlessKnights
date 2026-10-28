using System;
using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyRiftTests
    {
        private const ulong HostId = 1;
        private static readonly long TuesdayNoon = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        private static readonly long Inside = TuesdayNoon + 3_600_000;
        private static long WindowEnd => TuesdayNoon + TestWorld.Data.Rift.Schedule.DurationMs;

        private sealed class Member
        {
            public Member(LoopbackNetwork network, ulong id, long stage)
            {
                Input = new FakeInputSource();
                var hero = new StatSheet();
                var hp = TestWorld.Data.Stages.EnemyHp(stage).ToDouble();
                hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, hp / (10 * TestWorld.Data.Combat.KeyDamage) - 1));
                Loop = TestWorld.Loop(Input, new GameState { Stage = stage, HighestStage = stage, RecordStage = stage }, hero);
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
                Client.Join();
            }

            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public int Kills { get; private set; }

            public void Tick()
            {
                Input.PressKeys(1);
                Loop.Tick();
                Kills += Loop.Events.CountOf(GameEventKind.EnemyKilled);
                Loop.Events.Clear();
            }
        }

        private sealed class Rig
        {
            public Rig(params long[] stages)
            {
                Network = new LoopbackNetwork(5, 20, 60, 0, 4096);
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 9), Network.Connect(HostId), TestWorld.Data.Party,
                    TestWorld.Data.Rift) { ClockUnixMs = Inside };
                Members = new Member[stages.Length];
                for (var i = 0; i < stages.Length; i++)
                    Members[i] = new Member(Network, 10UL + (ulong)i, stages[i]);
                Host.LeaderId = 10;
                Members[0].Client.IsLeader = true;
            }

            public long RiftEnd => Host.Encounter.RiftId * 1000 + TestWorld.Data.Rift.PartyDurationMs;

            public LoopbackNetwork Network { get; }
            public PartyHost Host { get; }
            public Member[] Members { get; }

            public void Run(double seconds)
            {
                var ticks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
                for (var tick = 0; tick < ticks; tick++)
                {
                    Network.Advance(100);
                    foreach (var member in Members)
                        member.Tick();
                    Host.ClockUnixMs += 100;
                    Host.Tick();
                }
            }
        }

        [Test]
        public void ThePartyGoesIntoTheRiftTogetherAfterTheCountdown()
        {
            var rig = new Rig(40, 7);
            rig.Run(5);
            var (first, second) = (rig.Members[0], rig.Members[1]);

            Assert.That(first.Client.ProposeWorld(PartyWorld.Rift, Inside), Is.True);
            rig.Run(1);
            Assert.That(second.Client.WorldSwitching, Is.True);
            Assert.That(second.Client.PendingWorld, Is.EqualTo(PartyWorld.Rift));
            Assert.That(second.Client.WorldSwitchSecondsLeft, Is.InRange(8, 10));
            Assert.That(second.Loop.Rift.Inside, Is.False, "nobody moves before the countdown ends");

            rig.Run(10);

            foreach (var member in rig.Members)
            {
                Assert.That(member.Loop.Rift.Inside, Is.True);
                Assert.That(member.Loop.State.Rift.WindowId, Is.EqualTo(rig.Host.Encounter.RiftId), "the party plays its own rift");
                Assert.That(member.Loop.State.Rift.WindowEndUnixMs, Is.EqualTo(rig.RiftEnd));
                Assert.That(member.Client.World, Is.EqualTo(PartyWorld.Rift));
            }
            Assert.That(rig.RiftEnd, Is.GreaterThan(WindowEnd), "a rift opened inside the window lasts its own 24 hours");
            Assert.That(first.Loop.Rift.WindowModifiers, Is.EqualTo(TestWorld.Data.Rift.ModifiersFor(TuesdayNoon / 1000)), "the window picks the modifiers");
            Assert.That(first.Loop.State.Stage, Is.EqualTo(1), "the rift starts from the first stage");
            rig.Run(10);
            Assert.That(first.Client.EnemyId, Is.EqualTo(second.Client.EnemyId));
            Assert.That(first.Client.PartyRewards, Is.GreaterThan(0));
        }

        [Test]
        public void ThePanelButtonFollowsTheWindowAndTheCountdown()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
            var rig = new Rig(10, 10);
            var client = rig.Members[0].Client;
            Assert.That(PartyRiftButton.Of(client, Inside), Is.EqualTo(PartyRiftAction.None), "solo has no party rift");
            rig.Run(5);

            Assert.That(PartyRiftButton.Of(client, TuesdayNoon - 1), Is.EqualTo(PartyRiftAction.None), "no button while the rift is closed");
            Assert.That(PartyRiftButton.Of(client, Inside), Is.EqualTo(PartyRiftAction.Enter));
            Assert.That(PartyRiftButton.Label(PartyRiftAction.Enter, client), Is.EqualTo("В Разлом отрядом"));
            Assert.That(PartyRiftButton.Perform(PartyRiftAction.Enter, client, Inside), Is.True);
            rig.Run(1);
            Assert.That(PartyRiftButton.Of(client, Inside), Is.EqualTo(PartyRiftAction.Cancel));
            Assert.That(PartyRiftButton.Label(PartyRiftAction.Cancel, client), Does.Match("^В Разлом через (9|10) с · отменить$"));
            rig.Run(10);
            Assert.That(PartyRiftButton.Of(client, Inside), Is.EqualTo(PartyRiftAction.Home));
            Assert.That(PartyRiftButton.Perform(PartyRiftAction.Home, client, Inside), Is.True);
            rig.Run(1);
            Assert.That(PartyRiftButton.Label(PartyRiftAction.Cancel, client), Does.StartWith("Домой через"));
            rig.Run(10);
            Assert.That(rig.Members[1].Loop.Rift.Inside, Is.False);
        }

        [Test]
        public void OnlyTheLeaderLeadsIntoTheRiftAndCallsItOff()
        {
            var rig = new Rig(10, 10);
            rig.Run(5);
            var (leader, member) = (rig.Members[0], rig.Members[1]);

            member.Client.ProposeWorld(PartyWorld.Rift, Inside);
            rig.Run(2);
            Assert.That(rig.Host.Encounter.Switching, Is.False, "a member cannot start the move");
            Assert.That(PartyRiftButton.Of(member.Client, Inside), Is.EqualTo(PartyRiftAction.None));

            leader.Client.ProposeWorld(PartyWorld.Rift, Inside);
            rig.Run(2);
            Assert.That(PartyRiftButton.Of(member.Client, Inside), Is.EqualTo(PartyRiftAction.Countdown));
            member.Client.CancelWorldSwitch();
            rig.Run(1);
            Assert.That(rig.Host.Encounter.Switching, Is.True, "a member cannot call it off");
            leader.Client.CancelWorldSwitch();
            rig.Run(12);

            Assert.That(leader.Loop.Rift.Inside, Is.False);
            Assert.That(member.Client.WorldSwitching, Is.False);
            Assert.That(rig.Host.Encounter.World, Is.EqualTo(PartyWorld.Home));
        }

        [Test]
        public void TheHostRefusesAClosedRiftAndASoloEntry()
        {
            var rig = new Rig(10, 10);
            rig.Run(5);
            rig.Host.ClockUnixMs = WindowEnd + 1000;

            Assert.That(rig.Members[0].Client.ProposeWorld(PartyWorld.Rift, Inside), Is.True, "the client still believes the window is open");
            rig.Run(2);

            Assert.That(rig.Host.Encounter.Switching, Is.False);
            Assert.That(rig.Members[0].Loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.InParty));
        }

        [Test]
        public void TheRiftEndBringsEveryoneHomeWithoutAFreeKill()
        {
            var rig = new Rig(40, 40);
            rig.Run(5);
            rig.Members[0].Client.ProposeWorld(PartyWorld.Rift, Inside);
            rig.Run(9);
            var homeStages = new[] { rig.Members[0].Loop.State.Stage, rig.Members[1].Loop.State.Stage };
            rig.Run(3);
            Assert.That(rig.Members[1].Loop.Rift.Inside, Is.True);
            var rewards = rig.Members[1].Client.PartyRewards;
            var kills = rig.Host.Kills;
            var riftEnd = rig.RiftEnd;
            rig.Host.ClockUnixMs = WindowEnd + 3_600_000;
            rig.Run(1);
            Assert.That(rig.Members[1].Loop.Rift.Inside, Is.True, "the closed window does not end a rift already open");

            rig.Host.ClockUnixMs = riftEnd;
            rig.Run(0.1);
            rig.Run(2);

            for (var i = 0; i < rig.Members.Length; i++)
            {
                var member = rig.Members[i];
                Assert.That(member.Loop.Rift.Inside, Is.False);
                Assert.That(member.Loop.State.Stage, Is.EqualTo(homeStages[i]), "home progress comes back");
                Assert.That(member.Loop.Rift.CloseIfEnded(riftEnd), Is.True, "the rift is wiped once the hero is home");
            }
            Assert.That(rig.Members[1].Client.PartyRewards - rewards, Is.LessThanOrEqualTo(rig.Host.Kills - kills), "the abandoned rift enemy pays nothing");
        }

        [Test]
        public void AfterLeavingThePartyTheHeroCanGoOnInTheSameRiftAlone()
        {
            var rig = new Rig(10, 10);
            rig.Run(5);
            rig.Members[0].Client.ProposeWorld(PartyWorld.Rift, Inside);
            rig.Run(12);
            var hero = rig.Members[1];
            var riftId = hero.Loop.State.Rift.WindowId;
            hero.Client.Leave();
            hero.Loop.Rift.Leave();

            Assert.That(hero.Loop.Rift.Enter(WindowEnd + 3_600_000), Is.EqualTo(RiftEntry.Entered), "the party rift still runs after the window closed");
            Assert.That(hero.Loop.State.Rift.WindowId, Is.EqualTo(riftId));
        }

        [Test]
        public void ARiftHeroStaysInWhileThePartyStillFightsThere()
        {
            var rig = new Rig(10, 10);
            rig.Run(5);
            rig.Members[0].Client.ProposeWorld(PartyWorld.Rift, Inside);
            rig.Run(12);

            Assert.That(rig.Members[0].Loop.Rift.CloseIfEnded(rig.RiftEnd), Is.False, "the host closes the party rift, not the client clock");
            Assert.That(rig.Members[0].Loop.Rift.Inside, Is.True);
        }
    }
}
