using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BossTimerBankTests
    {
        private static double FastKillSeconds => TestWorld.Data.Combat.Conditions.FastKillSeconds;

        private static (GameLoop Loop, FakeInputSource Input) Runedancer(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Runedancer }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            SpawnTough(loop);
            loop.Events.Clear();
            return (loop, input);
        }

        private static void SpawnTough(GameLoop loop) =>
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));

        private static void KillAfter(GameLoop loop, FakeInputSource input, double seconds)
        {
            SpawnTough(loop);
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
            loop.State.EnemyHp = BigNumber.FromDouble(1e-9);
            input.PressKeys(1);
            loop.Tick();
        }

        [Test]
        public void BankKeepsSavedSecondsUpToItsCapAndSpendsThemOnTheBoss()
        {
            var (loop, input) = Runedancer(new Modifier(CombatStats.BossTimerBank, ModifierKind.Flat, 3));

            KillAfter(loop, input, 1);
            Assert.That(loop.State.BankedSeconds, Is.EqualTo(FastKillSeconds - 1).Within(1e-6));
            KillAfter(loop, input, 1);
            Assert.That(loop.State.BankedSeconds, Is.EqualTo(3).Within(1e-9));

            var timer = TestWorld.Data.Stages.BossTimer(0);
            loop.State.KillsOnStage = TestWorld.Data.Stages.EnemiesOnStage(loop.State.Stage);
            loop.State.EnemyHp = BigNumber.FromDouble(1e-9);
            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.FightingBoss, Is.True);
            Assert.That(loop.State.BankedSeconds, Is.EqualTo(0));
            Assert.That(loop.State.BossSecondsLeft, Is.EqualTo(timer + 3).Within(0.2));
        }

        [Test]
        public void WithoutTheBankNothingIsSaved()
        {
            var (loop, input) = Runedancer();

            KillAfter(loop, input, 1);

            Assert.That(loop.State.BankedSeconds, Is.EqualTo(0));
        }
    }
}
