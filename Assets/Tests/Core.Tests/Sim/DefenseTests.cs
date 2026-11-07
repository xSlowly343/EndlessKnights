using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DefenseTests
    {
        private static DefenseRules Rules => TestWorld.Data.Combat.Defense;

        private static (GameLoop Loop, FakeInputSource Input) Fighting(StatSheet hero, double defense)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(7), hero);
            loop.Tick();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 100));
            loop.State.EnemyDefense = defense;
            return (loop, input);
        }

        private static BigNumber OneHit(StatSheet hero, double defense)
        {
            var (loop, input) = Fighting(hero, defense);
            input.PressKeys(1);
            loop.Tick();
            return loop.Combat.LastTickDamage;
        }

        private static StatSheet With(StatId stat, double value)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(stat, ModifierKind.Flat, value));
            return hero;
        }

        [Test]
        public void DefenseGrowsByStageTier()
        {
            Assert.That(Rules.Share(1, false), Is.Zero);
            Assert.That(Rules.Share(500, false), Is.Zero);
            Assert.That(Rules.Share(500, true), Is.Zero, "no boss extra before defense starts");
            Assert.That(Rules.Share(5000, false), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(Rules.Cut(Rules.Defense(5000, false)), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(Rules.Cut(Rules.Defense(20000, false)), Is.EqualTo(0.4).Within(0.002));
            Assert.That(Rules.Share(5000, true), Is.EqualTo(0.55).Within(1e-9));
            Assert.That(Rules.Share(10_000_000, false), Is.EqualTo(1.5));
            Assert.That(Rules.Cut(Rules.Defense(10_000_000, false)), Is.EqualTo(0.6).Within(1e-9));
        }

        [Test]
        public void TheCutIsBuiltIntoTheHpCurveSoWithoutPenetrationNothingChanges()
        {
            var plain = OneHit(new StatSheet(), 0);
            var armored = OneHit(new StatSheet(), Rules.K);
            Assert.That(armored, Is.EqualTo(plain));
        }

        [Test]
        public void ShredOnAWholeDefenseGivesTheExampleGain()
        {
            var plain = OneHit(new StatSheet(), Rules.K);
            var shredded = OneHit(With(CombatStats.DefenseShred, 0.3), Rules.K);
            Assert.That((shredded / plain).ToDouble(), Is.EqualTo(2 / 1.7).Within(1e-9));
        }

        [Test]
        public void PenetrationAndShredMultiplyAndAreCapped()
        {
            Assert.That(Rules.Gain(Rules.K, 0.5, 0.5), Is.EqualTo(2 / 1.25).Within(1e-9));
            Assert.That(Rules.Gain(Rules.K, 0.9, 0), Is.EqualTo(Rules.Gain(Rules.K, 0.5, 0)), "shred stops at 50%");
            Assert.That(Rules.Gain(Rules.K, 0, 0.95), Is.EqualTo(Rules.Gain(Rules.K, 0, 0.7)), "penetration stops at 70%");
            Assert.That(Rules.Gain(0, 0.5, 0.5), Is.EqualTo(1), "no defense, nothing to break");
        }

        [Test]
        public void StagesSpawnEnemiesWithTheirDefense()
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1) { Stage = 5000, HighestStage = 5000, RecordStage = 5000 });
            loop.Tick();
            Assert.That(loop.State.EnemyDefense, Is.EqualTo(Rules.Defense(5000, loop.State.FightingBoss)).Within(1e-6));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.One);
            Assert.That(loop.State.EnemyDefense, Is.Zero);
        }

        [Test]
        public void OnASharedEnemyShredGoesToTheHostForTheWholeParty()
        {
            var hero = With(CombatStats.DefenseShred, 0.3);
            var (loop, input) = Fighting(hero, Rules.K);
            loop.Skills.ShareEnemy(true);
            input.PressKeys(1);
            loop.Tick();
            var found = 0.0;
            var effects = loop.State.TraitEnemyEffects;
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i].Strength > 0.17 && effects[i].Strength < 0.18)
                    found = effects[i].Strength;
            }
            Assert.That(found, Is.EqualTo(2 / 1.7 - 1).Within(1e-9));
            Assert.That(loop.Combat.DefenseGain(StatTags.Strike), Is.EqualTo(1), "the own hit leaves the shred to the host");
        }
    }
}
