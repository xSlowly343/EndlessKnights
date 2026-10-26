using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyHostTests
    {
        private const ulong HostId = 1;

        private static (PartyHost Host, PartyBot[] Bots, LoopbackNetwork Network) Party(double loss, params double[] sharesPerTick)
        {
            var network = new LoopbackNetwork(2024, 50, 500, loss, 4096);
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 99);
            var host = new PartyHost(encounter, network.Connect(HostId), TestWorld.Data.Party);
            var bots = new PartyBot[sharesPerTick.Length];
            for (var i = 0; i < bots.Length; i++)
                bots[i] = new PartyBot(network.Connect(10UL + (ulong)i), HostId, sharesPerTick[i]);
            return (host, bots, network);
        }

        private static void Run(PartyHost host, PartyBot[] bots, LoopbackNetwork network, int ticks, bool fighting)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                network.Advance(100);
                foreach (var bot in bots)
                    bot.Tick(fighting);
                host.Tick();
            }
        }

        [Test]
        public void FourBotsWithFivePercentLossConverge()
        {
            var (host, bots, network) = Party(0.05, 0.03, 0.02, 0.04, 0.01);

            Run(host, bots, network, 1200, true);
            Run(host, bots, network, 60, false);

            var encounter = host.Encounter;
            Assert.That(network.Dropped, Is.GreaterThan(0));
            Assert.That(encounter.MemberCount, Is.EqualTo(4));
            Assert.That(host.Kills, Is.GreaterThan(10));
            var sent = 0.0;
            foreach (var bot in bots)
            {
                Assert.That(bot.Joined, Is.True);
                Assert.That(bot.State.EnemyId, Is.EqualTo(encounter.EnemyId));
                Assert.That(bot.State.BarLeft, Is.EqualTo(encounter.BarLeft));
                Assert.That(bot.State.BarFull, Is.EqualTo(encounter.BarFull));
                Assert.That(bot.DuplicateKills, Is.EqualTo(0));
                Assert.That(bot.Kills.Count, Is.LessThanOrEqualTo(host.Kills));
                sent += bot.SentShares;
            }
            Assert.That(host.AcceptedShares, Is.GreaterThan(0.8 * sent));
            Assert.That(host.AcceptedShares, Is.LessThanOrEqualTo(sent * encounter.DamageTakenMultiplier + 1e-9));
        }

        [Test]
        public void SilentBotIsMarkedOfflineAndComesBack()
        {
            var (host, bots, network) = Party(0, 0.02, 0.02);
            Run(host, bots, network, 50, true);
            var silenceTicks = (int)System.Math.Round(TestWorld.Data.Party.SilenceSeconds * 10);

            network.SetLinkDown(11, true);
            Run(host, bots, network, silenceTicks + 10, true);
            Assert.That(host.Encounter.Presence(host.Encounter.SlotOf(11)), Is.EqualTo(PartyPresence.Offline));

            network.SetLinkDown(11, false);
            Run(host, bots, network, 60, true);
            Assert.That(host.Encounter.Presence(host.Encounter.SlotOf(11)), Is.EqualTo(PartyPresence.Fighting));
        }

        [Test]
        public void RepeatedBatchesDoNotCountTwice()
        {
            var (host, bots, network) = Party(0, 0.0);
            Run(host, bots, network, 30, true);
            var transport = network.Connect(10);
            var buffer = new byte[Game.Core.Party.Wire.PartyCodec.MaxPacketBytes];
            var enemy = host.Encounter.EnemyId;
            var batch = new Game.Core.Party.Wire.DamageBatchMessage { Sequence = 1000, EnemyId = enemy, Keys = 0.3, Hits = 1 };
            var length = Game.Core.Party.Wire.PartyCodec.Encode(buffer, batch);

            transport.Send(HostId, buffer, length);
            transport.Send(HostId, buffer, length);
            network.Advance(600);
            host.Tick();

            Assert.That(host.Encounter.BarLeft, Is.EqualTo(host.Encounter.BarFull - 0.3).Within(1e-9));
        }
    }
}
