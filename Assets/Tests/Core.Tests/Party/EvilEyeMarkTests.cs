using System.Linq;
using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class EvilEyeMarkTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        [Test]
        public void TheEvilEyeSetsItsOwnMarkOnlyForTheWhisperer()
        {
            var whisperer = TestWorld.Loop();
            whisperer.State.Class = HeroClass.Whisperer;
            var other = TestWorld.Loop();
            other.State.Class = HeroClass.Runedancer;
            whisperer.Tick();
            whisperer.Tick();
            other.Tick();
            other.Tick();

            Assert.That(whisperer.Synergy.OwnMarks & Mark("evilEye"), Is.Not.Zero);
            Assert.That(whisperer.Synergy.OwnMarks & Mark("curse"), Is.Zero);
            Assert.That(other.Synergy.OwnMarks & Mark("evilEye"), Is.Zero);
        }

        [Test]
        public void AnAllysEvilEyeWakesCurseResponsesAtHalfButNotTreeLinesOrReactions()
        {
            var runedancer = TestWorld.Loop();
            runedancer.State.Class = HeroClass.Runedancer;
            runedancer.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, ModifierCondition.AllyCurse));
            runedancer.Synergy.SetAlly(1, true, HeroClass.Whisperer, Mark("evilEye"));
            runedancer.Tick();

            Assert.That(runedancer.Hero.Value(new StatId("resourceGain"), 1), Is.EqualTo(1.125).Within(1e-9));
            Assert.That(runedancer.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.AllyCurse), Is.Zero);
            var broken = Rules.Reactions.ToList().FindIndex(reaction => reaction.Id == "broken");
            runedancer.Synergy.SetAlly(2, true, HeroClass.Runedancer, Mark("stun"));
            runedancer.Tick();
            Assert.That(runedancer.Synergy.ReactionActive(broken), Is.False);
        }

        [Test]
        public void AFullCurseStillWakesTheWholeResponse()
        {
            var runedancer = TestWorld.Loop();
            runedancer.State.Class = HeroClass.Runedancer;
            runedancer.Synergy.SetAlly(1, true, HeroClass.Whisperer, Mark("evilEye") | Mark("curse"));
            runedancer.Tick();

            Assert.That(runedancer.Hero.Value(new StatId("resourceGain"), 1), Is.EqualTo(1.25).Within(1e-9));
        }
    }
}
