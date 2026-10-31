using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SeerClearDayTests
    {
        private static PartyBuffRules Rules => TestWorld.Data.Party.Buffs;

        private static int CritIndex()
        {
            for (var i = 0; i < Rules.Stats.Count; i++)
            {
                if (Rules.Stats[i].From == CombatStats.PartyCritChance)
                    return i;
            }
            throw new AssertionException("no party crit link");
        }

        private static GameLoop Hero(HeroClass heroClass, double resource, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero);
            loop.State.Resource = resource;
            loop.Tick();
            loop.State.Resource = resource;
            loop.Tick();
            return loop;
        }

        [Test]
        public void AboveFiftyVisionsThePartyGetsFivePercentCritChance()
        {
            var loop = Hero(HeroClass.Oracle, 60);

            Assert.That(Rules.ClearDayAbove, Is.EqualTo(50));
            Assert.That(loop.PartyBuffs.ClearDay, Is.True);
            Assert.That(loop.PartyBuffs.OwnValue(CritIndex()), Is.EqualTo(0.05).Within(1e-9), "allies get it too");
            Assert.That(loop.PartyBuffs.AppliedValue(CritIndex()), Is.EqualTo(0.05).Within(1e-9), "the seer is in the party as well");
        }

        [Test]
        public void TheClearDayEndsWhenVisionsFall()
        {
            var loop = Hero(HeroClass.Oracle, 60);

            loop.State.Resource = 30;
            loop.Tick();
            loop.State.Resource = 30;
            loop.Tick();

            Assert.That(loop.PartyBuffs.ClearDay, Is.False);
            Assert.That(loop.PartyBuffs.AppliedValue(CritIndex()), Is.Zero);
        }

        [Test]
        public void OnlyTheSeerBringsTheClearDay()
        {
            var loop = Hero(HeroClass.Ronin, 60);

            Assert.That(loop.PartyBuffs.ClearDay, Is.False);
        }

        [Test]
        public void TreeLinesStartItEarlierAndMakeItStronger()
        {
            var loop = Hero(HeroClass.Oracle, 47,
                new Modifier(CombatStats.ClearDayAbove, ModifierKind.Flat, -5),
                new Modifier(CombatStats.ClearDayCritChance, ModifierKind.Flat, 0.01));

            Assert.That(loop.PartyBuffs.ClearDay, Is.True);
            Assert.That(loop.PartyBuffs.OwnValue(CritIndex()), Is.EqualTo(0.06).Within(1e-9));
        }
    }
}
