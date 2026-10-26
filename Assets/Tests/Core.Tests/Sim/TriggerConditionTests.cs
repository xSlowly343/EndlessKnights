using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TriggerConditionTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Hero(string trigger, string skill = "through_stride", HeroClass heroClass = HeroClass.Ronin, params string[] before)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = heroClass }));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            foreach (var gem in before)
                loop.Skills.Equip(TestWorld.Data.Gems[gem]);
            loop.Skills.Equip(TestWorld.Data.Gems[skill], TestWorld.Data.Gems[trigger]);
            loop.State.Resource = 90;
            loop.Events.Clear();
            return (loop, input);
        }

        private static int Casts(GameLoop loop) => loop.Events.CountOf(GameEventKind.SkillCast);

        private static void Ticks(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Tick();
        }

        private static void Kill(GameLoop loop, FakeInputSource input)
        {
            loop.State.EnemyHp = BigNumber.FromDouble(1e-6);
            input.PressKeys(1);
            loop.Tick();
            loop.State.EnemyHp = BigNumber.Create(1, 50);
            loop.Tick();
        }

        [Test]
        public void MouseDistanceStartsOverAfterTheCast()
        {
            var (loop, input) = Hero("cast_on_mouse_distance");
            input.MoveMouse(1500);
            loop.Tick();
            loop.State.Skills[0].CooldownTicks = 0;

            input.MoveMouse(900);
            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [TestCase(51, 0)]
        [TestCase(49, 1)]
        public void BossTimerTriggerFiresInTheLastFiveSeconds(int ticksLeft, int casts)
        {
            var (loop, _) = Hero("cast_on_boss_timer_low");
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = ticksLeft;

            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(casts));
        }

        [Test]
        public void IdleTriggerFiresAfterThirtySecondsWithoutInput()
        {
            var (loop, input) = Hero("cast_on_idle");

            Ticks(loop, 299);
            Assert.That(Casts(loop), Is.EqualTo(0));

            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [Test]
        public void BossKillTriggerFiresAfterTheBossDies()
        {
            var (loop, input) = Hero("cast_on_boss_kill");
            loop.State.SpawnEnemy(null, EnemyType.Boss, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 300;

            Kill(loop, input);

            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [Test]
        public void NormalKillDoesNotFireTheBossKillTrigger()
        {
            var (loop, input) = Hero("cast_on_boss_kill");

            Kill(loop, input);

            Assert.That(Casts(loop), Is.EqualTo(0));
        }

        [TestCase(99, 0)]
        [TestCase(100, 1)]
        public void FullResourceTriggerWaitsForAFullBar(double resource, int casts)
        {
            var (loop, _) = Hero("cast_on_full_resource");
            loop.State.Resource = resource;

            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(casts));
        }

        [Test]
        public void WithLeftTriggerFollowsTheLeftButtonSkill()
        {
            var (loop, input) = Hero("cast_with_left", "through_stride", HeroClass.Ronin, "march");

            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(0));

            input.ClickLeft();
            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(2));
            Assert.That(loop.State.Skills[1].Ready, Is.False);
        }

        [Test]
        public void WithRightTriggerFollowsTheRightButtonSkill()
        {
            var (loop, input) = Hero("cast_with_right", "through_stride", HeroClass.Ronin, "lull", "march");

            input.ClickLeft();
            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(0));

            input.ClickRight();
            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(2));
        }

        [Test]
        public void TenthKillTriggerCountsKills()
        {
            var (loop, input) = Hero("cast_on_tenth_kill");

            for (var i = 0; i < 9; i++)
                Kill(loop, input);
            Assert.That(Casts(loop), Is.EqualTo(0));

            Kill(loop, input);
            Assert.That(loop.State.Kills, Is.EqualTo(10));
            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [Test]
        public void BossSpawnTriggerFiresWhenTheBossFightStarts()
        {
            var (loop, input) = Hero("cast_on_boss_spawn");
            loop.State.KillsOnStage = TestWorld.Data.Stages.EnemiesPerStage - 1;

            Kill(loop, input);
            Assert.That(loop.State.FightingBoss, Is.True);
            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [Test]
        public void CircleTriggerNeedsOneFullTurn()
        {
            var (loop, input) = Hero("cast_on_circle");

            input.Circle(0.6f);
            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(0));

            input.Circle(0.6f);
            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(1));
        }

        [TestCase(10, 0)]
        [TestCase(35, 1)]
        public void AllyTriggerFiresOnAnAllysHeavySkill(double allyCooldown, int casts)
        {
            var (loop, _) = Hero("cast_with_ally");
            loop.State.AllyHeavyCastSeconds = allyCooldown;

            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(casts));
            Assert.That(loop.State.AllyHeavyCastSeconds, Is.EqualTo(0));
        }

        [TestCase(25, 0)]
        [TestCase(15, 1)]
        public void LowResourceTriggerCastsCheapSkills(double resource, int casts)
        {
            var (loop, _) = Hero("cast_on_low_resource", "hatchet_toss", HeroClass.Viking);
            loop.State.Resource = resource;

            loop.Tick();

            Assert.That(Casts(loop), Is.EqualTo(casts));
        }

        [Test]
        public void TimerTriggerCastsEveryThirtySeconds()
        {
            var (loop, input) = Hero("cast_on_timer");

            for (var i = 0; i < 300; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(Casts(loop), Is.EqualTo(0));

            loop.Tick();
            Assert.That(Casts(loop), Is.EqualTo(1));

            loop.State.Resource = 90;
            Ticks(loop, 300);
            Assert.That(Casts(loop), Is.EqualTo(2));
        }
    }
}
