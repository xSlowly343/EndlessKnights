using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyClientTests
    {
        private const ulong HostId = 1;

        private sealed class Player
        {
            public Player(LoopbackNetwork network, ulong id, long stage)
            {
                Id = id;
                Input = new FakeInputSource();
                var hero = new StatSheet();
                var hp = TestWorld.Data.Stages.EnemyHp(stage).ToDouble();
                hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, hp / (10 * TestWorld.Data.Combat.KeyDamage) - 1));
                Loop = TestWorld.Loop(Input, new GameState { Stage = stage, HighestStage = stage, RecordStage = stage }, hero);
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
            }

            public ulong Id { get; }
            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public int SoloKills { get; private set; }
            public int AllKills { get; private set; }

            public void Tick(bool typing)
            {
                if (typing)
                    Input.PressKeys(1);
                var solo = Client.Status != PartyLink.Linked && Client.Status != PartyLink.NoSignal;
                Loop.Tick();
                var kills = Loop.Events.CountOf(GameEventKind.EnemyKilled);
                AllKills += kills;
                if (solo)
                    SoloKills += kills;
                Loop.Events.Clear();
            }
        }

        private sealed class Rig
        {
            public Rig(params long[] stages) : this(0, stages)
            {
            }

            public Rig(double loss, params long[] stages)
            {
                Network = new LoopbackNetwork(5, 20, 120, loss, 4096);
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 11), Network.Connect(HostId), TestWorld.Data.Party);
                Players = new Player[stages.Length];
                for (var i = 0; i < stages.Length; i++)
                {
                    Players[i] = new Player(Network, 10UL + (ulong)i, stages[i]);
                    Players[i].Client.Join();
                }
            }

            public LoopbackNetwork Network { get; }
            public PartyHost Host { get; }
            public Player[] Players { get; }

            public void Run(double seconds, bool typing = true)
            {
                var ticks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
                for (var tick = 0; tick < ticks; tick++)
                {
                    Network.Advance(100);
                    foreach (var player in Players)
                        player.Tick(typing);
                    Host.Tick();
                }
            }
        }

        [Test]
        public void TwoPlayersOnDifferentStagesFightOneEnemy()
        {
            var rig = new Rig(5, 40);

            rig.Run(60);

            Assert.That(rig.Host.Encounter.MemberCount, Is.EqualTo(2));
            Assert.That(rig.Host.Kills, Is.GreaterThan(15));
            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
                Assert.That(player.Client.PartyRewards, Is.InRange(rig.Host.Kills - 3, rig.Host.Kills));
                Assert.That(player.Loop.State.Gold.Sign, Is.GreaterThan(0));
            }
            Assert.That(rig.Players[0].Loop.State.Stage, Is.GreaterThan(5));
            Assert.That(rig.Players[1].Loop.State.Stage - 40, Is.EqualTo(rig.Players[0].Loop.State.Stage - 5));
        }

        [Test]
        public void FivePercentLossStillRewardsEveryKillOnce()
        {
            var rig = new Rig(0.05, 5, 40, 120);

            rig.Run(60);

            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
                Assert.That(player.Client.PartyRewards, Is.InRange(rig.Host.Kills - 3, rig.Host.Kills));
                Assert.That(player.AllKills, Is.EqualTo(player.Client.PartyRewards + player.SoloKills));
            }
            var cleared = rig.Players[0].Loop.State.Stage - 5;
            Assert.That(cleared, Is.GreaterThan(0));
            Assert.That(rig.Players[1].Loop.State.Stage - 40, Is.EqualTo(cleared));
            Assert.That(rig.Players[2].Loop.State.Stage - 120, Is.EqualTo(cleared));
        }

        [Test]
        public void LinkedTicksDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var rig = new Rig(0.05, 5, 40);
            rig.Run(60);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            rig.Run(60);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void OwnHitsLowerThePredictedBarAtOnce()
        {
            var rig = new Rig(5, 40);
            rig.Run(3, false);
            var player = rig.Players[0];
            Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
            var before = player.Client.PredictedBarLeft;

            player.Input.PressKeys(1);
            player.Loop.Tick();
            player.Loop.Tick();

            Assert.That(player.Client.PredictedBarLeft, Is.LessThan(before));
            Assert.That(player.Loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(TestWorld.Data.Stages.EnemyHp(5).ToDouble() * player.Loop.State.Enemy!.HpMultiplier * player.Client.BarFull).Within(1e-6));
        }

        [Test]
        public void ThirtySecondDisconnectGoesSoloAndReturnsWithoutDoubleRewards()
        {
            var rig = new Rig(5, 40);
            rig.Run(20);
            var b = rig.Players[1];
            var killsBefore = rig.Host.Kills;

            rig.Network.SetLinkDown(b.Id, true);
            rig.Run(10);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.NoSignal));
            var rewardsAtLoss = b.Client.PartyRewards;
            rig.Run(25);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Solo));
            Assert.That(b.Client.PartyRewards, Is.EqualTo(rewardsAtLoss));
            Assert.That(b.SoloKills, Is.GreaterThan(0));
            var killsWhileAway = rig.Host.Kills - killsBefore;

            rig.Network.SetLinkDown(b.Id, false);
            var killsAtReturn = rig.Host.Kills;
            rig.Run(20);

            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Linked));
            Assert.That(rig.Players[0].Client.Status, Is.EqualTo(PartyLink.Linked));
            Assert.That(killsWhileAway, Is.GreaterThan(0));
            Assert.That(b.Client.PartyRewards, Is.LessThanOrEqualTo(killsBefore + (rig.Host.Kills - killsAtReturn)));
            Assert.That(b.AllKills, Is.EqualTo(b.Client.PartyRewards + b.SoloKills));
        }

        [Test]
        public void LongAbsenceStaysAwayUntilJoinedAgain()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var b = rig.Players[1];

            rig.Network.SetLinkDown(b.Id, true);
            rig.Run(TestWorld.Data.Party.DisconnectGraceSeconds + TestWorld.Data.Party.RejoinWindowSeconds + 5);
            rig.Network.SetLinkDown(b.Id, false);
            rig.Run(10);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Away));

            b.Client.Join();
            rig.Run(5);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Linked));
        }

        [Test]
        public void LeavingGoesSoloAndIgnoresTheParty()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var a = rig.Players[0];

            a.Client.Leave();
            rig.Run(10);

            Assert.That(a.Client.Status, Is.EqualTo(PartyLink.Solo));
            Assert.That(a.Loop.Stages.Linked, Is.False);
            Assert.That(a.SoloKills, Is.GreaterThan(0));
        }
    }
}
