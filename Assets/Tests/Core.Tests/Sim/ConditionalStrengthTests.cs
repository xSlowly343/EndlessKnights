using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ConditionalStrengthTests
    {
        private static GemDef Debuff(string id, string mark) =>
            new GemDef(id, GemKind.Skill, GemColor.Blue, id, id, StatTags.Skill,
                skill: new SkillDef(0, 1, 10, new EnemyDebuffEffect(0.2), new[] { mark }));

        private static GameLoop Voodoo(params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = HeroClass.Voodoo }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            return loop;
        }

        private static void Cast(GameLoop loop, GemDef gem)
        {
            var index = loop.Skills.Equip(gem);
            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast), gem.Id);
        }

        [Test]
        public void ACurseGrowsStrongerWhenItsConditionStartsToHold()
        {
            var loop = Voodoo(new Modifier(CombatStats.CurseEffect, ModifierKind.Increased, 0.5, StatTags.None, ModifierPool.General, ModifierCondition.Time));
            Cast(loop, Debuff("hex", "curse"));
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(1.2).Within(1e-9));

            Cast(loop, Debuff("slow", "time"));
            loop.Tick();

            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo((1 + 0.2 * 1.5) * 1.2).Within(1e-9));
        }
    }
}
