using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SynergyMechanicsTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        private static GameLoop Hero(HeroClass heroClass)
        {
            var loop = TestWorld.Loop();
            loop.State.Class = heroClass;
            return loop;
        }

        private static GameLoop Poisoned(HeroClass heroClass, double stacks)
        {
            var loop = Hero(heroClass);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.State.PoisonStacks = stacks;
            return loop;
        }

        [Test]
        public void MiasmaReactionBurstsTheOwnStacksOnceAndThenWaitsForItsCooldown()
        {
            var loop = Poisoned(HeroClass.Ronin, 20);
            loop.Synergy.SetAlly(0, true, HeroClass.Moonshiner, Mark("fire"));

            loop.Tick();
            Assert.That(loop.Skills.Dots.MiasmaBursts, Is.EqualTo(1));

            for (var tick = 0; tick < 95; tick++)
                loop.Tick();
            Assert.That(loop.Skills.Dots.MiasmaBursts, Is.EqualTo(1), "the reaction burns one second and then cools down");
            for (var tick = 0; tick < 10; tick++)
                loop.Tick();
            Assert.That(loop.Skills.Dots.MiasmaBursts, Is.EqualTo(2));
            Assert.That(loop.State.PoisonStacks, Is.EqualTo(20).Within(1e-9), "the stacks stay");
        }

        [Test]
        public void MiasmaBurstDealsTheShareOfTheStacksDamageOverItsSeconds()
        {
            var with = Poisoned(HeroClass.Ronin, 20);
            var without = Poisoned(HeroClass.Ronin, 20);
            with.Synergy.SetAlly(0, true, HeroClass.Moonshiner, Mark("fire"));
            without.Synergy.SetAlly(0, true, HeroClass.Viking, Mark("stun"));

            with.Tick();
            without.Tick();

            var dots = TestWorld.Data.Combat.Dots;
            var share = Rules.Reactions[IndexOfReaction("miasma_burst")].Modifiers[0].Value;
            var hits = 20 * dots.PoisonHitsPerStack * dots.MiasmaStackSeconds * share;
            var oneHit = with.Combat.SkillDamage(with.Combat.BaseKeyDamage, 1, Game.Core.Stats.StatTags.Skill | Game.Core.Stats.StatTags.DoT);
            var extra = (with.State.TotalDamage - without.State.TotalDamage).ToDouble();
            Assert.That(extra, Is.GreaterThanOrEqualTo(hits * oneHit.ToDouble() * 0.99), "crits only add");
            Assert.That(extra, Is.LessThan(hits * oneHit.ToDouble() * 10));
        }

        [Test]
        public void PlagueDoctorBurstsEveryFewSecondsWhileAnAllyBurns()
        {
            var loop = Poisoned(HeroClass.PlagueDoctor, 20);
            loop.Synergy.SetAlly(0, true, HeroClass.Captain, Mark("fire"));
            var every = TestWorld.Data.Combat.Dots.MiasmaEverySeconds;

            for (var tick = 0; tick < 3 * every * GameLoop.TicksPerSecond; tick++)
                loop.Tick();

            Assert.That(loop.Skills.Dots.MiasmaBursts, Is.EqualTo(3), "the response holds and the reaction shares its rhythm");
        }

        [Test]
        public void NoStacksNoBurst()
        {
            var loop = Hero(HeroClass.PlagueDoctor);
            loop.Synergy.SetAlly(0, true, HeroClass.Moonshiner, Mark("fire"));

            for (var tick = 0; tick < 20; tick++)
                loop.Tick();

            Assert.That(loop.Skills.Dots.MiasmaBursts, Is.Zero);
        }

        private static int IndexOfReaction(string id)
        {
            for (var i = 0; i < Rules.Reactions.Count; i++)
            {
                if (Rules.Reactions[i].Id == id)
                    return i;
            }
            return -1;
        }
    }
}
