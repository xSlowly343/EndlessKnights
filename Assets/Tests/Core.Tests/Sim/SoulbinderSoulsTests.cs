using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class SoulbinderSoulsTests
    {
        private static MinionRules Rules => TestWorld.Data.Combat.Skeletons;
        private static ClassResource Souls => TestWorld.Data.Combat.Resources.For("soulbinder");
        private static GemDef Colleague => TestWorld.Real.Gems["soulbinder_hit_2"];
        private static SummonEffect Skeleton => (SummonEffect)Colleague.Skill!.Effect;
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;

        private static (GameLoop Loop, FakeInputSource Input) Soulbinder(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Soulbinder }, hero);
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
        public void SoulsComeFromKillsAndKeys()
        {
            var (loop, input) = Soulbinder();

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e-9));
            Wait(loop, 5);
            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(Souls.PerKill + Souls.PerKey).Within(1e-6));
        }
    }
}
