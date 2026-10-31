using Game.Core.Content;
using Game.Core.Content.Slates;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonTreeMechanicsTests
    {
        private static Modifier Flat(StatId stat, double value, ModifierCondition when = ModifierCondition.Always) =>
            new Modifier(stat, ModifierKind.Flat, value, StatTags.None, ModifierPool.General, when);

        private static GameLoop Hero(FakeInputSource input, EnemyType enemy, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Viking }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
            return loop;
        }

        private static GameLoop Hero(EnemyType enemy, params Modifier[] modifiers) => Hero(new FakeInputSource(), enemy, modifiers);

        private static bool Holds(GameLoop loop, ModifierCondition condition) => (loop.Hero.Conditions & ModifierConditions.Bit(condition)) != 0;

        private static void Ticks(GameLoop loop, double seconds)
        {
            for (var tick = 0; tick < System.Math.Round(seconds * GameLoop.TicksPerSecond); tick++)
                loop.Tick();
        }

        [Test]
        public void ABossLowOnHpAndAtTheEndOfItsTimerTurnsItsConditionsOn()
        {
            var boss = Hero(EnemyType.Boss);
            Assert.That(Holds(boss, ModifierCondition.BossLowHp) || Holds(boss, ModifierCondition.BossTimerEnd), Is.False);

            boss.State.EnemyHp = boss.State.EnemyMaxHp * 0.2;
            boss.State.BossTicksLeft = 5;
            boss.Tick();

            Assert.That(Holds(boss, ModifierCondition.BossLowHp) && Holds(boss, ModifierCondition.BossTimerEnd), Is.True);
        }

        [Test]
        public void MouseGesturesHoldTheirConditionForAFewSeconds()
        {
            var input = new FakeInputSource();
            var hero = Hero(input, EnemyType.Normal);
            input.Circle(1);
            input.Flick();
            input.MoveMouse(200);
            hero.Tick();

            Assert.That(Holds(hero, ModifierCondition.AfterCircle) && Holds(hero, ModifierCondition.AfterFlick) && Holds(hero, ModifierCondition.MouseMoving), Is.True);
            Ticks(hero, TestWorld.Data.Combat.Conditions.AfterGestureSeconds + 0.2);
            Assert.That(Holds(hero, ModifierCondition.AfterCircle) || Holds(hero, ModifierCondition.AfterFlick) || Holds(hero, ModifierCondition.MouseMoving), Is.False);
        }

        [Test]
        public void AFastKillAndAPartyTurnTheirConditionsOn()
        {
            var hero = Hero(EnemyType.Normal);
            Assert.That(Holds(hero, ModifierCondition.AfterFastKill) || Holds(hero, ModifierCondition.InParty), Is.False);

            hero.State.AfterFastKillTicks = 10;
            hero.State.PartyAllies = 1;
            hero.Tick();

            Assert.That(Holds(hero, ModifierCondition.AfterFastKill) && Holds(hero, ModifierCondition.InParty), Is.True);
        }

        [Test]
        public void FullFocusTurnsItsConditionOnAndTheIdleFloorKeepsHalf()
        {
            var hero = Hero(EnemyType.Normal, Flat(CombatStats.FocusIdleFloor, 0.5));
            hero.State.Focus = hero.Focus.Max;
            hero.Tick();
            Assert.That(Holds(hero, ModifierCondition.FocusFull), Is.True);

            Ticks(hero, hero.Focus.ResetAfterIdleSeconds + 1);

            Assert.That(Holds(hero, ModifierCondition.FocusFull), Is.False);
            Assert.That(hero.State.Focus, Is.EqualTo(hero.Focus.Max * 0.5).Within(1e-9));
        }

        [Test]
        public void TheFirstHitOnANewEnemyCrits()
        {
            var input = new FakeInputSource();
            var hero = Hero(input, EnemyType.Normal, Flat(CombatStats.FirstHitCrit, 1), Flat(CombatStats.CritChance, -1));
            var crits = hero.State.Crits;

            input.PressKeys(1);
            hero.Tick();
            input.PressKeys(1);
            hero.Tick();

            Assert.That(hero.State.Crits - crits, Is.EqualTo(1));
        }

        [Test]
        public void TheFirstHitAfterALullCrits()
        {
            var input = new FakeInputSource();
            var hero = Hero(input, EnemyType.Normal, Flat(CombatStats.PauseCrit, 1), Flat(CombatStats.CritChance, -1));
            Ticks(hero, TestWorld.Data.Combat.Conditions.AfterIdlePauseSeconds + 1);
            var crits = hero.State.Crits;

            input.PressKeys(1);
            hero.Tick();
            input.PressKeys(1);
            hero.Tick();

            Assert.That(hero.State.Crits - crits, Is.EqualTo(1));
        }

        [Test]
        public void EveryNthCritCountsTowardTheBurst()
        {
            var input = new FakeInputSource();
            var hero = Hero(input, EnemyType.Normal, Flat(CombatStats.CritChance, 1), Flat(CombatStats.CritBurstEvery, 2), Flat(CombatStats.CritBurstMultiplier, 2));

            input.PressKeys(3);
            hero.Tick();

            Assert.That(hero.State.CritsTowardBurst, Is.EqualTo(3));
        }

        [Test]
        public void AShortPauseKeepsTheLongStreakWithTheStat()
        {
            var pause = TestWorld.Data.Combat.Conditions.LongStreakPauseSeconds + 0.5;
            var input = new FakeInputSource();
            var kept = Hero(input, EnemyType.Normal, Flat(CombatStats.LongStreakPauseSeconds, 1));
            var plainInput = new FakeInputSource();
            var plain = Hero(plainInput, EnemyType.Normal);
            input.PressKeys(1);
            plainInput.PressKeys(1);
            kept.Tick();
            plain.Tick();

            Ticks(kept, pause);
            Ticks(plain, pause);

            Assert.That(kept.State.InputStreakTicks, Is.GreaterThan(0));
            Assert.That(plain.State.InputStreakTicks, Is.Zero);
        }

        [Test]
        public void MouseFocusAndMouseQueueShareFollowTheMouse()
        {
            var input = new FakeInputSource();
            var hero = Hero(input, EnemyType.Normal, new Modifier(CombatStats.FocusFromMouse, ModifierKind.Increased, 1), Flat(CombatStats.MouseQueueShare, 0.5));
            var plainInput = new FakeInputSource();
            var plain = Hero(plainInput, EnemyType.Normal);
            var focus = hero.State.Focus;

            input.MoveMouse(5000);
            plainInput.MoveMouse(5000);
            hero.Tick();
            plain.Tick();

            Assert.That(hero.State.Focus - focus, Is.EqualTo(2 * (plain.State.Focus - focus)).Within(1e-6));
            Assert.That(hero.State.QueueDamage[0].Sign, Is.GreaterThan(0));
            Assert.That(plain.State.QueueDamage[0].Sign, Is.Zero);
        }

        [Test]
        public void DustGainRaisesTheDustFromADismantledSlate()
        {
            var hero = Hero(EnemyType.Normal, new Modifier(CombatStats.DustGain, ModifierKind.Increased, 0.5));
            var rules = TestWorld.Data.Slates;
            var theme = rules.Themes[0];
            var slate = new SlateItem(rules.Shapes[0], theme, SlateRarity.Epic, new[] { theme.Lines[0] }, false);
            hero.Slates.Give(slate);

            hero.Slates.Dismantle(slate);

            Assert.That(hero.State.Drops.Dust, Is.EqualTo(rules.Rarity(SlateRarity.Epic).Dust * 1.5).Within(1e-9));
        }

        [Test]
        public void TheNewConditionsHaveTheirText()
        {
            var text = ModifierText.Describe(new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.1, StatTags.None, ModifierPool.General, ModifierCondition.AfterCircle));

            Assert.That(text, Does.EndWith("сразу после круга мышью"));
        }
    }
}
