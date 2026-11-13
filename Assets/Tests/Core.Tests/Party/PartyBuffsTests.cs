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
                Loop = TestWorld.Loop(Input, TestWorld.WithAllSkillsOpen(new GameState { Stage = 5, HighestStage = 5, RecordStage = 5 }));
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
            }

            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public double Damage => Loop.Hero.Value(CombatStats.Damage, 1);

            public int Equip(string gem)
            {
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
        public void TheLoopRepeatsTheAlliesDamageAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var clock = rig.Members[0];
            clock.Loop.State.Class = HeroClass.Clockwright;
            clock.Loop.Hero.Add(new Modifier(ClockwrightStats.LoopPower, ModifierKind.Flat, 1));
            var repeated = rig.Host.RepeatedShares;

            rig.Run(TestWorld.Data.Combat.Classes.Clockwright.LoopEverySeconds + 2);

            Assert.That(rig.Host.RepeatedShares, Is.GreaterThan(repeated), "the host repeated the party's recent damage");
        }

        [Test]
        public void AlliesSeeEachOthersSkillSlots()
        {
            var rig = new Rig(2);
            var caster = rig.Members[0];
            var friend = rig.Members[1];
            while (caster.Loop.State.Skills.Count > 0)
                caster.Loop.Skills.Unequip(0);
            caster.Equip("runedancer_hit_1");
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
        public void AlliesSeeEachOthersLook()
        {
            var rig = new Rig(2);
            var dresser = rig.Members[0];
            var friend = rig.Members[1];
            rig.Run(5);
            var dresserSlot = friend.Client.Roster.SlotOf(10UL);
            Assert.That(friend.Client.AllyLook(dresserSlot), Is.EqualTo(default(HeroLook)));

            dresser.Loop.Cosmetics.Equip("cloak_moss");
            dresser.Loop.Cosmetics.Grant("strike_koi");
            dresser.Loop.Cosmetics.Equip("strike_koi");
            rig.Run(2);

            Assert.That(friend.Client.AllyLook(dresserSlot), Is.EqualTo(new HeroLook(4, 0, 7)));
            Assert.That(dresser.Client.AllyLook(dresserSlot), Is.EqualTo(default(HeroLook)), "the own slot is not an ally");
            var allies = new Game.Core.Ui.StripAlly[Game.Core.Ui.StripAllies.MaxShown];
            Assert.That(Game.Core.Ui.StripAllies.Fill(friend.Client, allies), Is.EqualTo(1));
            Assert.That(allies[0].Look.Strike, Is.EqualTo(7));
        }

        [Test]
        public void AlliesSeeEachOthersSkillCasts()
        {
            var rig = new Rig(2);
            var caster = rig.Members[0];
            var friend = rig.Members[1];
            while (caster.Loop.State.Skills.Count > 0)
                caster.Loop.Skills.Unequip(0);
            caster.Equip("runedancer_hit_1");
            rig.Run(5);
            var casterSlot = friend.Client.Roster.SlotOf(10UL);
            var left = caster.Loop.State.Skills[SkillLoadout.IndexOf(caster.Loop.State.Skills, SkillSlotRole.Left)].Skill;

            caster.Loop.RequestCast(SkillLoadout.IndexOf(caster.Loop.State.Skills, SkillSlotRole.Left));
            rig.Run(3);

            Assert.That(friend.Client.TakeAllyCast(out var slot, out var gem), Is.True);
            Assert.That(slot, Is.EqualTo(casterSlot));
            Assert.That(gem, Is.SameAs(left));
            Assert.That(friend.Client.TakeAllyCast(out _, out _), Is.False);
            Assert.That(caster.Client.TakeAllyCast(out _, out _), Is.False, "the own cast is not an ally cast");
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
        public void EveryMemberGetsTheRoleBuffsOfTheOthers()
        {
            var rig = new Rig(2);

            rig.Run(3);

            var synergy = TestWorld.Data.Synergy;
            Assert.That(rig.Members[0].Loop.Synergy.AllyRoles, Is.EqualTo(synergy.RoleMaskOf(rig.Members[1].Loop.State.Class)));
            Assert.That(rig.Members[1].Loop.Synergy.AllyRoles, Is.EqualTo(synergy.RoleMaskOf(rig.Members[0].Loop.State.Class)));
        }

        [Test]
        public void AHeroInTheCampStaysInThePartyAsPeaceful()
        {
            var rig = new Rig(2);
            rig.Run(3);
            rig.Members[1].Loop.Peaceful.SetMode(Game.Core.Content.PeacefulMode.Camp);
            rig.Run(7);

            var slot = rig.Host.Encounter.SlotOf(11UL);
            Assert.That(rig.Host.Encounter.Member(slot).Peaceful, Is.True);
            Assert.That(rig.Host.Encounter.Fighters, Is.EqualTo(1), "a hero in the camp does not make the enemy bigger");
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
        public void APartyContributionStatCreditsTheBoosterWithPartOfTheBoostedDamage()
        {
            var rig = new Rig(2);
            var bannerman = rig.Members[0];
            bannerman.Loop.Hero.Add(new Modifier(CombatStats.PartyContribution, ModifierKind.Flat, 0.05));
            rig.Run(5);
            Assert.That(rig.Host.Encounter.Boosting(bannerman.Client.OwnSlot), Is.False, "no aura yet");
            bannerman.Equip("lanternbearer_aura_1");
            rig.Run(3);
            Assert.That(rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditRate, Is.EqualTo(0.05).Within(1e-6));
            Assert.That(rig.Host.Encounter.Boosting(bannerman.Client.OwnSlot), Is.True);

            var before = rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditedShare;
            var share = rig.Host.Encounter.BarLeft / 2;
            rig.Host.Encounter.ApplyDamage(rig.Members[1].Client.OwnSlot, rig.Host.Encounter.EnemyId, share);

            Assert.That(rig.Host.Encounter.Member(bannerman.Client.OwnSlot).CreditedShare - before, Is.EqualTo(share * 0.05).Within(1e-9));
        }

        [Test]
        public void ASandwalkerStopsThePartyBossTimerAndGivesAlliesSand()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var walker = rig.Members[0];
            var friend = rig.Members[1];
            walker.Loop.State.Class = HeroClass.Sandwalker;
            var running = rig.Host.Encounter.BossTimerRate;

            walker.Loop.State.Sandwalker.PartyStopTicks = 50;
            rig.Run(1);
            Assert.That(rig.Host.Encounter.BossTimerRate, Is.EqualTo(TestWorld.Data.Party.Timer.MinBossRate).Within(1e-9));
            Assert.That(rig.Host.Encounter.BossTimerRate, Is.LessThan(running));
            rig.Run(6);
            Assert.That(rig.Host.Encounter.BossTimerRate, Is.EqualTo(running).Within(1e-9));

            friend.Loop.State.Resource = 0;
            rig.Run(1);
            var plain = friend.Loop.State.Resource;
            friend.Loop.State.Resource = 0;
            walker.Loop.State.TraitPartyResourceGift = 10;
            rig.Run(1);

            Assert.That(walker.Loop.State.TraitPartyResourceGift, Is.Zero);
            Assert.That(friend.Loop.State.Resource - plain, Is.EqualTo(10).Within(0.5));
        }

        [Test]
        public void AWeakerSandwalkerSlowReplacesTheStopInsteadOfHoldingIt()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var walker = rig.Members[0];
            walker.Loop.State.Class = HeroClass.Sandwalker;

            walker.Loop.State.Sandwalker.PartyStopTicks = 10;
            walker.Loop.State.Sandwalker.PartyTimeTicks = 60;
            rig.Run(0.5);
            Assert.That(TraitTimerSlow(rig), Is.EqualTo(1).Within(1e-9));

            rig.Run(1.5);
            Assert.That(TraitTimerSlow(rig), Is.EqualTo(TestWorld.Data.Combat.Classes.Sandwalker.PartyTimeSlow).Within(1e-9));

            rig.Run(4.5);
            Assert.That(TraitTimerSlow(rig), Is.Zero);
        }

        private static double TraitTimerSlow(Rig rig)
        {
            for (var i = 0; i < PartyEncounter.MaxEffects; i++)
            {
                var effect = rig.Host.Encounter.Effect(i);
                if (effect.Active && effect.Id == PartyClient.TraitTimerEffect)
                    return effect.TimerSlow;
            }
            return 0;
        }

        [Test]
        public void AMinstrelSongAndChorusGiftReachTheAllyWithoutNewMessages()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var minstrel = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            minstrel.Loop.State.Class = HeroClass.Minstrel;
            rig.Run(2);
            var heard = friend.Damage;
            minstrel.Loop.Hero.Add(new Modifier(MinstrelStats.SongPower, ModifierKind.Flat, 0.15));
            minstrel.Loop.Hero.Add(CoopStatNames.Line("{ 'to': 'allies', 'stat': 'attackPower', 'kind': 'increased', 'value': 0.001, 'per': { 'signal': 'songShare', 'step': 0.001, 'cap': 5000 } }"));
            minstrel.Loop.RebuildTree();

            rig.Run(2);
            Assert.That(friend.Damage - heard, Is.EqualTo(before * 0.15).Within(1e-6));

            friend.Loop.State.Resource = 0;
            rig.Run(1);
            var plain = friend.Loop.State.Resource;
            friend.Loop.State.Resource = 0;
            minstrel.Loop.State.TraitPartyResourceGift = 10;
            rig.Run(1);
            Assert.That(friend.Loop.State.Resource - plain, Is.EqualTo(10).Within(0.5));
        }

        [Test]
        public void TreePartyDamageReachesTheAlly()
        {
            var rig = new Rig(2);
            rig.Members[0].Loop.State.Class = HeroClass.Minstrel;
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.3));
            rig.Members[0].Loop.RebuildTree();

            rig.Run(2);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.3).Within(1e-6));

            rig.Members[0].Client.Leave();
            rig.Run(7);
            var synergy = TestWorld.Data.Synergy;
            var heraldBuff = 1.0;
            foreach (var modifier in synergy.RoleBuffs[System.Linq.Enumerable.ToList(synergy.Roles).IndexOf("herald")])
            {
                if (modifier.Stat == CombatStats.Damage && modifier.Kind == ModifierKind.More)
                    heraldBuff *= 1 + modifier.Value;
            }
            Assert.That(friend.Damage, Is.EqualTo(before / heraldBuff).Within(1e-6), "the party damage and the herald's role buff leave with the ally");
        }

        [Test]
        public void CohesionStrengthensWhatTheGiverSends()
        {
            var rig = new Rig(2);
            rig.Members[0].Loop.State.Class = HeroClass.Minstrel;
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.3));
            rig.Members[0].Loop.Hero.Add(new Modifier(CombatStats.Cohesion, ModifierKind.Flat, 0.5));
            rig.Members[0].Loop.RebuildTree();

            rig.Run(2);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.45).Within(1e-6));
        }

        [Test]
        public void ReceptivityStrengthensWhatTheAllyGets()
        {
            var rig = new Rig(2);
            rig.Members[0].Loop.State.Class = HeroClass.Minstrel;
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            friend.Loop.Hero.Add(new Modifier(CombatStats.Receptivity, ModifierKind.Flat, 0.5));
            rig.Members[0].Loop.Hero.Add(new Modifier(new StatId("partyDamage"), ModifierKind.Increased, 0.3));
            rig.Members[0].Loop.RebuildTree();

            rig.Run(2);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.45).Within(1e-6));
        }

        [Test]
        public void FractureOnTheSharedEnemyRaisesTheDamageOfTheWholeParty()
        {
            var rig = new Rig(2);
            rig.Run(3);
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1));
            rig.Members[0].Loop.Hero.Add(new Modifier(CombatStats.FractureChance, ModifierKind.Flat, 1));

            rig.Run(3);
            Assert.That(rig.Members[0].Loop.State.FractureStacks, Is.GreaterThan(0));
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.GreaterThan(1), "the breaker's stacks count for every hit on the shared enemy");
        }

        [Test]
        public void AReservedAuraBuffsTheAllyUntilItIsTakenOff()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var minstrel = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            var slot = minstrel.Equip("lanternbearer_aura_1");

            minstrel.Cast(slot);
            rig.Run(3);
            Assert.That(friend.Damage, Is.EqualTo(before * 1.15).Within(1e-6));

            minstrel.Loop.Skills.Unequip(slot);
            rig.Run(7);
            Assert.That(friend.Damage, Is.EqualTo(before).Within(1e-6));
        }

        [Test]
        public void TheSameAuraFromAnAllyDoesNotStackWithYourOwn()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var friend = rig.Members[1];
            var before = friend.Damage;
            var metalSlot = rig.Members[0].Equip("lanternbearer_aura_1");
            var friendSlot = friend.Equip("lanternbearer_aura_1");

            rig.Members[0].Cast(metalSlot);
            friend.Cast(friendSlot);
            rig.Run(3);

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

            weak.Cast(weak.Equip("lanternbearer_aura_1"));
            strong.Cast(strong.Equip("lanternbearer_aura_1"));
            rig.Run(3);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.30).Within(1e-6));
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
        public void AWeakerOwnAuraLeavesRoomForTheStrongerAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var strong = rig.Members[0];
            var friend = rig.Members[1];
            var before = friend.Damage;
            strong.Loop.Hero.Add(new Modifier(CombatStats.AuraEffect, ModifierKind.Increased, 1, StatTags.Aura));

            strong.Cast(strong.Equip("lanternbearer_aura_1"));
            friend.Cast(friend.Equip("lanternbearer_aura_1"));
            rig.Run(3);

            Assert.That(friend.Damage, Is.EqualTo(before * 1.30).Within(1e-6), "the stronger song wins even over your own weaker one");
        }

        [Test]
        public void CursedVulnerabilityHangsOnThePartyEnemyAtTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var caster = rig.Members[0];
            var skill = caster.Equip("runedancer_hit_2");
            caster.Loop.State.HeroLevel = 100;
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
