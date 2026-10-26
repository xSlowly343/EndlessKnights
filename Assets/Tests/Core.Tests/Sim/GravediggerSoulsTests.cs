using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GravediggerSoulsTests
    {
        private static MinionRules Rules => TestWorld.Data.Combat.Skeletons;
        private static ClassResource Souls => TestWorld.Data.Combat.Resources.For("gravedigger");
        private static GemDef Colleague => TestWorld.Real.Gems["call_a_colleague"];
        private static SummonEffect Skeleton => (SummonEffect)Colleague.Skill!.Effect;
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        private static (GameLoop Loop, FakeInputSource Input) Gravedigger(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Gravedigger }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            loop.Events.Clear();
            return (loop, input);
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        private static void Summon(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Skills.Minions.Place(Skeleton, Colleague.Tags);
        }

        [Test]
        public void CallAColleagueIsTheGravediggersStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("gravedigger"), Has.Some.Property("Id").EqualTo("call_a_colleague"));
            Assert.That(Colleague.Skill!.Weapon, Is.EqualTo("shovel"));
        }

        [Test]
        public void CastingRaisesASkeleton()
        {
            var (loop, _) = Gravedigger();
            loop.State.Resource = 100;
            TestWorld.ArmFor(loop, Colleague);

            Assert.That(loop.Skills.TryCast(loop.Skills.Equip(Colleague)), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(1));
        }

        [Test]
        public void SkeletonHitsOncePerSecondForSixtySeconds()
        {
            var (loop, _) = Gravedigger();
            Summon(loop, 1);
            var hp = loop.State.EnemyHp;

            Wait(loop, 3);
            Assert.That((hp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(KeyDamage * Skeleton.Multiplier * 3).Within(1e-6));

            Wait(loop, Skeleton.Seconds - 3);
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(0));
            Assert.That(loop.Skills.Minions.SkeletonHits, Is.EqualTo((long)Skeleton.Seconds));
        }

        [Test]
        public void FiveSkeletonsStandAndMinionCountAddsMore()
        {
            var (plain, _) = Gravedigger();
            var (more, _) = Gravedigger(new Modifier(CombatStats.MinionCount, ModifierKind.Flat, 2));

            Summon(plain, Rules.Max + 3);
            Summon(more, Rules.Max + 3);

            Assert.That(plain.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(Rules.Max));
            Assert.That(more.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(Rules.Max + 2));
        }

        [Test]
        public void SkeletonsAndCannonsHaveSeparateLimits()
        {
            var (loop, _) = Gravedigger();
            var cannon = (SummonEffect)TestWorld.Real.Gems["cannon_by_name"].Skill!.Effect;

            Summon(loop, Rules.Max);
            for (var i = 0; i < 3; i++)
                loop.Skills.Minions.Place(cannon, StatTags.Skill);

            Assert.That(loop.Skills.Minions.Standing(MinionKind.Skeleton), Is.EqualTo(Rules.Max));
            Assert.That(loop.Skills.Minions.Standing(MinionKind.Cannon), Is.EqualTo(3));
        }

        [Test]
        public void LanternMakesSkeletonsHitFasterWhileTheMouseMoves()
        {
            var (loop, input) = Gravedigger();
            Summon(loop, 1);

            for (var i = 0; i < 10 * GameLoop.TicksPerSecond; i++)
            {
                input.MoveMouse(1);
                loop.Tick();
            }

            Assert.That(loop.Skills.Minions.SkeletonHits, Is.EqualTo((long)(10 * Rules.MouseSpeed)));
        }

        [Test]
        public void SoulsComeFromKillsAndKeys()
        {
            var (loop, input) = Gravedigger();

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e-9));
            Wait(loop, 5);
            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Souls.PerKill + Souls.PerKey).Within(1e-6));
        }
    }
}
