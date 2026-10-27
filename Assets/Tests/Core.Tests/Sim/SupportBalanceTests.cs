using Game.Core.Sim.Headless;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SupportBalanceTests
    {
        private static readonly BotProfile Typist = new BotProfile(8, 0, 300);

        [Test]
        public void ExecutionerMakesItsHostSkillStronger()
        {
            var balance = new SupportBalance(TestWorld.Data);

            Assert.That(balance.Multiplier(TestWorld.Data.Gems["executioner"], Typist, 600), Is.GreaterThan(1.1));
        }

        [Test]
        public void ChainFitsTheColoredSupportBudget()
        {
            var balance = new SupportBalance(TestWorld.Data);

            Assert.That(balance.Multiplier(TestWorld.Data.Gems["chain"], Typist, 600), Is.InRange(1.15, 1.40));
        }

        [Test]
        public void SameSeedGivesTheSameDamage()
        {
            var balance = new SupportBalance(TestWorld.Data, 7);

            Assert.That(balance.SkillDamage(null, Typist, 120), Is.EqualTo(balance.SkillDamage(null, Typist, 120)));
        }
    }
}
