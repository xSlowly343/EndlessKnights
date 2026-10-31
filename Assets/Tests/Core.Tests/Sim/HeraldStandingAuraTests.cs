using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HeraldStandingAuraTests
    {
        private static StandingAuraRules Rules => TestWorld.Data.Combat.Classes.StandingAuras;

        private static SkillTestHero Herald(params Modifier[] modifiers)
        {
            var herald = SkillTestHero.Of(HeroClass.Bannerbearer, "artel_pay", "storm_over_the_banner", "nobody_retreats");
            foreach (var modifier in modifiers)
                herald.Loop.Hero.Add(modifier);
            herald.Ticks(1);
            return herald;
        }

        private static void Pause(SkillTestHero hero, double seconds) => hero.Ticks((int)(seconds * GameLoop.TicksPerSecond) + 1);

        [Test]
        public void TwoStandingAurasStayOnInALongPauseAndTheThirdGoesOff()
        {
            Assert.That(Rules.Count, Is.EqualTo(2));
            var herald = Herald();
            Assert.That(herald.State.Skills[0].AuraApplied && herald.State.Skills[1].AuraApplied && herald.State.Skills[2].AuraApplied, Is.True);

            Pause(herald, 15);

            Assert.That(herald.State.Skills[0].AuraApplied, Is.True);
            Assert.That(herald.State.Skills[1].AuraApplied, Is.True);
            Assert.That(herald.State.Skills[2].AuraApplied, Is.False);
        }

        [Test]
        public void TheThirdAuraFromTheTreeStandsTooAndCanReserveNothing()
        {
            var herald = Herald(new Modifier(CombatStats.ThirdAura, ModifierKind.Flat, 1));
            Assert.That(herald.Loop.Skills.StandingAuras, Is.EqualTo(3));
            Pause(herald, 15);
            Assert.That(herald.State.Skills[2].AuraApplied, Is.True);

            var free = Herald(new Modifier(CombatStats.ThirdAura, ModifierKind.Flat, 1), new Modifier(CombatStats.ThirdAuraFree, ModifierKind.Flat, 1));
            free.Loop.Skills.Unequip(2);
            free.Loop.Skills.Equip(TestWorld.Data.Gems["nobody_retreats"]);
            free.Ticks(1);

            Assert.That(free.State.Skills[2].AuraApplied, Is.True);
            Assert.That(free.State.Skills[2].ReservedApplied, Is.Zero);
            Assert.That(free.State.Skills[0].ReservedApplied, Is.GreaterThan(0));
        }

        [Test]
        public void AurasGrowInAPauseWithTheTree()
        {
            var herald = Herald(new Modifier(CombatStats.AuraInPause, ModifierKind.Flat, 0.2));
            herald.Ticks(1, keysPerTick: 1);
            var typing = herald.Loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);

            Pause(herald, Rules.PauseSeconds);

            Assert.That(herald.Loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura) - typing, Is.EqualTo(0.2).Within(1e-9));
        }

        [Test]
        public void OtherClassesKeepTheirOwnAurasInPausesAsBefore()
        {
            var ronin = SkillTestHero.Borrowing(HeroClass.Ronin, HeroClass.Bannerbearer, "artel_pay");
            ronin.Ticks(1);
            Assert.That(ronin.Loop.Skills.StandingAuras, Is.Zero);

            Pause(ronin, 15);

            Assert.That(ronin.State.Skills[0].AuraApplied, Is.False);
        }
    }
}
