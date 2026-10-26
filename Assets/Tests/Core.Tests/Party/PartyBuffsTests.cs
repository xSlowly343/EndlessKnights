using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyBuffsTests
    {
        private const ulong HostId = 1;

        private sealed class Member
        {
            public Member(LoopbackNetwork network, ulong id)
            {
                Input = new FakeInputSource();
                Loop = TestWorld.Loop(Input, new GameState { Stage = 5, HighestStage = 5, RecordStage = 5 });
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
            }

            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public double Damage => Loop.Hero.Value(CombatStats.Damage, 1);

            public int Equip(string gem)
            {
                TestWorld.ArmFor(Loop, TestWorld.Data.Gems[gem]);
                Loop.Skills.Equip(TestWorld.Data.Gems[gem]);
                return Loop.State.Skills.Count - 1;
            }

            public void Cast(int slot)
            {
                Loop.State.Resource = 1000;
                Loop.RequestCast(slot);
            }
        }

        private sealed class Rig
        {
            public Rig(int members)
            {
                Network = new LoopbackNetwork(5, 20, 60, 0, 4096);
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 3), Network.Connect(HostId), TestWorld.Data.Party);
                Members = new Member[members];
                for (var i = 0; i < members; i++)
                {
                    Members[i] = new Member(Network, 10UL + (ulong)i);
                    Members[i].Client.Join();
                }
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
                    {
                        member.Input.PressKeys(1);
                        member.Loop.Tick();
                        member.Loop.Events.Clear();
                    }
                    Host.Tick();
                }
            }
        }

        [Test]
        public void NeedleMakesTheEnemyTakeMoreDamageInSolo()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            TestWorld.ArmFor(loop, TestWorld.Data.Gems["voodoo_needle"]);
            loop.Skills.Equip(TestWorld.Data.Gems["voodoo_needle"]);
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before * 1.15).Within(1e-9));

            for (var tick = 0; tick < 10 * GameLoop.TicksPerSecond; tick++)
                loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void OwnPartyStatsHelpTheHeroInSolo()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.2));

            loop.RebuildTree();

            Assert.That(loop.PartyBuffs.AppliedValue(0), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before * 1.2).Within(1e-9));
        }

        [Test]
        public void VoodooNeedleBoostsTheWholePartyAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var voodoo = rig.Members[0];
            var slot = voodoo.Equip("voodoo_needle");
            var before = voodoo.Damage;

            voodoo.Cast(slot);
            rig.Run(1);

            Assert.That(voodoo.Loop.State.Skills[slot].Active, Is.True);
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1.15).Within(1e-9));
            Assert.That(voodoo.Damage, Is.EqualTo(before).Within(1e-9));
            rig.Run(10);
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void TreePartyDamageReachesTheAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.3));
            rig.Members[0].Loop.RebuildTree();

            rig.Run(2);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.3).Within(1e-6));

            rig.Members[0].Client.Leave();
            rig.Run(7);
            Assert.That(friend.Damage, Is.EqualTo(before).Within(1e-6));
        }

        [Test]
        public void SongBuffsTheAllyUntilItEnds()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var metalhead = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            var slot = metalhead.Equip("song");

            metalhead.Cast(slot);
            rig.Run(1);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.15).Within(1e-6));
            Assert.That(friend.Loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(1.10).Within(1e-6));

            rig.Run(20);
            Assert.That(friend.Damage, Is.EqualTo(before).Within(1e-6));
        }

        [Test]
        public void TheSameAuraFromAnAllyDoesNotStackWithYourOwn()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            var metalSlot = rig.Members[0].Equip("song");
            var friendSlot = friend.Equip("song");

            rig.Members[0].Cast(metalSlot);
            friend.Cast(friendSlot);
            rig.Run(1);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.15).Within(1e-6));
        }

        [Test]
        public void TwoAlliesWithTheSameAuraGiveOnlyTheStrongest()
        {
            var rig = new Rig(3);
            rig.Run(5);
            var friend = rig.Members[2];
            var before = friend.Damage;
            var weak = rig.Members[0];
            var strong = rig.Members[1];
            strong.Loop.Hero.Add(new Modifier(CombatStats.AuraEffect, ModifierKind.Increased, 1, StatTags.Aura));

            weak.Cast(weak.Equip("song"));
            strong.Cast(strong.Equip("song"));
            rig.Run(1);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.30).Within(1e-6));
        }

        [Test]
        public void AlliesSeeTheCurseIconAndEachOthersClass()
        {
            var rig = new Rig(2);
            rig.Members[0].Loop.State.Class = HeroClass.Voodoo;
            rig.Run(5);
            var voodoo = rig.Members[0];
            var friend = rig.Members[1].Client;

            voodoo.Cast(voodoo.Equip("voodoo_needle"));
            rig.Run(1);

            var icon = friend.EnemyEffect(0);
            Assert.That(icon.Id, Is.EqualTo(TestWorld.Data.Gems["voodoo_needle"].NetId));
            Assert.That(icon.TicksLeft, Is.InRange(80, 100));
            Assert.That(friend.EnemyEffect(1).Active, Is.False);
            Assert.That(friend.DamageTaken, Is.EqualTo(1.15).Within(1e-9));
            Assert.That(friend.Roster.HeroClass(voodoo.Client.OwnSlot), Is.EqualTo((byte)HeroClass.Voodoo));
        }

        [Test]
        public void VoodooCurseWakesTheVikingResponseOverTheNetwork()
        {
            var rig = new Rig(2);
            var voodoo = rig.Members[0];
            var viking = rig.Members[1];
            voodoo.Loop.State.Class = HeroClass.Voodoo;
            viking.Loop.State.Class = HeroClass.Viking;
            var rage = new StatId("rageGain");
            rig.Run(5);
            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1));

            voodoo.Cast(voodoo.Equip("voodoo_needle"));
            rig.Run(1);

            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(voodoo.Loop.Hero.Value(new StatId("curseEffect"), 1), Is.EqualTo(1));
            rig.Run(11);
            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1));
        }

        [Test]
        public void OfferSurvivesTheWire()
        {
            var buffer = new byte[PartyCodec.MaxPacketBytes];
            var offer = new PartyOfferMessage { Slot = 2 };
            offer.SetStat(0, 0.25);
            offer.SetStat(5, 1.5);
            offer.SetAura(3, new OfferedAura { GemNetId = 77, Strength = 2, TicksLeft = 150 });

            var length = PartyCodec.Encode(buffer, offer);

            Assert.That(PartyCodec.KindOf(buffer, length), Is.EqualTo(PartyMessageKind.PartyOffer));
            Assert.That(PartyCodec.TryDecode(buffer, length, out PartyOfferMessage read), Is.True);
            Assert.That(read.Slot, Is.EqualTo(2));
            Assert.That(read.Stat(0), Is.EqualTo(0.25).Within(1e-6));
            Assert.That(read.Stat(5), Is.EqualTo(1.5).Within(1e-6));
            Assert.That(read.Aura(3).GemNetId, Is.EqualTo(77));
            Assert.That(read.Aura(3).TicksLeft, Is.EqualTo(150));
        }

        [Test]
        public void BuffedPartyTicksDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var rig = new Rig(2);
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.3));
            rig.Members[0].Loop.RebuildTree();
            rig.Run(30);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            rig.Run(30);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
