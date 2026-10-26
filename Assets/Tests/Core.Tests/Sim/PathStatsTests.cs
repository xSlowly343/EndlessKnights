using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PathStatsTests
    {
        private static ConditionRules Conditions => TestWorld.Data.Combat.Conditions;

        private static (GameLoop Loop, FakeInputSource Input) Hero(EnemyType enemy, params Modifier[] modifiers) =>
            Hero(HeroClass.Viking, enemy, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = heroClass }), hero);
            loop.State.SpawnEnemy(null, enemy, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = enemy == EnemyType.Normal ? 0 : 900;
            loop.State.HitsOnEnemy = Conditions.FirstHits;
            loop.Events.Clear();
            return (loop, input);
        }

        private static double KeyHit(GameLoop loop, FakeInputSource input)
        {
            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Keyboard)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        private static double Ratio(EnemyType enemy, Modifier modifier, System.Action<GameLoop>? setup = null)
        {
            var (plain, plainInput) = Hero(enemy);
            var (strong, strongInput) = Hero(enemy, modifier);
            setup?.Invoke(plain);
            setup?.Invoke(strong);
            return KeyHit(strong, strongInput) / KeyHit(plain, plainInput);
        }

        private static void Ticks(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Tick();
        }

        [Test]
        public void DamageVsBossWorksOnlyOnBosses()
        {
            var modifier = new Modifier(CombatStats.DamageVsBoss, ModifierKind.Increased, 0.5);

            Assert.That(Ratio(EnemyType.Boss, modifier), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(Ratio(EnemyType.Normal, modifier), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void DamageAtBossTimerEndWorksOnlyInTheLastSeconds()
        {
            var modifier = new Modifier(CombatStats.DamageAtBossTimerEnd, ModifierKind.More, 0.5);
            var endTicks = (int)(Conditions.BossTimerEndSeconds * GameLoop.TicksPerSecond) - 5;

            Assert.That(Ratio(EnemyType.Boss, modifier), Is.EqualTo(1).Within(1e-9));
            Assert.That(Ratio(EnemyType.Boss, modifier, loop => loop.State.BossTicksLeft = endTicks), Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void FirstHitsOnAnEnemyAreStronger()
        {
            var (loop, input) = Hero(EnemyType.Normal, new Modifier(CombatStats.FirstHitsBonus, ModifierKind.Flat, 0.4));
            var (plain, plainInput) = Hero(EnemyType.Normal);
            loop.State.HitsOnEnemy = 0;
            plain.State.HitsOnEnemy = 0;

            for (var i = 0; i < Conditions.FirstHits; i++)
                Assert.That(KeyHit(loop, input) / KeyHit(plain, plainInput), Is.EqualTo(1.4).Within(1e-9), $"hit {i + 1}");
            Assert.That(KeyHit(loop, input) / KeyHit(plain, plainInput), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void NewEnemyStartsItsFirstHitsAgain()
        {
            var (loop, _) = Hero(EnemyType.Normal);

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(100));

            Assert.That(loop.State.HitsOnEnemy, Is.EqualTo(0));
        }

        [Test]
        public void KillsInARowAddDamageUpToTheCap()
        {
            var modifier = new Modifier(CombatStats.DamagePerKill, ModifierKind.Flat, 0.04);

            Assert.That(Ratio(EnemyType.Normal, modifier, loop => loop.State.KillStreak = 10), Is.EqualTo(1.4).Within(1e-9));
            Assert.That(Ratio(EnemyType.Normal, modifier, loop => loop.State.KillStreak = Conditions.KillStreakMax * 10),
                Is.EqualTo(1 + 0.04 * Conditions.KillStreakMax).Within(1e-9));
        }

        [Test]
        public void KillStreakGrowsOnKillsAndBreaksAfterAPause()
        {
            var (loop, input) = Hero(EnemyType.Normal, new Modifier(CombatStats.Damage, ModifierKind.More, 1e60));

            input.PressKeys(1);
            loop.Tick();
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.KillStreak, Is.EqualTo(2));

            Ticks(loop, (int)(Conditions.KillStreakResetSeconds * GameLoop.TicksPerSecond) + 1);
            Assert.That(loop.State.KillStreak, Is.EqualTo(0));
        }

        [Test]
        public void CullingKillsAWoundedEnemy()
        {
            var cull = new Modifier(CombatStats.CullThreshold, ModifierKind.Flat, 0.15);
            var (loop, input) = Hero(EnemyType.Normal, cull);
            var (plain, plainInput) = Hero(EnemyType.Normal);
            foreach (var hero in new[] { loop, plain })
            {
                hero.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
                hero.State.EnemyHp = BigNumber.FromDouble(1e5);
            }

            KeyHit(loop, input);
            KeyHit(plain, plainInput);

            Assert.That(loop.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(1));
            Assert.That(plain.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(0));
        }

        [Test]
        public void CullingThresholdHasACap()
        {
            var (loop, input) = Hero(EnemyType.Normal, new Modifier(CombatStats.CullThreshold, ModifierKind.Flat, 0.99));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.State.EnemyHp = BigNumber.FromDouble(1e6 * (Conditions.CullMax + 0.05));

            KeyHit(loop, input);

            Assert.That(loop.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(0));
        }

        [Test]
        public void BossTimerSlowStretchesTheTimer()
        {
            var (loop, _) = Hero(EnemyType.Boss, new Modifier(CombatStats.BossTimerSlow, ModifierKind.Increased, 1.0));
            var before = loop.State.BossTicksLeft;

            Ticks(loop, 20);

            Assert.That(before - loop.State.BossTicksLeft, Is.EqualTo(10));
        }

        [Test]
        public void ReturningAfterALongPauseGivesDamageAfterIdleForAWhile()
        {
            var modifier = new Modifier(CombatStats.DamageAfterIdle, ModifierKind.More, 0.5);
            var pause = (int)(Conditions.AfterIdlePauseSeconds * GameLoop.TicksPerSecond) + 1;
            var (loop, input) = Hero(EnemyType.Normal, modifier);
            var (plain, plainInput) = Hero(EnemyType.Normal);
            Ticks(loop, pause);
            Ticks(plain, pause);

            Assert.That(KeyHit(loop, input) / KeyHit(plain, plainInput), Is.EqualTo(1.5).Within(1e-9));

            var window = (int)(Conditions.AfterIdleSeconds * GameLoop.TicksPerSecond);
            for (var i = 0; i < window; i++)
            {
                KeyHit(loop, input);
                KeyHit(plain, plainInput);
            }
            Assert.That(KeyHit(loop, input) / KeyHit(plain, plainInput), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void ResourceMaxRaisesTheResourceLimit()
        {
            var (loop, _) = Hero(EnemyType.Normal, new Modifier(CombatStats.ResourceMax, ModifierKind.Flat, 20));

            Assert.That(loop.Resource.Max, Is.EqualTo(TestWorld.Data.Combat.Resources.Max + 20).Within(1e-9));
        }

        [Test]
        public void RageBurnDelayKeepsRageLonger()
        {
            var (loop, input) = Hero(EnemyType.Normal, new Modifier(CombatStats.RageBurnDelaySeconds, ModifierKind.Flat, 2));
            loop.State.Resource = 50;
            input.PressKeys(1);
            loop.Tick();
            var afterKey = loop.State.Resource;

            Ticks(loop, 49);
            Assert.That(loop.State.Resource, Is.EqualTo(afterKey).Within(1e-9));

            Ticks(loop, 3);
            Assert.That(loop.State.Resource, Is.LessThan(afterKey));
        }

        [Test]
        public void SkillCooldownShortensCooldowns()
        {
            var (loop, _) = Hero(EnemyType.Normal, new Modifier(CombatStats.SkillCooldown, ModifierKind.Increased, -0.2));
            var gem = TestWorld.Data.Gems["iron_rim_bash"];
            loop.Skills.Equip(gem);
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.State.Skills[0].CooldownTicks, Is.EqualTo((int)System.Math.Round(gem.Skill!.CooldownSeconds * 0.8 * GameLoop.TicksPerSecond)));
        }
    }
}
