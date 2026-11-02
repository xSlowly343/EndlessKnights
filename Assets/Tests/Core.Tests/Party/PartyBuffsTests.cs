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
                Loop = TestWorld.Loop(Input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Stage = 5, HighestStage = 5, RecordStage = 5 }));
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
        public void ADreamcatcherNetHangsOnThePartyEnemyAndAnAllyCritHoldsIt()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var catcher = rig.Members[0];
            var friend = rig.Members[1];
            catcher.Loop.State.TraitEnemyEffects.Add(DreamcatcherMechanics.NetEffect, 0.5, 3);
            rig.Run(1);

            var effect = Enumerable.Range(0, PartyEncounter.MaxEffects).Select(i => rig.Host.Encounter.Effect(i)).Single(e => e.Active && e.Id == DreamcatcherMechanics.NetEffect);
            Assert.That(effect.DamageTaken, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(friend.Client.EnemyNetted, Is.True);

            var left = effect.TicksLeft;
            Assert.That(rig.Host.Encounter.ExtendEnemyEffect(DreamcatcherMechanics.NetEffect, TestWorld.Data.Combat.Classes.Dreamcatcher.NetPullSeconds), Is.True);
            var pulled = Enumerable.Range(0, PartyEncounter.MaxEffects).Select(i => rig.Host.Encounter.Effect(i)).Single(e => e.Active && e.Id == DreamcatcherMechanics.NetEffect);
            Assert.That(pulled.TicksLeft - left, Is.EqualTo(TestWorld.Data.Combat.Classes.Dreamcatcher.NetPullSeconds * GameLoop.TicksPerSecond));
        }

        [Test]
        public void ADreamSpiritCalledByOneMemberComesOutTougherForTheWholeParty()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var catcher = rig.Members[0];
            catcher.Loop.State.TraitSpiritCalls = 1;
            catcher.Loop.State.TraitSpiritHp = 3;
            rig.Run(1);
            Assert.That(rig.Host.Encounter.SpiritCalls, Is.EqualTo(1));

            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 5);
            encounter.Join(1);
            encounter.Join(2);
            encounter.Advance();
            var plain = encounter.BarFull;
            Assert.That(encounter.CallSpirit(1, 3), Is.True);
            encounter.ApplyDamage(0, encounter.EnemyId, plain * 2);
            encounter.Advance();
            Assert.That(encounter.EnemySpirit, Is.True);
            Assert.That(encounter.BarFull, Is.EqualTo(TestWorld.Data.Party.HpMultiplier(encounter.Fighters) * 3).Within(1e-9));
            encounter.Events.Clear();
            encounter.ApplyDamage(1, encounter.EnemyId, encounter.BarFull * 2);
            encounter.Advance();
            var killed = Enumerable.Range(0, encounter.Events.Count).Select(i => encounter.Events[i]).First(e => e.Kind == PartyEventKind.EnemyKilled);
            Assert.That(killed.Spirit, Is.True);
            Assert.That(encounter.EnemySpirit, Is.False);
        }

        [Test]
        public void AGhostcaptainsBoatswainAndParrotReachTheAllyAndTheAllysCallComesBack()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var captain = rig.Members[0];
            var friend = rig.Members[1];
            captain.Loop.State.Class = HeroClass.Ghostcaptain;
            captain.Loop.Hero.Add(new Modifier(GhostcaptainStats.ParrotPower, ModifierKind.Flat, 0.5));
            captain.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Mate, To = LanternbearerMechanics.AnyAlly, Value = 0.3, Seconds = 120 });
            rig.Run(0.5);

            Assert.That(friend.Loop.State.Boons.MatePower, Is.EqualTo(0.3).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.MateTicks, Is.GreaterThan(0));
            Assert.That(friend.Loop.State.Boons.ParrotOwner, Is.EqualTo(captain.Client.OwnSlot));

            friend.Loop.State.Boons.ParrotCallOwed = true;
            rig.Run(0.5);
            Assert.That(captain.Loop.State.Ghostcaptain.PerchSlot, Is.EqualTo(friend.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.ParrotPower, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.ParrotTicks, Is.GreaterThan(0));
        }

        [Test]
        public void ASoulbindersSpiritReachesTheAllyAndTheAllysSoulComesBack()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var soulbinder = rig.Members[0];
            var friend = rig.Members[1];
            soulbinder.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Lend, To = LanternbearerMechanics.AnyAlly, Value = 0.25, Seconds = 10 });
            soulbinder.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Souls, To = TraitRelay.AllSlots, Count = 4 });
            soulbinder.Loop.State.TraitEnemyEffects.Add(SoulbinderMechanics.ColdEffect, 0.15, 1);
            rig.Run(0.5);

            Assert.That(friend.Loop.State.Boons.LendOwner, Is.EqualTo(soulbinder.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.LendPower, Is.EqualTo(0.25).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.SoulsShown, Is.EqualTo(4));

            soulbinder.Loop.State.Class = HeroClass.Soulbinder;
            soulbinder.Loop.Hero.Add(new Modifier(SoulbinderStats.LendPower, ModifierKind.Flat, 0.25));
            rig.Run(0.1);
            var back = soulbinder.Loop.State.Soulbinder.SoulsBack;
            friend.Loop.State.Boons.SoulsOwed = 2;
            friend.Loop.State.Boons.LendTicks = 100;
            rig.Run(0.3);
            Assert.That(soulbinder.Loop.State.Soulbinder.SoulsBack, Is.GreaterThanOrEqualTo(back + 2));
        }

        [Test]
        public void ADowsersVeinLuckAndCrewReachTheAllyAndTheAllysFindComesBack()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var dowser = rig.Members[0];
            var friend = rig.Members[1];
            dowser.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.VeinHint, To = TraitRelay.AllSlots, Value = 0.6, Seconds = 5 });
            dowser.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Luck, To = TraitRelay.AllSlots, Value = 0.3, Seconds = 30 });
            dowser.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Artel, To = TraitRelay.AllSlots, Every = 10, Value = 0.5, Extra = 0.1, Seconds = 30 });
            rig.Run(0.5);

            Assert.That(friend.Loop.State.Boons.VeinHint, Is.EqualTo(0.6).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.LuckOwner, Is.EqualTo(dowser.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.LuckChance, Is.EqualTo(0.3).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.ArtelOwner, Is.EqualTo(dowser.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.ArtelEvery, Is.EqualTo(10));

            dowser.Loop.State.Class = HeroClass.Dowser;
            dowser.Loop.Hero.Add(new Modifier(DowserStats.LuckPower, ModifierKind.Flat, 0.003));
            rig.Run(0.1);
            dowser.Loop.State.Dowser.Luck = 0.5;
            friend.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.LuckFound, To = (byte)dowser.Client.OwnSlot, Count = 1 });
            rig.Run(0.3);
            Assert.That(dowser.Loop.State.Dowser.Luck, Is.LessThan(0.5));
        }

        [Test]
        public void AnAlchemistsElixirAcidAndReagentCallReachTheAllyAndTheAllysReagentComesBack()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var alchemist = rig.Members[0];
            var friend = rig.Members[1];
            alchemist.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Elixir, To = LanternbearerMechanics.AnyAlly, Count = 1 | (5 << 2), Every = 5, Max = 3, Value = 3, Seconds = 8, Extra = 0.5 });
            alchemist.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Acid, To = LanternbearerMechanics.AnyAlly, Seconds = 10 });
            alchemist.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Reagents, To = TraitRelay.AllSlots, Seconds = 30 });
            rig.Run(0.5);

            Assert.That(friend.Loop.State.Boons.Elixirs, Is.EqualTo(1));
            Assert.That(friend.Loop.State.Boons.BlueTicks, Is.GreaterThan(0));
            Assert.That(friend.Loop.State.Boons.AcidOwner, Is.EqualTo(alchemist.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.ReagentOwner, Is.EqualTo(alchemist.Client.OwnSlot));

            alchemist.Loop.State.Class = HeroClass.Alchemist;
            alchemist.Loop.Hero.Add(new Modifier(AlchemistStats.MixPower, ModifierKind.Flat, 1));
            rig.Run(0.1);
            var before = alchemist.Loop.State.Alchemist.InFlask + 3 * alchemist.Loop.State.Alchemist.Mixes;
            friend.Loop.State.Boons.ReagentOwed = true;
            rig.Run(0.3);
            Assert.That(alchemist.Loop.State.Alchemist.InFlask + 3 * alchemist.Loop.State.Alchemist.Mixes, Is.GreaterThan(before));
        }

        [Test]
        public void AClockwrightsLoopRepeatsAllyDamageAtTheHostAndHerSpringAndGearsReachTheAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var clock = rig.Members[0];
            var friend = rig.Members[1];
            var repeated = rig.Host.RepeatedShares;
            clock.Loop.State.TraitLoopSeconds = 5;
            clock.Loop.State.TraitLoopShare = 0.5;
            clock.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Spring, To = TraitRelay.AllSlots, Value = 0.8, Seconds = 3, Extra = 0.3 });
            clock.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Gears, To = TraitRelay.AllSlots, Seconds = 30 });
            rig.Run(0.5);

            Assert.That(rig.Host.RepeatedShares, Is.GreaterThan(repeated));
            Assert.That(clock.Loop.State.TraitLoopSeconds, Is.Zero);
            Assert.That(friend.Loop.State.Boons.SpringWind, Is.EqualTo(0.8).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.GearOwner, Is.EqualTo(clock.Client.OwnSlot));

            var cuts = friend.Loop.State.Boons.GearCuts;
            clock.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.GearTick, To = TraitRelay.AllSlots, Seconds = 2 });
            rig.Run(0.3);
            Assert.That(friend.Loop.State.Boons.GearCuts, Is.EqualTo(cuts + 1));
        }

        [Test]
        public void AWhisperersKnotsReachTheHostAndHerWhisperAndNameCallReachTheAlly()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var whisperer = rig.Members[0];
            var friend = rig.Members[1];
            whisperer.Loop.State.TraitEnemyEffects.Add(WhispererMechanics.KnotEffect, 0.2, 2, 0.05);
            whisperer.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Hush, To = LanternbearerMechanics.AnyAlly, Count = 1, Max = 3, Value = 4 });
            whisperer.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.NameCall, To = TraitRelay.AllSlots, Every = 50, Seconds = 30 });
            rig.Run(0.5);

            var knots = Enumerable.Range(0, PartyEncounter.MaxEffects).Select(i => rig.Host.Encounter.Effect(i)).Single(e => e.Active && e.Id == WhispererMechanics.KnotEffect);
            Assert.That(knots.DamageTaken, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(knots.TimerSlow, Is.EqualTo(0.05).Within(1e-6));
            Assert.That(friend.Loop.State.Boons.Hushes + friend.Loop.State.Boons.HushHits, Is.EqualTo(1));
            Assert.That(friend.Loop.State.Boons.HushOwner, Is.EqualTo(whisperer.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.NameOwner, Is.EqualTo(whisperer.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.NameEvery, Is.EqualTo(50));

            whisperer.Loop.State.Class = HeroClass.Whisperer;
            whisperer.Loop.Hero.Add(new Modifier(WhispererStats.VowPower, ModifierKind.Flat, 3));
            rig.Run(0.1);
            friend.Loop.State.Boons.HushesOwed = 2;
            rig.Run(0.3);
            Assert.That(friend.Loop.State.Boons.HushesOwed, Is.Zero);
            Assert.That(whisperer.Loop.State.Whisperer.HushHitsBack, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ALanternbearerFireflyAndFlashReachTheAllyAndTheLightReachesTheHost()
        {
            var rig = new Rig(2);
            rig.Run(5);
            var lantern = rig.Members[0];
            var friend = rig.Members[1];
            lantern.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Firefly, To = LanternbearerMechanics.AnyAlly, Count = 2, Max = 10, Value = 2, Seconds = 20 });
            lantern.Loop.State.TraitRelays.Add(new TraitRelay { Kind = TraitRelayKind.Beacon, To = TraitRelay.AllSlots, Count = 1, Value = 0.5 });
            lantern.Loop.State.TraitLight = 0.4;
            var hostLight = 0.0;
            var seenLight = 0.0;
            for (var i = 0; i < 20; i++)
            {
                rig.Run(0.1);
                hostLight = System.Math.Max(hostLight, rig.Host.Encounter.EnemyLight);
                seenLight = System.Math.Max(seenLight, friend.Client.EnemyLight);
            }

            Assert.That(friend.Loop.State.Boons.Fireflies, Is.EqualTo(2));
            Assert.That(friend.Loop.State.Boons.FireflyOwner, Is.EqualTo(lantern.Client.OwnSlot));
            Assert.That(friend.Loop.State.Boons.BeaconCharges, Is.EqualTo(1));
            Assert.That(friend.Loop.State.Boons.BeaconPower, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(lantern.Loop.State.Boons.Fireflies, Is.Zero);
            Assert.That(hostLight, Is.EqualTo(0.4).Within(1e-6));
            Assert.That(seenLight, Is.EqualTo(0.4).Within(1e-6));

            lantern.Loop.State.Class = HeroClass.Lanternbearer;
            lantern.Loop.Hero.Add(new Modifier(LanternbearerStats.SparkPower, ModifierKind.Flat, 2));
            var pending = lantern.Loop.State.PendingResource;
            friend.Loop.State.Boons.SparksOwed = 3;
            rig.Run(0.2);
            Assert.That(friend.Loop.State.Boons.SparksOwed, Is.Zero);
            Assert.That(lantern.Loop.State.PendingResource + lantern.Loop.State.Resource, Is.GreaterThan(pending));
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
            AddToWeapon(caster, "runedancer_hit_2");
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
