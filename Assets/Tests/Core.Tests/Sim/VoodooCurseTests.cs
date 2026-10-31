using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class VoodooCurseTests
    {
        private static CurseRules Rules => TestWorld.Data.Combat.Curses;

        private static GemDef Curse(string id, double damageTaken = 0.2) =>
            new GemDef(id, GemKind.Skill, GemColor.Blue, id, id, StatTags.Skill,
                skill: new SkillDef(0, 1, 10, new EnemyDebuffEffect(damageTaken), new[] { "curse" }));

        private static (GameLoop Loop, FakeInputSource Input) Voodoo(params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Voodoo }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.Events.Clear();
            return (loop, input);
        }

        private static double Ticks(GameLoop loop, int count)
        {
            for (var i = 0; i < count; i++)
                loop.Tick();
            return loop.State.Resource;
        }

        private static double DamageMore(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 1);

        private static int CastNew(GameLoop loop, GemDef gem)
        {
            var index = loop.Skills.Equip(gem);
            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast), gem.Id);
            return index;
        }

        private static void KillEnemy(GameLoop loop, FakeInputSource input)
        {
            loop.State.EnemyHp = BigNumber.FromDouble(1e-9);
            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
        }

        [Test]
        public void BlightGrowsByItselfAndFromKeys()
        {
            var (loop, input) = Voodoo();

            Assert.That(Ticks(loop, 10), Is.EqualTo(3).Within(1e-9));

            input.PressKeys(5);
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(3 + 0.3 + 1).Within(1e-9));
        }

        [Test]
        public void MouseGivesNoBlight()
        {
            var (loop, input) = Voodoo();

            input.MoveMouse(100000);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void CurseEndsWhenItsEnemyDies()
        {
            var (loop, input) = Voodoo();
            var slot = CastNew(loop, Curse("hex"));
            Assert.That(DamageMore(loop), Is.EqualTo(1.2).Within(1e-9));

            KillEnemy(loop, input);

            Assert.That(loop.State.Skills[slot].Active, Is.False);
            Assert.That(DamageMore(loop), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void OnlyOneCurseHoldsAndTheNewOneReplacesTheOldest()
        {
            var (loop, _) = Voodoo();
            var first = CastNew(loop, Curse("hex_a"));
            var second = CastNew(loop, Curse("hex_b", 0.3));

            Assert.That(loop.State.Skills[first].Active, Is.False);
            Assert.That(loop.State.Skills[second].Active, Is.True);
            Assert.That(loop.Skills.ActiveCurses, Is.EqualTo(Rules.MaxOnEnemy));
            Assert.That(DamageMore(loop), Is.EqualTo(1.3).Within(1e-9));
        }

        [Test]
        public void ExtraCurseSlotKeepsBothCurses()
        {
            var (loop, _) = Voodoo(new Modifier(CombatStats.CursesOnEnemy, ModifierKind.Flat, 1));
            CastNew(loop, Curse("hex_a"));
            CastNew(loop, Curse("hex_b"));

            Assert.That(loop.Skills.ActiveCurses, Is.EqualTo(2));
            Assert.That(DamageMore(loop), Is.EqualTo(1.2 * 1.2).Within(1e-9));
        }

        [Test]
        public void CurseEffectAndEnemyDebuffEffectMakeTheCurseStronger()
        {
            var (loop, _) = Voodoo(
                new Modifier(CombatStats.CurseEffect, ModifierKind.Increased, 0.5),
                new Modifier(CombatStats.EnemyDebuffEffect, ModifierKind.Increased, 0.5));

            CastNew(loop, Curse("hex"));

            Assert.That(DamageMore(loop), Is.EqualTo(1 + 0.2 * 1.5 * 1.5).Within(1e-9));
        }

        [Test]
        public void CurseSecondsStretchTheCurse()
        {
            var (loop, _) = Voodoo(new Modifier(CombatStats.CurseSeconds, ModifierKind.Increased, 0.5));

            var slot = CastNew(loop, Curse("hex"));

            Assert.That(loop.State.Skills[slot].ActiveTicks, Is.EqualTo(150));
        }

        [Test]
        public void FlicksStrengthenTheNextCurseUpToTheCap()
        {
            var (loop, input) = Voodoo();
            input.Flick(2);
            loop.Tick();

            CastNew(loop, Curse("hex_a"));
            Assert.That(DamageMore(loop), Is.EqualTo(1 + 0.2 * (1 + 2 * Rules.FlickBonus)).Within(1e-9));
            Assert.That(loop.State.CurseFlicks, Is.EqualTo(0));

            input.Flick(100);
            loop.Tick();
            CastNew(loop, Curse("hex_b"));
            Assert.That(DamageMore(loop), Is.EqualTo(1 + 0.2 * (1 + Rules.FlickMax)).Within(1e-9));
        }

        [Test]
        public void FermentedCurseKeepsCursesThroughKillsAndHitsEverySecond()
        {
            var (loop, input) = Voodoo();
            var hex = CastNew(loop, Curse("hex"));
            loop.State.Resource = 100;
            CastNew(loop, TestWorld.Data.Gems["fermented_curse"]);

            KillEnemy(loop, input);
            Assert.That(loop.State.Skills[hex].Active, Is.True);

            loop.Events.Clear();
            Ticks(loop, GameLoop.TicksPerSecond);
            var hits = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    hits++;
            }
            Assert.That(hits, Is.EqualTo(1));
        }
    }
}
