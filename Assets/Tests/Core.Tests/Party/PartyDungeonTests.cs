using System.Linq;
using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyDungeonTests
    {
        private const ulong HostId = 1;

        private static Game.Core.Content.DungeonRules Rules => TestWorld.Data.Dungeons;

        private sealed class Member
        {
            public Member(LoopbackNetwork network, ulong id, long stage, double strikesPerEnemy)
            {
                Input = new FakeInputSource();
                var hero = new StatSheet();
                var hp = TestWorld.Data.Stages.EnemyHp(stage).ToDouble();
                hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, hp / (strikesPerEnemy * TestWorld.Data.Combat.StrikeDamage) - 1));
                Loop = TestWorld.Loop(Input, new GameState { Stage = stage, HighestStage = stage, RecordStage = stage }, hero);
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
                Client.Join();
            }

            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public int DungeonDrops { get; private set; }

            public void Tick()
            {
                Input.PressKeys(1);
                Loop.Tick();
                for (var i = 0; i < Loop.Events.Count; i++)
                {
                    if (Loop.Events[i].Drop?.Source == Game.Core.Content.Items.DropSource.Dungeon)
                        DungeonDrops++;
                }
                Loop.Events.Clear();
            }
        }

        private sealed class Rig
        {
            public Rig(double strikesPerEnemy, params long[] stages)
            {
                Network = new LoopbackNetwork(5, 20, 60, 0, 4096);
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 9), Network.Connect(HostId), TestWorld.Data.Party,
                    TestWorld.Data.Rift);
                Members = new Member[stages.Length];
                for (var i = 0; i < stages.Length; i++)
                    Members[i] = new Member(Network, 10UL + (ulong)i, stages[i], strikesPerEnemy);
                Host.LeaderId = 10;
                Members[0].Client.IsLeader = true;
            }

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

            public bool RunUntil(System.Func<bool> done, double seconds)
            {
                for (var second = 0; second < seconds && !done(); second++)
                    Run(1);
                return done();
            }
        }

        private static DungeonKey KeyAt(int tier, long stage, params string[] modifiers) => new DungeonKey(tier, modifiers, stage, Rules.KeyAttempts);

        [Test]
        public void ThePartyIdCarriesTheWholeKey()
        {
            var id = PartyDungeonId.Pack(54321, 13, 0b101001, 3, 2, 3);

            Assert.That(PartyDungeonId.Stage(id), Is.EqualTo(54321));
            Assert.That(PartyDungeonId.Tier(id), Is.EqualTo(13));
            Assert.That(PartyDungeonId.ModifierMask(id), Is.EqualTo(0b101001));
            Assert.That(PartyDungeonId.Owner(id), Is.EqualTo(3));
            Assert.That(PartyDungeonId.Stages(id), Is.EqualTo(2));
            Assert.That(PartyDungeonId.Attempts(id), Is.EqualTo(3));
            Assert.That(PartyDungeonId.Pack(PartyDungeonId.MaxStage, 255, 0xFFF, 3, 7, 7), Is.LessThan(1L << 53), "the server reads the id as a double");
            Assert.That(PartyDungeonId.Pack(300, 2, 5, 1, 2, 3), Is.EqualTo(1847201043054892L), "the same key the server tests unpack");

            var loop = TestWorld.Loop();
            var key = KeyAt(2, 420, "hurry", "thick_hide");
            var back = loop.Dungeons.KeyOf(loop.Dungeons.PartyId(key, 1));
            Assert.That(back.Tier, Is.EqualTo(2));
            Assert.That(back.Stage, Is.EqualTo(420));
            Assert.That(back.Attempts, Is.EqualTo(Rules.KeyAttempts));
            Assert.That(back.Modifiers, Is.EquivalentTo(new[] { "hurry", "thick_hide" }));
        }

        [Test]
        public void EveryMemberSeesTheKeysOfTheWholeParty()
        {
            var rig = new Rig(10, 400, 400);
            var leader = rig.Members[0];
            var member = rig.Members[1];
            leader.Loop.State.Dungeons.Keys.Add(KeyAt(1, 200));
            for (var i = 0; i < 12; i++)
                member.Loop.State.Dungeons.Keys.Add(KeyAt(1 + i % 3, 300 + i));
            member.Loop.State.Dungeons.Keys.Add(new DungeonKey(9, System.Array.Empty<string>(), 40, Rules.KeyAttempts, 77));

            rig.Run(8);

            var memberSlot = member.Client.OwnSlot;
            var leaderSlot = leader.Client.OwnSlot;
            Assert.That(leader.Client.PartyKeyCount(memberSlot), Is.EqualTo(Game.Core.Party.Wire.PartyKeysMessage.MaxKeys));
            Assert.That(member.Client.PartyKeyCount(leaderSlot), Is.EqualTo(1));
            Assert.That(leader.Client.PartyKeyCount(leaderSlot), Is.EqualTo(1));
            var best = leader.Client.PartyKey(memberSlot, 0);
            Assert.That(PartyDungeonId.Tier(best), Is.EqualTo(3), "the best keys come first and Rift keys stay private");
            Assert.That(PartyDungeonId.Owner(best), Is.EqualTo(memberSlot));
            Assert.That(PartyDungeonId.Stage(member.Client.PartyKey(leaderSlot, 0)), Is.EqualTo(200));
        }

        [Test]
        public void AMemberInTheCampOffersNoKeysAndStaysOutOfThePartyRun()
        {
            var rig = new Rig(10, 400, 400);
            var leader = rig.Members[0];
            var member = rig.Members[1];
            leader.Loop.State.Dungeons.Keys.Add(KeyAt(1, 300));
            member.Loop.State.Dungeons.Keys.Add(KeyAt(2, 300));
            rig.Run(8);
            Assert.That(leader.Client.PartyKeyCount(member.Client.OwnSlot), Is.EqualTo(1));

            Assert.That(member.Loop.Peaceful.SetMode(Game.Core.Content.PeacefulMode.Camp), Is.EqualTo(PeacefulResult.Done));
            rig.Run(8);
            Assert.That(leader.Client.PartyKeyCount(member.Client.OwnSlot), Is.Zero);

            Assert.That(leader.Client.ProposeDungeon(leader.Client.PartyKey(leader.Client.OwnSlot, 0)), Is.True);
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Dungeon, 30), Is.True);
            rig.Run(1);
            Assert.That(leader.Loop.Dungeons.InParty, Is.True);
            Assert.That(member.Loop.Dungeons.Inside, Is.False);

            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Home, 600), Is.True);
            rig.Run(3);
            Assert.That(leader.Loop.State.Dungeons.Completed, Is.EqualTo(1));
            Assert.That(member.Loop.State.Dungeons.Completed, Is.Zero);
            Assert.That(member.DungeonDrops, Is.Zero);
            Assert.That(member.Loop.Dungeons.Keys.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheLeaderLaunchesAnyPartyKeyAndEveryoneGetsTheLoot()
        {
            var rig = new Rig(10, 400, 400);
            var leader = rig.Members[0];
            var member = rig.Members[1];
            var key = KeyAt(2, 300, "thick_hide");
            member.Loop.State.Dungeons.Keys.Add(key);
            rig.Run(8);
            var id = leader.Client.PartyKey(member.Client.OwnSlot, 0);
            Assert.That(id, Is.GreaterThan(0));

            Assert.That(member.Client.ProposeDungeon(id), Is.False, "only the leader launches");
            Assert.That(leader.Loop.Dungeons.Enter(key), Is.Not.EqualTo(DungeonResult.Entered));
            Assert.That(leader.Client.ProposeDungeon(id + 1), Is.False, "nobody holds such a key");
            Assert.That(leader.Client.ProposeDungeon(id), Is.True);
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Dungeon, 30), Is.True);
            rig.Run(1);

            Assert.That(leader.Loop.Dungeons.InParty, Is.True);
            Assert.That(member.Loop.Dungeons.InParty, Is.True);
            Assert.That(leader.Loop.State.Dungeons.Borrowed, Is.True);
            Assert.That(member.Loop.State.Dungeons.Borrowed, Is.False);
            Assert.That(member.Loop.State.Dungeons.Active, Is.SameAs(key));
            Assert.That(member.Loop.Dungeons.Keys, Is.Empty);
            Assert.That(leader.Loop.State.Stage, Is.EqualTo(301));
            Assert.That(leader.Loop.Stages.RaisesRecord, Is.False);
            Assert.That(leader.Client.DungeonId, Is.EqualTo(id));

            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Home, 600), Is.True);
            rig.Run(3);

            foreach (var hero in rig.Members)
            {
                Assert.That(hero.Loop.Dungeons.Inside, Is.False);
                Assert.That(hero.Loop.State.Dungeons.Completed, Is.EqualTo(1));
                Assert.That(hero.Loop.State.Dungeons.Failed, Is.Zero);
                Assert.That(hero.DungeonDrops, Is.GreaterThanOrEqualTo(Rules.TierAt(2)!.Loot.Items));
                Assert.That(hero.Loop.State.Stage, Is.GreaterThanOrEqualTo(400));
                Assert.That(hero.Loop.Stages.RaisesRecord, Is.True);
            }
            Assert.That(member.Loop.Dungeons.Keys, Is.Empty, "the key is spent");
            Assert.That(leader.Loop.Dungeons.Keys, Is.Empty);
        }

        [Test]
        public void AFailedPartyRunCostsTheOwnerOneAttemptAndNobodyElseAnything()
        {
            var rig = new Rig(60, 400, 400);
            var leader = rig.Members[0];
            var member = rig.Members[1];
            var key = KeyAt(2, 395, "tough_boss", "hurry");
            leader.Loop.State.Dungeons.Keys.Add(key);
            rig.Run(8);
            var id = leader.Client.PartyKey(leader.Client.OwnSlot, 0);

            Assert.That(leader.Client.ProposeDungeon(id), Is.True);
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Dungeon, 30), Is.True);
            rig.Run(1);
            Assert.That(member.Loop.State.Dungeons.Borrowed, Is.True);
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Home, 3000), Is.True);
            rig.Run(3);

            Assert.That(leader.Loop.Dungeons.Inside, Is.False);
            Assert.That(member.Loop.Dungeons.Inside, Is.False);
            Assert.That(leader.Loop.State.Dungeons.Failed, Is.EqualTo(1));
            Assert.That(member.Loop.State.Dungeons.Failed, Is.Zero);
            Assert.That(leader.Loop.State.Dungeons.Completed + member.Loop.State.Dungeons.Completed, Is.Zero);
            Assert.That(leader.Loop.Dungeons.Keys, Is.EquivalentTo(new[] { key }));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts - 1));
            Assert.That(member.Loop.Dungeons.Keys, Is.Empty);
        }

        [Test]
        public void TheLeaderCanCallThePartyHomeFromADungeon()
        {
            var rig = new Rig(4000, 400, 400);
            var leader = rig.Members[0];
            var member = rig.Members[1];
            var key = KeyAt(2, 395);
            member.Loop.State.Dungeons.Keys.Add(key);
            rig.Run(8);
            leader.Client.ProposeDungeon(leader.Client.PartyKey(member.Client.OwnSlot, 0));
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Dungeon, 30), Is.True);
            rig.Run(1);
            Assert.That(member.Loop.Dungeons.InParty, Is.True);

            Assert.That(leader.Client.ProposeWorld(PartyWorld.Home, 0), Is.True);
            Assert.That(rig.RunUntil(() => rig.Host.Encounter.World == PartyWorld.Home, 30), Is.True);
            rig.Run(3);

            Assert.That(member.Loop.Dungeons.Inside, Is.False);
            Assert.That(leader.Loop.Dungeons.Inside, Is.False);
            Assert.That(member.Loop.Dungeons.Keys, Is.EquivalentTo(new[] { key }));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts - 1));
            Assert.That(member.Loop.State.Stage, Is.GreaterThanOrEqualTo(400));
        }

        [Test]
        public void AMemberWhoDropsOutOfAPartyRunGoesHomeAndTheOwnerKeepsTheKey()
        {
            var loop = TestWorld.Loop(state: new GameState { Stage = 400, HighestStage = 400, RecordStage = 400 });
            var key = KeyAt(2, 300);
            loop.State.Dungeons.Keys.Add(key);
            loop.Stages.Link();
            Assert.That(loop.Dungeons.EnterParty(loop.Dungeons.PartyId(key, 0), owned: true), Is.EqualTo(DungeonResult.Entered));
            Assert.That(loop.Dungeons.Keys, Is.Empty);

            loop.Stages.Unlink();
            loop.Tick();

            Assert.That(loop.Dungeons.Inside, Is.False);
            Assert.That(loop.State.Stage, Is.EqualTo(400));
            Assert.That(loop.Dungeons.Keys, Is.EquivalentTo(new[] { key }));
            Assert.That(key.Attempts, Is.EqualTo(Rules.KeyAttempts));

            var guest = TestWorld.Loop(state: new GameState { Stage = 100, HighestStage = 100, RecordStage = 100 });
            guest.Stages.Link();
            guest.Dungeons.EnterParty(loop.Dungeons.PartyId(key, 0), owned: false);
            guest.Stages.Unlink();
            guest.Tick();
            Assert.That(guest.Dungeons.Inside, Is.False);
            Assert.That(guest.Dungeons.Keys, Is.Empty, "a borrowed key never lands in the bag");
        }
    }
}
