using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SkaldHeardByAllTests
    {
        private static PartyBuffRules Rules => TestWorld.Data.Party.Buffs;

        private static int DamageIndex()
        {
            for (var i = 0; i < Rules.Stats.Count; i++)
            {
                if (Rules.Stats[i].From == CombatStats.PartyDamage)
                    return i;
            }
            throw new AssertionException("no party damage link");
        }

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            return (loop, input);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < seconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
        }

        [Test]
        public void WhileTheSkaldTypesThePartyGetsFivePercentDamage()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);
            Type(loop, input, 5);

            Assert.That(loop.PartyBuffs.HeardByAll, Is.True);
            Assert.That(loop.PartyBuffs.OwnValue(DamageIndex()), Is.EqualTo(0.05).Within(1e-9), "allies get it");
            Assert.That(loop.PartyBuffs.AppliedValue(DamageIndex()), Is.EqualTo(0.05).Within(1e-9), "the skald too");
        }

        [Test]
        public void TheBonusFadesAfterASecondWithoutKeysUnlessTheTreeHoldsIt()
        {
            var (loop, input) = Hero(HeroClass.Metalhead);
            Type(loop, input, 5);
            Wait(loop, Rules.TypingSeconds + 0.3);
            Assert.That(loop.PartyBuffs.HeardByAll, Is.False);
            Assert.That(loop.PartyBuffs.AppliedValue(DamageIndex()), Is.Zero);

            var (held, heldInput) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.TypingHoldSeconds, ModifierKind.Flat, 2));
            Type(held, heldInput, 5);
            Wait(held, Rules.TypingSeconds + 1.5);
            Assert.That(held.PartyBuffs.HeardByAll, Is.True);
        }

        [Test]
        public void OnlyTheSkaldIsHeardByAll()
        {
            var (loop, input) = Hero(HeroClass.Ronin);
            Type(loop, input, 5);

            Assert.That(loop.PartyBuffs.HeardByAll, Is.False);
        }

        [Test]
        public void TheChoirDoublesTheBonusInAPartyAndHalvesItSolo()
        {
            var choir = new[]
            {
                new Modifier(CombatStats.TypingPartyBonus, ModifierKind.Flat, 0.01),
                new Modifier(CombatStats.ChoirPartyOnly, ModifierKind.Flat, 1),
                new Modifier(CombatStats.ChoirPower, ModifierKind.Flat, 1),
                new Modifier(CombatStats.ChoirSoloShare, ModifierKind.Flat, 0.5)
            };
            var (solo, soloInput) = Hero(HeroClass.Metalhead, choir);
            Type(solo, soloInput, 5);
            Assert.That(solo.PartyBuffs.OwnValue(DamageIndex()), Is.EqualTo(0.06).Within(1e-9));

            solo.State.PartyMembers = 2;
            Assert.That(solo.PartyBuffs.OwnValue(DamageIndex()), Is.EqualTo(0.12).Within(1e-9));
        }

        [Test]
        public void AtFullOverdriveSongsGetStrongerForEightSecondsOnce()
        {
            var (loop, _) = Hero(HeroClass.Metalhead);
            var plain = loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            loop.State.Resource = 1000;
            loop.Tick();
            Assert.That(loop.State.OverdriveTicks, Is.GreaterThan(0));
            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura) / plain, Is.EqualTo(1.5).Within(1e-9));

            for (var i = 0; i < 8 * GameLoop.TicksPerSecond + 1; i++)
            {
                loop.State.Resource = 1000;
                loop.Tick();
            }
            Assert.That(loop.State.OverdriveTicks, Is.Zero, "full Overdrive does not start it again until it falls");
            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura), Is.EqualTo(plain).Within(1e-9));
        }

        [Test]
        public void TreeLinesMakeTheOverdriveSongsLongerAndStronger()
        {
            var (loop, _) = Hero(HeroClass.Metalhead,
                new Modifier(CombatStats.OverdriveSeconds, ModifierKind.Flat, 2),
                new Modifier(CombatStats.OverdriveSongs, ModifierKind.Flat, 0.2));
            var plain = loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura);
            loop.State.Resource = 1000;
            loop.Tick();

            Assert.That(loop.State.OverdriveTicks, Is.EqualTo(10 * GameLoop.TicksPerSecond).Within(1));
            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura) / plain, Is.EqualTo(1.7).Within(1e-9));
        }
    }
}
