using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HuntressHunterMarkTests
    {
        private static HunterMarkRules Rules => TestWorld.Data.Combat.Classes.HunterMark;

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers) =>
            Hero(heroClass, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.State.MarkedEnemyDied = false;
            loop.Events.Clear();
            return (loop, input);
        }

        private static double Key(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
            for (var i = loop.Events.Count - 1; i >= 0; i--)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage)
                    return loop.Events[i].Amount.ToDouble();
            }
            throw new AssertionException("no damage events");
        }

        private static int MarkAt(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var i = 1; i <= keys; i++)
            {
                Key(loop, input);
                if (loop.State.HunterMarkTicks > 0)
                    return i;
            }
            return 0;
        }

        private static bool MarkShown(GameLoop loop) =>
            (loop.Synergy.OwnMarks & TestWorld.Data.Synergy.MaskOf(new[] { "hunterMark" })) != 0;

        [Test]
        public void EveryTwentiethKeyMarksTheEnemyAndKeysHitItHarder()
        {
            Assert.That(Rules.Every, Is.EqualTo(20));
            Assert.That(Rules.Seconds, Is.EqualTo(8));
            var (loop, input) = Hero(HeroClass.Amazon);

            Assert.That(MarkAt(loop, input, Rules.Every), Is.EqualTo(Rules.Every));
            var marked = Key(loop, input);
            loop.State.HunterMarkTicks = 0;
            var plain = Key(loop, input);

            Assert.That(marked / plain, Is.EqualTo(1 + Rules.KeyDamage).Within(0.05));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Mouse), Is.EqualTo(1));
        }

        [Test]
        public void TheMarkIsShownToAlliesAndWearsOff()
        {
            var (loop, input) = Hero(HeroClass.Amazon);
            MarkAt(loop, input, Rules.Every);
            loop.Tick();
            Assert.That(MarkShown(loop), Is.True);

            for (var i = 0; i < Rules.Seconds * GameLoop.TicksPerSecond + 1; i++)
                loop.Tick();

            Assert.That(loop.State.HunterMarkTicks, Is.Zero);
            Assert.That(MarkShown(loop), Is.False);
        }

        [Test]
        public void TreeLinesBringTheMarkCloserAndMakeItLastAndHitHarder()
        {
            var (loop, input) = Hero(HeroClass.Amazon,
                new Modifier(CombatStats.MarkEvery, ModifierKind.Flat, -2),
                new Modifier(CombatStats.MarkSeconds, ModifierKind.Flat, 3),
                new Modifier(CombatStats.MarkDamage, ModifierKind.Flat, 0.1));

            Assert.That(MarkAt(loop, input, Rules.Every), Is.EqualTo(Rules.Every - 2));
            Assert.That(loop.State.HunterMarkTicks, Is.EqualTo((int)((Rules.Seconds + 3) * GameLoop.TicksPerSecond) - 1).Within(1));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Keyboard), Is.EqualTo(1 + Rules.KeyDamage + 0.1).Within(1e-9));
        }

        [Test]
        public void AForeverMarkStaysAndAJumpingMarkMovesToTheNextEnemy()
        {
            var (loop, input) = Hero(HeroClass.Amazon,
                new Modifier(CombatStats.MarkForever, ModifierKind.Flat, 1),
                new Modifier(CombatStats.MarkJump, ModifierKind.Flat, 1));
            MarkAt(loop, input, Rules.Every);
            for (var i = 0; i < 200; i++)
                loop.Tick();
            Assert.That(loop.State.HunterMarkTicks, Is.GreaterThan(0));

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Tick();

            Assert.That(loop.State.HunterMarkTicks, Is.GreaterThan(0));
        }

        [Test]
        public void WithoutJumpTheMarkDiesWithTheEnemy()
        {
            var (loop, input) = Hero(HeroClass.Amazon);
            MarkAt(loop, input, Rules.Every);

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Tick();

            Assert.That(loop.State.HunterMarkTicks, Is.Zero);
        }

        [Test]
        public void ABossOnlyMarkSkipsNormalEnemiesAndIsStrongerOnBosses()
        {
            var bossOnly = new[] { new Modifier(CombatStats.MarkBossOnly, ModifierKind.Flat, 1), new Modifier(CombatStats.MarkPower, ModifierKind.Flat, 1) };
            var (normal, normalInput) = Hero(HeroClass.Amazon, bossOnly);
            Assert.That(MarkAt(normal, normalInput, Rules.Every * 2), Is.Zero);

            var (guardian, guardianInput) = Hero(HeroClass.Amazon, EnemyType.Guardian, bossOnly);
            Assert.That(MarkAt(guardian, guardianInput, Rules.Every * 2), Is.Zero);

            var (boss, bossInput) = Hero(HeroClass.Amazon, EnemyType.Boss, bossOnly);
            Assert.That(MarkAt(boss, bossInput, Rules.Every), Is.EqualTo(Rules.Every));
            Assert.That(boss.Combat.MarkedMultiplier(StatTags.Keyboard), Is.EqualTo(1 + Rules.KeyDamage * 2).Within(1e-9));

            var (onGuardian, onGuardianInput) = Hero(HeroClass.Amazon, EnemyType.Guardian,
                new Modifier(CombatStats.MarkBossOnly, ModifierKind.Flat, 1), new Modifier(CombatStats.MarkOnGuardians, ModifierKind.Flat, 1));
            Assert.That(MarkAt(onGuardian, onGuardianInput, Rules.Every), Is.EqualTo(Rules.Every));
        }

        [Test]
        public void OtherClassesDoNotMark()
        {
            var (loop, input) = Hero(HeroClass.Viking);

            Assert.That(MarkAt(loop, input, Rules.Every * 2), Is.Zero);
            Assert.That(loop.State.KeysTowardMark, Is.Zero);
        }

        [Test]
        public void TheCountTowardTheMarkIsSaved()
        {
            var (loop, input) = Hero(HeroClass.Amazon);
            MarkAt(loop, input, 7);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.KeysTowardMark, Is.EqualTo(7));
        }
    }
}
