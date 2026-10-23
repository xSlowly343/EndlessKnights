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
    }
}
