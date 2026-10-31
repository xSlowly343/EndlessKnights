using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WanderingBladeSeriesTests
    {
        private static ClassRules Rules => TestWorld.Data.Combat.Classes;

        private static (GameLoop Loop, FakeInputSource Input) Ronin(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Ronin }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static int KeysUntilStrike(GameLoop loop, FakeInputSource input, int limit)
        {
            for (var i = 1; i <= limit; i++)
            {
                loop.Events.Clear();
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Kind == GameEventKind.Damage && (loop.Events[e].Flags & HitFlags.ComboStrike) != 0)
                        return i;
                }
            }
            return -1;
        }

        [Test]
        public void TheStreakStrikeComesEveryFiftyHitsAndBreathEveryTwentyFive()
        {
            var (loop, input) = Ronin();

            Assert.That(Rules.ComboStrikeEvery, Is.EqualTo(50));
            Assert.That(KeysUntilStrike(loop, input, 100), Is.EqualTo(50));
            Assert.That(KeysUntilStrike(loop, input, 100), Is.EqualTo(50));
            Assert.That(loop.State.Resource, Is.EqualTo(4 * Rules.WhirlResource).Within(1e-9));
        }

        [Test]
        public void TheStreakStrikeAlsoHitsTheNextEnemyInTheQueue()
        {
            var (loop, input) = Ronin();

            KeysUntilStrike(loop, input, 100);

            Assert.That(loop.State.QueueDamage[0].Sign, Is.GreaterThan(0));
            Assert.That(loop.State.QueueDamage[1].Sign, Is.Zero);
        }

        [Test]
        public void TreeLinesShortenTheStreakAndHitMoreOfTheQueue()
        {
            var (loop, input) = Ronin(
                new Modifier(CombatStats.ComboStrikeEvery, ModifierKind.Flat, -10, StatTags.Keyboard),
                new Modifier(CombatStats.ComboStrikeTargets, ModifierKind.Flat, 1, StatTags.Keyboard));

            Assert.That(KeysUntilStrike(loop, input, 100), Is.EqualTo(40));
            Assert.That(loop.State.QueueDamage[1].Sign, Is.GreaterThan(0));
            Assert.That(loop.State.QueueDamage[2].Sign, Is.Zero);
        }

        [Test]
        public void TheStreakNeverGetsShorterThanItsFloor()
        {
            var (loop, input) = Ronin(new Modifier(CombatStats.ComboStrikeEvery, ModifierKind.Flat, -100, StatTags.Keyboard));

            Assert.That(KeysUntilStrike(loop, input, 100), Is.EqualTo(Rules.ComboStrikeEveryMin));
        }

        [Test]
        public void AtFullBreathTheStrikeComesSooner()
        {
            var (loop, input) = Ronin(new Modifier(CombatStats.ComboStrikeEveryAtFullResource, ModifierKind.Flat, -10, StatTags.Keyboard));
            loop.State.Resource = 100;
            loop.Tick();

            Assert.That(KeysUntilStrike(loop, input, 100), Is.EqualTo(40));
        }

        [Test]
        public void ACritOfTheStreakStrikeHitsHarderWithItsLine()
        {
            var crit = new Modifier(CombatStats.ComboStrikeCrit, ModifierKind.Flat, 1, StatTags.Keyboard);
            var (plain, plainInput) = Ronin(crit);
            var (strong, strongInput) = Ronin(crit, new Modifier(CombatStats.ComboStrikeCritMultiplier, ModifierKind.Flat, 0.5, StatTags.Keyboard));

            KeysUntilStrike(plain, plainInput, 100);
            KeysUntilStrike(strong, strongInput, 100);

            Assert.That(Strike(strong) / Strike(plain), Is.EqualTo(1.5).Within(1e-9));
        }

        private static double Strike(GameLoop loop)
        {
            for (var e = 0; e < loop.Events.Count; e++)
            {
                if (loop.Events[e].Kind == GameEventKind.Damage && (loop.Events[e].Flags & HitFlags.ComboStrike) != 0)
                    return loop.Events[e].Amount.ToDouble();
            }
            throw new AssertionException("no streak strike");
        }
    }
}
