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

        [Test]
        public void FireLegionLetsOwnMinionsIgniteTheEnemy()
        {
            var with = Summoned(HeroClass.Gravedigger);
            var without = Summoned(HeroClass.Gravedigger);
            with.Synergy.SetAlly(0, true, HeroClass.Moonshiner, Mark("fire"));

            for (var tick = 0; tick < 30; tick++)
            {
                with.Tick();
                without.Tick();
            }

            Assert.That(with.Synergy.ReactionActive(IndexOfReaction("fire_legion")), Is.True);
            Assert.That(with.Skills.Minions.SkeletonHits, Is.GreaterThan(0));
            Assert.That(with.State.BurnStacks, Is.GreaterThan(0));
            Assert.That(without.State.BurnStacks, Is.Zero);
        }

        [Test]
        public void RotOfAgesCarriesThePoisonToTheNextEnemy()
        {
            var with = Poisoned(HeroClass.Ronin, 20);
            var without = Poisoned(HeroClass.Ronin, 20);
            with.Synergy.SetAlly(0, true, HeroClass.Clockmaker, Mark("time"));
            without.Synergy.SetAlly(0, true, HeroClass.Viking, Mark("stun"));
            with.Tick();
            without.Tick();
            Assert.That(with.Synergy.ReactionActive(IndexOfReaction("rot_of_ages")), Is.True);

            KillTheEnemy(with);
            KillTheEnemy(without);

            Assert.That(with.State.PoisonStacks, Is.EqualTo(20).Within(1e-9));
            Assert.That(without.State.PoisonStacks, Is.Zero);
        }

        [Test]
        public void StormweaverLightningHitsWithTheAllyPoisonOnEveryLightningCast()
        {
            var with = Casting(HeroClass.Stormweaver, Mark("poison"));
            var without = Casting(HeroClass.Stormweaver, Mark("stun"));

            Assert.That(with.SynergyStrikes.ElectrolysisStrikes, Is.EqualTo(1));
            Assert.That(without.SynergyStrikes.ElectrolysisStrikes, Is.Zero);
            Assert.That(with.State.TotalDamage.ToDouble(), Is.GreaterThan(without.State.TotalDamage.ToDouble()));
        }

        [Test]
        public void MoonshinerSparkAddsBurnEverySecondOnlyToABurningEnemy()
        {
            var loop = Hero(HeroClass.Moonshiner);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.Synergy.SetAlly(0, true, HeroClass.Stormweaver, Mark("lightning"));
            for (var tick = 0; tick < 20; tick++)
                loop.Tick();
            Assert.That(loop.SynergyStrikes.Sparks, Is.Zero, "nothing burns yet");

            loop.State.BurnStacks = 5;
            for (var tick = 0; tick < 30; tick++)
                loop.Tick();
            Assert.That(loop.SynergyStrikes.Sparks, Is.EqualTo(3));
            Assert.That(loop.State.BurnStacks, Is.EqualTo(8).Within(1e-9));
        }

        [Test]
        public void OracleCritsUnderTheHunterMarkGiveOmens()
        {
            var with = Hero(HeroClass.Oracle);
            var without = Hero(HeroClass.Oracle);
            with.Synergy.SetAlly(0, true, HeroClass.Amazon, Mark("hunterMark"));
            with.Tick();
            without.Tick();

            with.State.Crits += 4;
            without.State.Crits += 4;
            with.Tick();
            without.Tick();

            Assert.That(with.SynergyStrikes.OmensFromCrits, Is.EqualTo(4).Within(1e-9));
            Assert.That(without.SynergyStrikes.OmensFromCrits, Is.Zero);
            Assert.That(with.State.Resource, Is.GreaterThan(without.State.Resource));
        }

        private static GameLoop Casting(HeroClass heroClass, int allyMarks)
        {
            var loop = Hero(heroClass);
            var gem = FirstLightningGem();
            TestWorld.ArmFor(loop, gem);
            loop.Skills.Equip(gem);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.Synergy.SetAlly(0, true, HeroClass.PlagueDoctor, allyMarks);
            loop.Tick();
            loop.State.Resource = 1000;
            loop.RequestCast(0);
            loop.Tick();
            Assert.That(loop.State.Skills[0].LastCastTick, Is.EqualTo(loop.State.Tick - 1), "the lightning skill was cast");
            return loop;
        }

        private static Game.Core.Content.Gems.GemDef FirstLightningGem()
        {
            foreach (var gem in TestWorld.Data.Gems.All)
            {
                if (gem.Skill != null && gem.Skill.Marks.Count > 0 && gem.Skill.Marks[0] == "lightning" && gem.Ascendancy == null)
                    return gem;
            }
            throw new System.InvalidOperationException("no lightning gem");
        }

        private static GameLoop Summoned(HeroClass heroClass)
        {
            var loop = Hero(heroClass);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.Skills.Minions.Place(loop.Combat.Paths.SkeletonOnKill, Game.Core.Stats.StatTags.Skill);
            return loop;
        }

        private static void KillTheEnemy(GameLoop loop)
        {
            var kills = loop.State.Kills;
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e-9);
            for (var tick = 0; tick < 20 && loop.State.Kills == kills; tick++)
                loop.Tick();
            Assert.That(loop.State.Kills, Is.GreaterThan(kills));
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
