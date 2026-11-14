using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PartyGrowthModelTests
    {
        [Test]
        public void APartyOfFourOpensTheTreeAroundTwoMonthsAndGrowsItsPower()
        {
            var days = new System.Collections.Generic.List<PartyGrowthDay>(new PartyGrowthModel(TestWorld.Data, 4, 2, 5000).Run(90));
            Assert.That(days[6].PartyLevel, Is.InRange(5, 12), "the mascot comes in the first week");
            var opened = days.FindIndex(d => d.TreePoints > 0) + 1;
            Assert.That(opened, Is.InRange(50, 75), "the tree opens on party level 30, about seven weeks in");
            Assert.That(days[89].Damage, Is.GreaterThan(days[30].Damage));
            Assert.That(days[89].Damage, Is.LessThanOrEqualTo(TestWorld.Data.Party.Growth.DamageCap + 1e-9));
            Assert.That(days[89].ColossusTier, Is.GreaterThan(1));
        }
    }
}
