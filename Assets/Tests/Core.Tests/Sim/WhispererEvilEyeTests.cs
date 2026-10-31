using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class WhispererEvilEyeTests
    {
        private static EvilEyeRules Rules => TestWorld.Data.Combat.Classes.EvilEye;

        private static GameLoop Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
            return loop;
        }

        [Test]
        public void EveryNewEnemyOfTheWhispererTakesFivePercentMore()
        {
            Assert.That(Rules.DamageTaken, Is.EqualTo(0.05));
            var whisperer = Hero(HeroClass.Voodoo, EnemyType.Normal);
            var other = Hero(HeroClass.Viking, EnemyType.Normal);
            whisperer.Tick();

            Assert.That(whisperer.Combat.Class.EvilEyeShare, Is.EqualTo(0.05).Within(1e-9));
            Assert.That(other.Combat.Class.EvilEyeShare, Is.Zero);
        }

        [Test]
        public void TheEvilEyeRaisesOwnDamageWhenTheEnemyIsNotShared()
        {
            var whisperer = Hero(HeroClass.Voodoo, EnemyType.Normal);
            var solo = whisperer.Combat.ConditionMultiplier(StatTags.Keyboard);

            whisperer.State.SharedEnemy = true;

            Assert.That(solo / whisperer.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1.05).Within(1e-9));
        }

        [Test]
        public void TreeLinesStrengthenTheEvilEye()
        {
            var whisperer = Hero(HeroClass.Voodoo, EnemyType.Normal, new Modifier(CombatStats.EvilEyeEffect, ModifierKind.Flat, 0.03));

            Assert.That(whisperer.Combat.Class.EvilEyeShare, Is.EqualTo(0.08).Within(1e-9));
        }

        [Test]
        public void ABossOnlyEvilEyeSkipsOthersAndIsStrongerOnBosses()
        {
            var bossOnly = new[] { new Modifier(CombatStats.EvilEyeBossOnly, ModifierKind.Flat, 1), new Modifier(CombatStats.EvilEyePower, ModifierKind.Flat, 1) };

            Assert.That(Hero(HeroClass.Voodoo, EnemyType.Normal, bossOnly).Combat.Class.EvilEyeShare, Is.Zero);
            Assert.That(Hero(HeroClass.Voodoo, EnemyType.Guardian, bossOnly).Combat.Class.EvilEyeShare, Is.Zero);
            Assert.That(Hero(HeroClass.Voodoo, EnemyType.Boss, bossOnly).Combat.Class.EvilEyeShare, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(Hero(HeroClass.Voodoo, EnemyType.Guardian, bossOnly[0], new Modifier(CombatStats.EvilEyeOnGuardians, ModifierKind.Flat, 1)).Combat.Class.EvilEyeShare,
                Is.EqualTo(0.05).Within(1e-9));
        }
    }
}
