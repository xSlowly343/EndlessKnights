using System.Collections.Generic;
using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Content.Weapons;
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
                Loop.Skills.Equip(TestWorld.Data.Gems[gem], role: SkillTestHero.ManualRole(Loop.State.Skills.Count));
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
        public void AlliesSeeEachOthersSkillSlots()
        {
            var rig = new Rig(2);
            var caster = rig.Members[0];
            var friend = rig.Members[1];
            while (caster.Loop.State.Skills.Count > 0)
                caster.Loop.Skills.Unequip(0);
            caster.Equip("iron_rim_bash");
            rig.Run(5);
            var casterSlot = friend.Client.Roster.SlotOf(10UL);
            var left = caster.Loop.State.Skills[SkillLoadout.IndexOf(caster.Loop.State.Skills, SkillSlotRole.Left)].Skill;

            Assert.That(friend.Client.AllyLoadout(casterSlot).Left.GemNetId, Is.EqualTo(left.NetId));
            Assert.That(friend.Client.AllyLoadout(casterSlot).Left.Level, Is.EqualTo(caster.Loop.State.LevelOf(left)));
            Assert.That(caster.Client.AllyLoadout(caster.Client.Roster.SlotOf(10UL)).Left.Empty, Is.True, "the own slot is not an ally");

            while (caster.Loop.State.Skills.Count > 0)
                caster.Loop.Skills.Unequip(0);
            rig.Run(2);

            Assert.That(friend.Client.AllyLoadout(casterSlot).Left.Empty, Is.True, "a changed loadout reaches the ally");
        }

        [Test]
        public void MembersSeeTheHordeAndTheColossusShieldOfTheHost()
        {
            var rift = TestWorld.Data.Rift;
            var start = new System.DateTimeOffset(2026, 10, 27, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
            long riftId = 0;
            for (var week = 0; week < 400 && riftId == 0; week++)
            {
                foreach (var offset in new[] { 0L, 4L })
                {
                    var window = rift.Schedule.WindowAt(start + (week * 7 + offset) * 86_400_000L + 3_600_000);
                    if (window == null)
                        continue;
                    var ids = rift.ModifiersFor(window.Value.Id).Select(modifier => modifier.Id).ToList();
                    if (ids.Contains("horde") && ids.Contains("colossus_shield"))
                        riftId = window.Value.Id + 60;
                }
            }
            Assert.That(riftId, Is.Not.Zero, "a window with a horde and a colossus shield");
            var network = new LoopbackNetwork(5, 20, 60, 0, 4096);
            var host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 3), network.Connect(HostId), TestWorld.Data.Party, rift, TestWorld.Data.Synergy) { ClockUnixMs = riftId * 1000 };
            var members = new[] { new Member(network, 10), new Member(network, 11) };
            foreach (var member in members)
                member.Client.Join();
            void Run(double seconds)
            {
                for (var tick = 0; tick < seconds * GameLoop.TicksPerSecond; tick++)
                {
                    network.Advance(100);
                    foreach (var member in members)
                    {
                        member.Input.PressKeys(1);
                        member.Loop.Tick();
                        member.Loop.Events.Clear();
                    }
                    host.Tick();
                }
            }
            Run(2);
            host.Encounter.SwitchWorld(PartyWorld.Rift, riftId);
            Run(2);

            var seen = members[1].Loop;
            Assert.That(host.Encounter.EnemiesOnStage, Is.GreaterThan(TestWorld.Data.Stages.EnemiesOnStage(host.Encounter.WaveStage)), "the horde brings more enemies");
            Assert.That(seen.Stages.EnemiesOnThisStage, Is.EqualTo(host.Encounter.EnemiesOnStage));

            for (var i = 0; i < 400 && !host.Encounter.FightingBoss; i++)
                Run(0.5);
            Run(1);
            Assert.That(host.Encounter.FightingBoss, Is.True);
            Assert.That(host.Encounter.ShieldFloor, Is.GreaterThan(0), "no lightning or hunter mark in this party, the boss keeps its shield");
            Assert.That(seen.Stages.ShieldShare, Is.EqualTo(host.Encounter.ShieldFloor / host.Encounter.BarFull).Within(0.02), "the member sees where the shield of the boss ends");
        }

        [Test]
        public void TheHostTellsEveryMemberTheMarksOfTheOthers()
        {
            var rig = new Rig(2);
            var summon = TestWorld.Data.Synergy.MaskOf(new[] { "summon" });
            rig.Members[0].Loop.State.Minions[0] = new MinionState { Kind = MinionKind.Skeleton, TicksLeft = 100_000 };

            rig.Run(3);

            Assert.That(rig.Members[0].Loop.Synergy.OwnMarks & summon, Is.EqualTo(summon));
            Assert.That(rig.Members[1].Loop.Synergy.AllyMarks & summon, Is.EqualTo(summon), "a skeleton is no aura and no effect on the enemy, the host passes the mark on");
            Assert.That(rig.Members[0].Loop.Synergy.AllyMarks & summon, Is.Zero, "nobody gets their own mark back");

            rig.Members[0].Loop.State.Minions[0] = default;
            rig.Run(3);

            Assert.That(rig.Members[1].Loop.Synergy.AllyMarks & summon, Is.Zero);
        }

        [Test]
        public void NeedleMakesTheEnemyTakeMoreDamageInSolo()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            TestWorld.ArmFor(loop, TestWorld.Data.Gems["charm_prick"]);
            loop.Skills.Equip(TestWorld.Data.Gems["charm_prick"]);
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
        public void CharmPrickBoostsTheWholePartyAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var voodoo = rig.Members[0];
            var slot = voodoo.Equip("charm_prick");
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
        public void TheSeersGemLuckReachesTheAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Loop.Hero.Value(CombatStats.GemDrop, 1);
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyGemDrop"), ModifierKind.Increased, 0.15));
            rig.Members[0].Loop.RebuildTree();

            rig.Run(2);

            Assert.That(friend.Loop.Hero.Value(CombatStats.GemDrop, 1), Is.EqualTo(before * 1.15).Within(1e-6));
            Assert.That(TestWorld.Data.Ascendancy.Subclasses.SelectMany(path => path.Nodes).Where(node => node.Id == "seer_6").Single().Locked, Is.False);
        }

        [Test]
        public void APartyContributionStatCreditsTheBoosterWithPartOfTheBoostedDamage()
        {
            var rig = new Rig(2);
            var bannerman = rig.Members[0];
            bannerman.Loop.Hero.Add(new Modifier(CombatStats.PartyContribution, ModifierKind.Flat, 0.05));
            var song = bannerman.Equip("hearth_song");
            rig.Run(5);
            Assert.That(rig.Host.Encounter.Boosting(bannerman.Client.OwnSlot), Is.False, "no aura yet");
            bannerman.Cast(song);
            rig.Run(1);
            Assert.That(rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditRate, Is.EqualTo(0.05).Within(1e-6));
            Assert.That(rig.Host.Encounter.Boosting(bannerman.Client.OwnSlot), Is.True);

            var before = rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditedShare;
            var share = rig.Host.Encounter.BarLeft / 2;
            rig.Host.Encounter.ApplyDamage(rig.Members[1].Client.OwnSlot, rig.Host.Encounter.EnemyId, share);

            Assert.That(rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditedShare - before, Is.EqualTo(share * 0.05).Within(1e-9));
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
            var slot = metalhead.Equip("hearth_song");

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
            var metalSlot = rig.Members[0].Equip("hearth_song");
            var friendSlot = friend.Equip("hearth_song");

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

            weak.Cast(weak.Equip("hearth_song"));
            strong.Cast(strong.Equip("hearth_song"));
            rig.Run(1);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.30).Within(1e-6));
        }

        [Test]
        public void StretchTheSecondSlowsTheTimerAtTheHost()
        {
            var rig = new Rig(2);
            var clockmaker = rig.Members[0];
            clockmaker.Loop.State.Class = HeroClass.Clockmaker;
            rig.Run(5);

            clockmaker.Cast(clockmaker.Equip("stretch_second"));
            rig.Run(1);

            var effect = rig.Host.Encounter.Effect(0);
            Assert.That(effect.Id, Is.EqualTo(TestWorld.Data.Gems["stretch_second"].NetId));
            Assert.That(effect.TimerSlow, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(effect.Owner, Is.EqualTo(clockmaker.Client.OwnSlot));
        }

        [Test]
        public void TheChimesRepeatThePartyDamageAtTheHostNotAtHome()
        {
            var rig = new Rig(2);
            var clockmaker = rig.Members[0];
            clockmaker.Loop.State.Class = HeroClass.Clockmaker;
            rig.Run(5);
            var slot = clockmaker.Equip("the_chimes_strike_again");
            var pendulum = clockmaker.Loop.State.Weapons.Left!;
            clockmaker.Loop.State.Weapons.Left = new WeaponItem(pendulum.Base, pendulum.Rarity, new List<string>(pendulum.Skills) { "the_chimes_strike_again" });
            var before = clockmaker.Loop.State.TotalDamage;
            var hits = clockmaker.Loop.State.DamageWindow.Last(before, 70);

            clockmaker.Cast(slot);
            clockmaker.Loop.Tick();

            Assert.That((clockmaker.Loop.State.TotalDamage - before).ToDouble(), Is.LessThan(hits.ToDouble() * 0.5), "the local fight does not repeat on its own");
            Assert.That(clockmaker.Loop.Skills.LastRepeatSeconds, Is.EqualTo(7));
            rig.Run(1);
            Assert.That(rig.Host.RepeatedShares, Is.GreaterThan(0));
        }

        [Test]
        public void AlliesSeeTheCurseIconAndEachOthersClass()
        {
            var rig = new Rig(2);
            rig.Members[0].Loop.State.Class = HeroClass.Voodoo;
            rig.Run(5);
            var voodoo = rig.Members[0];
            var friend = rig.Members[1].Client;

            voodoo.Cast(voodoo.Equip("charm_prick"));
            rig.Run(1);

            var icon = friend.EnemyEffect(0);
            Assert.That(icon.Id, Is.EqualTo(TestWorld.Data.Gems["charm_prick"].NetId));
            Assert.That(icon.TicksLeft, Is.InRange(80, 100));
            Assert.That(friend.EnemyEffect(1).Active, Is.False);
            Assert.That(friend.DamageTaken, Is.EqualTo(1.15).Within(1e-9));
            Assert.That(friend.Roster.HeroClass(voodoo.Client.OwnSlot), Is.EqualTo((byte)HeroClass.Voodoo));
        }

        [Test]
        public void VoodooCurseWakesTheVikingResponseOverTheNetworkAndTheEvilEyeWakesHalf()
        {
            var rig = new Rig(2);
            var voodoo = rig.Members[0];
            var viking = rig.Members[1];
            voodoo.Loop.State.Class = HeroClass.Voodoo;
            viking.Loop.State.Class = HeroClass.Viking;
            var rage = new StatId("resourceGain");
            rig.Run(5);
            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1.125).Within(1e-9));

            voodoo.Cast(voodoo.Equip("charm_prick"));
            rig.Run(1);

            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(voodoo.Loop.Hero.Value(new StatId("curseEffect"), 1), Is.EqualTo(1));
            rig.Run(11);
            Assert.That(viking.Loop.Hero.Value(rage, 1), Is.EqualTo(1.125).Within(1e-9));
        }

        private static bool HasDamage(Member member, double value)
        {
            foreach (var modifier in member.Loop.PartyBuffs.Modifiers)
            {
                if (modifier.Stat == CombatStats.Damage && modifier.Kind == ModifierKind.Increased && System.Math.Abs(modifier.Value - value) < 1e-6)
                    return true;
            }
            return false;
        }

        [Test]
        public void TheForemansHammerStacksReachTheParty()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var bannerbearer = rig.Members[0];
            var friend = rig.Members[1];
            var hammer = bannerbearer.Equip("foreman_hammer");

            bannerbearer.Cast(hammer);
            rig.Run(4.5);
            bannerbearer.Cast(hammer);
            rig.Run(1.5);

            Assert.That(bannerbearer.Loop.State.Skills[hammer].Stacks, Is.EqualTo(2));
            Assert.That(HasDamage(friend, 0.06), Is.True, "two stacks of +3% reach the ally");
            Assert.That(friend.Loop.PartyBuffs.Ally(bannerbearer.Client.OwnSlot).Auras[0].Gem!.Id, Is.EqualTo("foreman_hammer"));

            rig.Run(15);

            Assert.That(HasDamage(friend, 0.06), Is.False, "the stacks run out for the ally too");
        }

        [Test]
        public void TheStarChartWeakensTheNextEnemiesAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var oracle = rig.Members[0];
            oracle.Loop.State.Class = HeroClass.Oracle;
            var omens = oracle.Equip("three_omens");
            var orb = oracle.Loop.State.Weapons.Left!;
            oracle.Loop.State.Weapons.Left = new WeaponItem(orb.Base, orb.Rarity, new List<string>(orb.Skills) { "three_omens" });

            Assert.That(oracle.Loop.Skills.Armed(omens), Is.True);
            oracle.Cast(omens);
            rig.Run(0.5);

            Assert.That(rig.Host.Encounter.WeakenedSpawns, Is.InRange(1, 3));
            Assert.That(rig.Host.Encounter.WeakenShare, Is.EqualTo(0.1).Within(1e-9));
            var weakened = rig.Host.Encounter.WeakenedSpawns;
            var enemy = rig.Host.Encounter.EnemyId;
            while (rig.Host.Encounter.EnemyId == enemy)
                rig.Run(0.1);
            Assert.That(rig.Host.Encounter.WeakenedSpawns, Is.EqualTo(weakened - 1), "each new enemy of the party uses one");
        }

        private static void AddToWeapon(Member member, string gem)
        {
            member.Loop.State.HeroLevel = 100;
            var weapons = member.Loop.State.Weapons;
            var type = TestWorld.Data.Gems[gem].Skill!.Weapon;
            var right = weapons.Right != null && weapons.Right.Type.Id == type;
            var hand = right ? weapons.Right! : weapons.Left!;
            if (hand.Skills.Contains(gem))
                return;
            var armed = new WeaponItem(hand.Base, hand.Rarity, new List<string>(hand.Skills) { gem });
            if (right)
                weapons.Right = armed;
            else
                weapons.Left = armed;
        }

        [Test]
        public void TheWholeHallSingsForThePartyOnlyFromTheMetalhead()
        {
            var rig = new Rig(3);
            rig.Run(5);
            var metalhead = rig.Members[0];
            var viking = rig.Members[1];
            var friend = rig.Members[2];
            metalhead.Loop.State.Class = HeroClass.Metalhead;
            var song = metalhead.Equip("whole_hall");
            var borrowed = viking.Equip("whole_hall");
            AddToWeapon(metalhead, "whole_hall");
            AddToWeapon(viking, "whole_hall");

            metalhead.Cast(song);
            viking.Cast(borrowed);
            rig.Run(2);

            Assert.That(metalhead.Loop.State.Skills[song].Stacks, Is.GreaterThan(5));
            Assert.That(viking.Loop.State.Skills[borrowed].Stacks, Is.GreaterThan(5));
            var hall = TestWorld.Data.Gems["whole_hall"];
            Assert.That(friend.Loop.PartyBuffs.Ally(metalhead.Client.OwnSlot).Auras[0].Gem, Is.SameAs(hall), "the Metalhead shares the stacks");
            Assert.That(friend.Loop.PartyBuffs.Ally(viking.Client.OwnSlot).Auras[0].Gem, Is.Null, "a borrowed guitar keeps them to itself");
        }

        [Test]
        public void AWeakerOwnAuraLeavesRoomForTheStrongerAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var strong = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            strong.Loop.Hero.Add(new Modifier(CombatStats.AuraEffect, ModifierKind.Increased, 1, StatTags.Aura));

            strong.Cast(strong.Equip("hearth_song"));
            friend.Cast(friend.Equip("hearth_song"));
            rig.Run(1);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.30).Within(1e-6), "the stronger song wins even over your own weaker one");
        }

        [Test]
        public void ResonanceMakesTheSongStrongerOnAlliesAndLongerAfterItEnds()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var metalhead = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            var song = metalhead.Equip("hearth_song");
            metalhead.Loop.State.HeroLevel = TestWorld.Data.Gems["resonance"].UnlockLevel;
            Assert.That(metalhead.Loop.Book.SetSupport(song, 0, TestWorld.Data.Gems["resonance"]), Is.EqualTo(Game.Core.Build.BookResult.Done));
            var seconds = TestWorld.Data.Gems["hearth_song"].Skill!.DurationSeconds;

            metalhead.Cast(song);
            rig.Run(1);
            Assert.That(friend.Damage, Is.EqualTo(before * (1 + 0.15 * 1.2)).Within(1e-5));

            rig.Run(seconds + 1);
            Assert.That(metalhead.Loop.State.Skills[song].AuraApplied, Is.False);
            Assert.That(friend.Damage, Is.GreaterThan(before), "the song keeps ringing for the ally after it ends");

            rig.Run(6);
            Assert.That(friend.Damage, Is.EqualTo(before).Within(1e-6));
        }

        [Test]
        public void CursedVulnerabilityHangsOnThePartyEnemyAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var caster = rig.Members[0];
            var skill = caster.Equip("hatchet_toss");
            AddToWeapon(caster, "hatchet_toss");
            caster.Loop.State.OwnedGems.Add(TestWorld.Data.Gems["cursed"]);
            Assert.That(caster.Loop.Book.SetSupport(skill, 0, TestWorld.Data.Gems["cursed"]), Is.EqualTo(Game.Core.Build.BookResult.Done));

            caster.Cast(skill);
            rig.Run(0.5);

            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1.08).Within(1e-9), "everyone's hits count 8% more");
            Assert.That(caster.Loop.State.SharedEnemy, Is.True);
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
