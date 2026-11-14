using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyTrialWorldTests
    {
        private const ulong HostId = 1;

        private static PartyTrialRules Trials => TestWorld.Data.Party.Growth.Trials;
        private static PartyTrialDef Trial(string id) => Trials.Find(id)!;
        private static int SwitchTicks => (int)System.Math.Round(TestWorld.Data.Party.WorldSwitchSeconds * GameLoop.TicksPerSecond);

        private static PartyEncounter Party(int players, bool withTrials = true)
        {
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 42);
            if (withTrials)
            {
                encounter.Trials = Trials;
                encounter.Defense = TestWorld.Data.Combat.Defense;
            }
            for (var i = 0; i < players; i++)
                encounter.Join(100UL + (ulong)i);
            return encounter;
        }

        private static void EnterTrial(PartyEncounter encounter, string id, int tier, bool night = false)
        {
            Assert.That(encounter.ProposeWorld(PartyWorld.Trial, PartyTrialId.Pack(Trial(id).Index, tier, night)), Is.True);
            for (var i = 0; i < SwitchTicks; i++)
                encounter.Advance();
            Assert.That(encounter.World, Is.EqualTo(PartyWorld.Trial));
            Assert.That(encounter.InTrial, Is.True);
        }

        private static bool Ended(PartyEncounter encounter)
        {
            for (var i = 0; i < encounter.Events.Count; i++)
            {
                if (encounter.Events[i].Kind == PartyEventKind.TrialEnded)
                    return true;
            }
            return false;
        }

        [Test]
        public void TheTrialIdKeepsTrialTierAndNight()
        {
            var id = PartyTrialId.Pack(7, 35, true);
            Assert.That(PartyTrialId.Trial(id), Is.EqualTo(7));
            Assert.That(PartyTrialId.Tier(id), Is.EqualTo(35));
            Assert.That(PartyTrialId.Night(id), Is.True);
            Assert.That(PartyTrialId.Night(PartyTrialId.Pack(0, 1, false)), Is.False);
            Assert.That(PartyTrialId.Valid(0), Is.False);
            Assert.That(PartyTrialId.Valid(PartyTrialId.Pack(0, 1, false)), Is.True);
        }

        [Test]
        public void ATrialNeedsItsRulesAKnownTrialAndTheHomeWorld()
        {
            Assert.That(Party(2, false).ProposeWorld(PartyWorld.Trial, PartyTrialId.Pack(0, 1, false)), Is.False, "no trial rules on this host");
            var encounter = Party(2);
            Assert.That(encounter.ProposeWorld(PartyWorld.Trial, PartyTrialId.Pack(Trials.List.Count, 1, false)), Is.False, "there is no such trial");
            EnterTrial(encounter, "choir", 3);
            Assert.That(encounter.ProposeWorld(PartyWorld.Dungeon, 5), Is.False, "from a trial the party only goes home");
            Assert.That(encounter.TrialFight.Tier, Is.EqualTo(3));
            Assert.That(encounter.TrialFight.Fighters, Is.EqualTo(2));
            Assert.That(encounter.BarFull, Is.EqualTo(encounter.TrialFight.HpFull));
        }

        [Test]
        public void DamageByKindsClearsTheTrialAndTheBandGoesHome()
        {
            var encounter = Party(2);
            EnterTrial(encounter, "choir", 1);
            encounter.Events.Clear();
            var ticks = 0;
            while (!Ended(encounter) && ticks++ < 3000)
            {
                encounter.ReportInput(0);
                encounter.ReportInput(1);
                Assert.That(encounter.AddTrialDamage(0, 0.002, 0.001, 0, 0), Is.EqualTo(0.003).Within(1e-12));
                encounter.AddTrialDamage(1, 0, 0, 0.002, 0.001);
                encounter.Advance();
            }
            Assert.That(encounter.TrialFight.Outcome, Is.EqualTo(PartyTrialOutcome.Cleared));
            Assert.That(encounter.TrialFight.ResultTier, Is.EqualTo(1));
            Assert.That(encounter.World, Is.EqualTo(PartyWorld.Home));
            Assert.That(encounter.InTrial, Is.False);
            Assert.That(PartyTrialId.Trial(encounter.TrialId), Is.EqualTo(Trial("choir").Index), "the last trial is remembered for the result");
        }

        [Test]
        public void AQuietFighterStopsTheChoirAndOrdinaryDamageMissesTheTrial()
        {
            var encounter = Party(2);
            EnterTrial(encounter, "choir", 1);
            encounter.ReportInput(0);
            var restTicks = (int)System.Math.Round(TestWorld.Data.Party.Contribution.ActiveInputSeconds * GameLoop.TicksPerSecond) + 1;
            for (var i = 0; i < restTicks; i++)
            {
                encounter.ReportInput(0);
                encounter.Advance();
            }
            var before = encounter.TrialFight.HpLeft;
            encounter.ReportInput(0);
            encounter.AddTrialDamage(0, 0.1, 0, 0, 0);
            Assert.That(encounter.ApplyDamage(0, encounter.EnemyId, 1), Is.EqualTo(0), "the stage enemy is not in a trial");
            Assert.That(encounter.RepeatRecent(0, 5), Is.EqualTo(0));
            encounter.Advance();
            Assert.That(encounter.TrialFight.HpLeft, Is.EqualTo(before), "the second fighter is not typing, so the choir is silent");
        }

        [Test]
        public void GoingHomeEarlyFailsTheTrial()
        {
            var encounter = Party(2);
            EnterTrial(encounter, "relay", 2);
            encounter.Events.Clear();
            Assert.That(encounter.ProposeWorld(PartyWorld.Home, 0), Is.True);
            for (var i = 0; i < SwitchTicks; i++)
                encounter.Advance();
            Assert.That(Ended(encounter), Is.True);
            Assert.That(encounter.TrialFight.Outcome, Is.EqualTo(PartyTrialOutcome.Failed));
            Assert.That(encounter.TrialFight.ResultTier, Is.EqualTo(0));
            Assert.That(encounter.World, Is.EqualTo(PartyWorld.Home));
        }

        [Test]
        public void TheRelayHolderIsASlot()
        {
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 42) { Trials = Trials, Defense = TestWorld.Data.Combat.Defense };
            encounter.Join(100);
            encounter.Join(101);
            encounter.Join(102);
            encounter.Leave(1);
            EnterTrial(encounter, "relay", 1);
            Assert.That(encounter.TrialHolderSlot, Is.EqualTo(0));
            var pass = (int)System.Math.Round(Trial("relay").PassSeconds * GameLoop.TicksPerSecond);
            for (var i = 0; i <= pass; i++)
                encounter.Advance();
            Assert.That(encounter.TrialHolderSlot, Is.EqualTo(2), "the wand skips the empty slot");
        }

        [Test]
        public void TheTrialStateCrossesTheWire()
        {
            var encounter = Party(2);
            EnterTrial(encounter, "shields", 4, true);
            encounter.ReportInput(0);
            encounter.ReportInput(1);
            encounter.AddTrialDamage(0, 0.05, 0, 0, 0);
            encounter.Advance();
            var sent = TrialStateMessage.Of(encounter);
            var buffer = new byte[PartyCodec.MaxPacketBytes];
            var length = PartyCodec.Encode(buffer, sent);
            Assert.That(PartyCodec.KindOf(buffer, length), Is.EqualTo(PartyMessageKind.TrialState));
            Assert.That(PartyCodec.TryDecode(buffer, length, out TrialStateMessage got), Is.True);
            Assert.That(got.TrialId, Is.EqualTo(PartyTrialId.Pack(Trial("shields").Index, 4, true)));
            Assert.That(got.Outcome, Is.EqualTo(PartyTrialOutcome.Running));
            Assert.That(got.Shield(PartyTrialDamage.Keys), Is.EqualTo((float)encounter.TrialFight.ShieldLeft(PartyTrialDamage.Keys)));
            Assert.That(got.Shield(PartyTrialDamage.Mouse), Is.EqualTo((float)Trial("shields").ShieldShare));
            Assert.That(got.HpLeft, Is.EqualTo(encounter.TrialFight.HpLeft));
            Assert.That(got.Holder, Is.EqualTo(TrialStateMessage.Nobody));
            Assert.That(got.TicksLeft, Is.EqualTo(encounter.TrialFight.TotalTicks - encounter.TrialFight.Tick));
        }

        private static (PartyHost Host, PartyBot[] Bots, LoopbackNetwork Network) Hosted(params double[] sharesPerTick)
        {
            var network = new LoopbackNetwork(7, 50, 50, 0, 4096);
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 99) { Trials = Trials, Defense = TestWorld.Data.Combat.Defense };
            var host = new PartyHost(encounter, network.Connect(HostId), TestWorld.Data.Party);
            var bots = new PartyBot[sharesPerTick.Length];
            for (var i = 0; i < bots.Length; i++)
                bots[i] = new PartyBot(network.Connect(10UL + (ulong)i), HostId, sharesPerTick[i], (byte)HeroClass.Runedancer);
            return (host, bots, network);
        }

        private static void Run(PartyHost host, PartyBot[] bots, LoopbackNetwork network, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                network.Advance(100);
                foreach (var bot in bots)
                    bot.Tick(true);
                host.Tick();
            }
        }

        [Test]
        public void TheHostFightsTheTrialFromDamageBatches()
        {
            var (host, bots, network) = Hosted(0.0015, 0.0005);
            Run(host, bots, network, 40);
            Assert.That(host.StartTrial(Trial("choir"), 1, false), Is.True);
            Run(host, bots, network, SwitchTicks + 2);
            Assert.That(host.Encounter.InTrial, Is.True);
            var ticks = 0;
            while (host.TrialsEnded == 0 && ticks++ < 5000)
                Run(host, bots, network, 1);
            Assert.That(host.Encounter.TrialFight.Outcome, Is.EqualTo(PartyTrialOutcome.Cleared));
            Assert.That(host.Encounter.World, Is.EqualTo(PartyWorld.Home));
            Assert.That(host.CutShares, Is.EqualTo(0), "honest batches are not cut");
            Assert.That(host.Flags(0) + host.Flags(1), Is.EqualTo(0));
        }

        [Test]
        public void TheHostDoesNotLetTheBandClearFasterThanTheMinimum()
        {
            var (host, bots, network) = Hosted(5, 5);
            Run(host, bots, network, 40);
            host.StartTrial(Trial("choir"), 1, false);
            Run(host, bots, network, SwitchTicks + 2);
            var minTicks = (int)(Trials.MinSeconds * GameLoop.TicksPerSecond);
            Run(host, bots, network, minTicks - 2 * (int)(TestWorld.Data.Party.Caps.HitBurstSeconds * GameLoop.TicksPerSecond));
            Assert.That(host.TrialsEnded, Is.EqualTo(0), "the cut holds the band to the minimum time");
            Assert.That(host.CutShares, Is.GreaterThan(0));
            Assert.That(host.Flags(0), Is.GreaterThan(0));
            Run(host, bots, network, minTicks);
            Assert.That(host.TrialsEnded, Is.EqualTo(1));
            Assert.That(host.Encounter.TrialFight.Outcome, Is.EqualTo(PartyTrialOutcome.Cleared));
        }
    }
}
