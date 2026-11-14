using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class HuntressHunterMarkTests
    {
        private static HunterMarkRules Rules => TestWorld.Data.Combat.Classes.HunterMark;

        private static int ShotEvery => TestWorld.Data.Combat.Classes.Windshot.ShotEvery;

        private static readonly Modifier LongShot = new Modifier(WindshotStats.ShotPower, ModifierKind.Flat, 1);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers) =>
            Hero(heroClass, EnemyType.Normal, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            hero.Add(LongShot);
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
                loop.State.Resource = 1000;
                loop.Tick();
                Key(loop, input);
                if (loop.State.HunterMarkTicks > 0)
                    return i;
            }
            return 0;
        }

        [Test]
        public void AShotAtFullTensionMarksTheEnemyAndKeysHitItHarder()
        {
            Assert.That(Rules.Seconds, Is.EqualTo(8));
            var (loop, input) = Hero(HeroClass.Windshot);

            Assert.That(MarkAt(loop, input, ShotEvery), Is.EqualTo(ShotEvery));
            var marked = Key(loop, input);
            loop.State.HunterMarkTicks = 0;
            var plain = Key(loop, input);

            Assert.That(marked / plain, Is.EqualTo(1 + Rules.StrikeDamage).Within(0.05));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Mouse), Is.EqualTo(1));
        }

        [Test]
        public void TheMarkWearsOff()
        {
            var (loop, input) = Hero(HeroClass.Windshot);
            MarkAt(loop, input, ShotEvery);
            loop.Tick();
            Assert.That(loop.State.HunterMarkTicks, Is.GreaterThan(0));

            for (var i = 0; i < Rules.Seconds * GameLoop.TicksPerSecond + 1; i++)
                loop.Tick();

            Assert.That(loop.State.HunterMarkTicks, Is.Zero);
        }

        [Test]
        public void TreeLinesMakeTheMarkLastAndHitHarder()
        {
            var (loop, input) = Hero(HeroClass.Windshot,
                new Modifier(CombatStats.MarkSeconds, ModifierKind.Flat, 3),
                new Modifier(CombatStats.MarkDamage, ModifierKind.Flat, 0.1));

            Assert.That(MarkAt(loop, input, ShotEvery), Is.EqualTo(ShotEvery));
            Assert.That(loop.State.HunterMarkTicks, Is.EqualTo((int)((Rules.Seconds + 3) * GameLoop.TicksPerSecond) - 1).Within(1));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Strike), Is.EqualTo(1 + Rules.StrikeDamage + 0.1).Within(1e-9));
        }

        [Test]
        public void DebuffEffectMakesTheMarkHitHarder()
        {
            var (loop, input) = Hero(HeroClass.Windshot, new Modifier(CombatStats.EnemyDebuffEffect, ModifierKind.Increased, 0.5));
            Assert.That(MarkAt(loop, input, ShotEvery), Is.EqualTo(ShotEvery));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Strike), Is.EqualTo(1 + Rules.StrikeDamage * 1.5).Within(1e-9));
            Assert.That(loop.Combat.MarkedMultiplier(StatTags.Mouse), Is.EqualTo(1), "no mark line for the mouse");
        }

        [Test]
        public void AForeverMarkStaysAndAJumpingMarkMovesToTheNextEnemy()
        {
            var (loop, input) = Hero(HeroClass.Windshot,
                new Modifier(CombatStats.MarkForever, ModifierKind.Flat, 1),
                new Modifier(CombatStats.MarkJump, ModifierKind.Flat, 1));
            MarkAt(loop, input, ShotEvery);
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
            var (loop, input) = Hero(HeroClass.Windshot);
            MarkAt(loop, input, ShotEvery);

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Tick();

            Assert.That(loop.State.HunterMarkTicks, Is.Zero);
        }

        [Test]
        public void ABossOnlyMarkSkipsNormalEnemiesAndIsStrongerOnBosses()
        {
            var bossOnly = new[] { new Modifier(CombatStats.MarkBossOnly, ModifierKind.Flat, 1), new Modifier(CombatStats.MarkPower, ModifierKind.Flat, 1) };
            var (normal, normalInput) = Hero(HeroClass.Windshot, bossOnly);
            Assert.That(MarkAt(normal, normalInput, ShotEvery * 2), Is.Zero);

            var (guardian, guardianInput) = Hero(HeroClass.Windshot, EnemyType.Guardian, bossOnly);
            Assert.That(MarkAt(guardian, guardianInput, ShotEvery * 2), Is.Zero);

            var (boss, bossInput) = Hero(HeroClass.Windshot, EnemyType.Boss, bossOnly);
            Assert.That(MarkAt(boss, bossInput, ShotEvery), Is.EqualTo(ShotEvery));
            Assert.That(boss.Combat.MarkedMultiplier(StatTags.Strike), Is.EqualTo(1 + Rules.StrikeDamage * 2).Within(1e-9));

            var (onGuardian, onGuardianInput) = Hero(HeroClass.Windshot, EnemyType.Guardian,
                new Modifier(CombatStats.MarkBossOnly, ModifierKind.Flat, 1), new Modifier(CombatStats.MarkOnGuardians, ModifierKind.Flat, 1));
            Assert.That(MarkAt(onGuardian, onGuardianInput, ShotEvery), Is.EqualTo(ShotEvery));
        }

        [Test]
        public void OtherClassesDoNotMark()
        {
            var (loop, input) = Hero(HeroClass.Runedancer);

            Assert.That(MarkAt(loop, input, ShotEvery * 2), Is.Zero);
        }
    }
}
