using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class UndertakerRiseTests
    {
        private static MinionRules Rules => TestWorld.Data.Combat.Skeletons;

        private static GameLoop Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e9));
            loop.Tick();
            return loop;
        }

        private static void Kill(GameLoop loop, int count)
        {
            loop.State.Kills += count;
            loop.Tick();
        }

        [Test]
        public void EveryFifthKillRisesASkeletonForTwentySeconds()
        {
            var loop = Hero(HeroClass.Gravedigger);

            Kill(loop, Rules.RiseEvery - 1);
            Assert.That(loop.Skills.Minions.StandingRisen(), Is.Zero);

            Kill(loop, 1);
            Assert.That(loop.Skills.Minions.StandingRisen(), Is.EqualTo(1));
            Assert.That(Rules.RiseEvery, Is.EqualTo(5));
            Assert.That(Rules.Rise!.Seconds, Is.EqualTo(20));

            for (var i = 0; i < Rules.Rise.Seconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
            Assert.That(loop.Skills.Minions.StandingRisen(), Is.Zero);
        }

        [Test]
        public void NoMoreThanThreeRiseOnTheirOwn()
        {
            var loop = Hero(HeroClass.Gravedigger);

            Kill(loop, Rules.RiseEvery * 5);

            Assert.That(Rules.RiseMax, Is.EqualTo(3));
            Assert.That(loop.Skills.Minions.StandingRisen(), Is.EqualTo(Rules.RiseMax));
        }

        [Test]
        public void OnlyTheUndertakerRaisesTheKilled()
        {
            var loop = Hero(HeroClass.Ronin);

            Kill(loop, Rules.RiseEvery * 2);

            Assert.That(loop.Skills.Minions.StandingRisen(), Is.Zero);
            Assert.That(loop.Skills.Minions.Standing(Game.Core.Content.Gems.MinionKind.Skeleton), Is.Zero);
        }

        [Test]
        public void TreeLinesRaiseTheKilledMoreOften()
        {
            var loop = Hero(HeroClass.Gravedigger, new Modifier(CombatStats.RiseEvery, ModifierKind.Flat, -1, StatTags.Minion));

            Kill(loop, Rules.RiseEvery - 1);

            Assert.That(loop.Skills.Minions.StandingRisen(), Is.EqualTo(1));
        }

        [Test]
        public void TheCountTowardTheNextRiseIsSaved()
        {
            var loop = Hero(HeroClass.Gravedigger);
            Kill(loop, 3);

            var loaded = Game.Core.Save.SaveCodec.Decode(Game.Core.Save.SaveCodec.Encode(loop.State, Game.Core.Clock.ClockStamp.First(new Game.Core.Clock.ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.KillsTowardRise, Is.EqualTo(3));
        }
    }
}
