using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ClockmakerSandTests
    {
        private static double FastKillSeconds => TestWorld.Data.Combat.Conditions.FastKillSeconds;
        private static ClassResource Sand => TestWorld.Data.Combat.Resources.For("clockmaker");

        private static (GameLoop Loop, FakeInputSource Input) Clockmaker(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Clockmaker }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            SpawnTough(loop);
            loop.Events.Clear();
            return (loop, input);
        }

        private static void SpawnTough(GameLoop loop) =>
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static void KillAfter(GameLoop loop, FakeInputSource input, double seconds)
        {
            SpawnTough(loop);
            Wait(loop, seconds);
            loop.State.EnemyHp = BigNumber.FromDouble(1e-9);
            input.PressKeys(1);
            loop.Tick();
        }

        private static GemDef StretchSecond => TestWorld.Real.Gems["stretch_second"];

        [Test]
        public void SandComesFromKeysAndMouse()
        {
            var (loop, input) = Clockmaker();

            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }
            Assert.That(loop.State.Resource, Is.EqualTo(10 * Sand.PerKey).Within(1e-9));

            for (var i = 0; i < 10; i++)
            {
                input.MoveMouse((float)Sand.MousePxPerPoint / 10);
                loop.Tick();
            }
            Assert.That(loop.State.Resource, Is.EqualTo(10 * Sand.PerKey + 1).Within(0.05));
        }

        [Test]
        public void FastKillPoursTheSavedSecondsIntoSand()
        {
            var (loop, input) = Clockmaker();

            KillAfter(loop, input, 1);

            var expected = (FastKillSeconds - 1) * Sand.PerFastKillSecond + Sand.PerKey;
            Assert.That(loop.State.Resource, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void SlowKillGivesNoSand()
        {
            var (loop, input) = Clockmaker();

            KillAfter(loop, input, FastKillSeconds + 1);

            Assert.That(loop.State.Resource, Is.EqualTo(Sand.PerKey).Within(1e-6));
        }

        [Test]
        public void SandFromFastKillsStatGrowsTheGain()
        {
            var (loop, input) = Clockmaker(new Modifier(CombatStats.SandFromFastKills, ModifierKind.Increased, 0.5));

            KillAfter(loop, input, 1);

            var expected = (FastKillSeconds - 1) * Sand.PerFastKillSecond * 1.5 + Sand.PerKey;
            Assert.That(loop.State.Resource, Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void OtherCharactersGetNoSandFromFastKills()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Voodoo }), new StatSheet());
            var before = loop.State.Resource;

            KillAfter(loop, input, 1);

            var voodoo = TestWorld.Data.Combat.Resources.For("voodoo");
            Assert.That(loop.State.Resource - before, Is.EqualTo(voodoo.PerKey + voodoo.PerSecond * 1.1).Within(1e-6));
        }

        [Test]
        public void StretchSecondSlowsTheBossTimerAndMakesTheEnemyTakeMore()
        {
            var (loop, _) = Clockmaker();
            loop.State.Resource = 100;
            TestWorld.ArmFor(loop, StretchSecond);
            var index = loop.Skills.Equip(StretchSecond);

            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.BossTimerSpeed, 1), Is.EqualTo(0.7).Within(1e-9));
        }

        [Test]
        public void StretchSecondIsTheClockmakersStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("clockmaker"), Has.Some.Property("Id").EqualTo("stretch_second"));
            Assert.That(StretchSecond.Skill!.Weapon, Is.EqualTo("pendulum"));
        }

        [Test]
        public void BankKeepsSavedSecondsUpToItsCapAndSpendsThemOnTheBoss()
        {
            var (loop, input) = Clockmaker(new Modifier(CombatStats.BossTimerBank, ModifierKind.Flat, 3));

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
            var (loop, input) = Clockmaker();

            KillAfter(loop, input, 1);

            Assert.That(loop.State.BankedSeconds, Is.EqualTo(0));
        }

        [Test]
        public void BankOfSecondsIsAPassiveWithAThirtySecondBank()
        {
            var gem = TestWorld.Real.Gems["bank_of_seconds"];

            Assert.That(gem.Locked, Is.False);
            Assert.That(gem.Skill!.IsPassive, Is.True);
            var passive = (PassiveEffect)gem.Skill.Effect;
            Assert.That(passive.Modifiers[0].Stat, Is.EqualTo(CombatStats.BossTimerBank));
            Assert.That(passive.Modifiers[0].Value, Is.EqualTo(30));
        }
    }
}
