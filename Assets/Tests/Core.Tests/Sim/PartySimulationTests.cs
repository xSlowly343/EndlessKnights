using Game.Core.Sim;
using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PartySimulationTests
    {
        private static readonly BotProfile Typist = new BotProfile(6, 0, 0);

        [Test]
        public void FourDamageDealersKillAboutAsFastAsSolo()
        {
            var solo = new PartySimulation(TestWorld.Real, Typist, new[] { HeroClass.Viking }).Run(600);
            var party = new PartySimulation(TestWorld.Real, Typist, new[] { HeroClass.Viking, HeroClass.Ronin, HeroClass.Amazon, HeroClass.Stormweaver }).Run(600);

            Assert.That(party.SecondsPerKill, Is.EqualTo(solo.SecondsPerKill).Within(solo.SecondsPerKill * 0.2));
        }

        [Test]
        public void ATwinkThatStopsTypingIsPaidOnlyFromItsRestBankAndThenNoMore()
        {
            var party = new PartySimulation(TestWorld.Real, Typist, new[] { HeroClass.Viking, HeroClass.Ronin }, 5)
            {
                IdleMembers = 1,
                IdleAfterSeconds = 120,
                SampleSeconds = 60
            }.Run(600);

            var atIdle = party.Samples[1];
            var last = party.Samples[party.Samples.Count - 1];
            Assert.That(atIdle.RestBankSeconds[1], Is.GreaterThan(60), "two minutes of typing fill the rest bank");
            Assert.That(last.RestBankSeconds[1], Is.Zero, "the bank runs dry without typing");
            Assert.That(last.Rewards[1] - party.Samples[party.Samples.Count - 2].Rewards[1], Is.Zero, "then the twink gets nothing");
            Assert.That(last.Rewards[0] - party.Samples[party.Samples.Count - 2].Rewards[0], Is.GreaterThan(0), "the one who types still gets every kill");
            Assert.That(last.HostShares[1] - party.Samples[party.Samples.Count - 2].HostShares[1], Is.Zero, "a twink without input deals nothing");
        }

        [Test]
        public void EveryMemberGetsEveryPartyKillAndTheSameStages()
        {
            var party = new PartySimulation(TestWorld.Real, Typist, new[] { HeroClass.Viking, HeroClass.Metalhead }, 3, 0.05).Run(600);

            Assert.That(party.HostKills, Is.GreaterThan(50));
            foreach (var member in party.Members)
            {
                Assert.That(member.ShareOfKills, Is.GreaterThan(0.97));
                Assert.That(member.FinalStage, Is.EqualTo(party.Members[0].FinalStage));
            }
        }

        [Test]
        public void KillsFasterThanTheStatesAreAllRewardedInOrder([Values(0.0, 0.2)] double loss, [Values(1UL, 2UL, 3UL)] ulong seed)
        {
            var party = new PartySimulation(TestWorld.Real, Typist, new[] { HeroClass.Viking, HeroClass.Ronin, HeroClass.Metalhead, HeroClass.Voodoo }, seed, loss, 1e9)
            {
                LossAfterJoin = true
            }.Run(300);

            Assert.That(party.SecondsPerKill, Is.LessThan(0.5), "enemies die faster than the host sends its state");
            foreach (var member in party.Members)
            {
                Assert.That(member.MissedKills, Is.Zero);
                Assert.That(member.ShareOfKills, Is.GreaterThan(0.99));
                Assert.That(member.FinalStage, Is.EqualTo(party.Members[0].FinalStage).Within(1));
            }
        }
    }
}
