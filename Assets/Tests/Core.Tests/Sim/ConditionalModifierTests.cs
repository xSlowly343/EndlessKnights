using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ConditionalModifierTests
    {
        private static readonly StatId Damage = new StatId("damage");

        private static GameLoop Hero(HeroClass heroClass, EnemyType enemy, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState { Class = heroClass }, hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, enemy, BigNumber.Create(1, 50));
            loop.State.BossTicksLeft = 100_000;
            loop.Tick();
            return loop;
        }

        private static bool Holds(GameLoop loop, ModifierCondition condition) => (loop.Hero.Conditions & ModifierConditions.Bit(condition)) != 0;

        [Test]
        public void AConditionalModifierCountsOnlyWhileItsConditionHolds()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.1, StatTags.None, ModifierPool.General, ModifierCondition.Curse));

            Assert.That(sheet.Value(Damage, 1), Is.EqualTo(1));
            sheet.SetConditions(ModifierConditions.Bit(ModifierCondition.Curse));
            Assert.That(sheet.Value(Damage, 1), Is.EqualTo(1.1).Within(1e-9));
            sheet.SetConditions(0);
            Assert.That(sheet.Value(Damage, 1), Is.EqualTo(1));
        }

        [Test]
        public void AConditionNoModifierUsesKeepsTheCache()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(Damage, ModifierKind.Increased, 0.1, StatTags.None, ModifierPool.General, ModifierCondition.Curse));
            sheet.Value(Damage, 1);
            var recalculations = sheet.RecalculationCount;

            sheet.SetConditions(ModifierConditions.Bit(ModifierCondition.Boss));
            sheet.Value(Damage, 1);

            Assert.That(sheet.RecalculationCount, Is.EqualTo(recalculations));
        }

        [Test]
        public void ScalingAModifierKeepsItsCondition()
        {
            var modifier = new Modifier(Damage, ModifierKind.Increased, 0.1, StatTags.Keyboard, ModifierPool.General, ModifierCondition.Blind);

            Assert.That(modifier.WithValue(0.3).When, Is.EqualTo(ModifierCondition.Blind));
            Assert.That(modifier.InPool(ModifierPool.Tree).When, Is.EqualTo(ModifierCondition.Blind));
            Assert.That(modifier.WithValue(0.1), Is.EqualTo(modifier));
            Assert.That(modifier, Is.Not.EqualTo(new Modifier(Damage, ModifierKind.Increased, 0.1, StatTags.Keyboard)));
        }

        [Test]
        public void ContentReadsTheConditionAndRejectsUnknownOnes()
        {
            var read = ModifierReader.Read(ContentNode.Root("{\"stat\":\"gold\",\"kind\":\"increased\",\"value\":0.1,\"when\":\"hunterMark\"}", "test.json"));

            Assert.That(read.When, Is.EqualTo(ModifierCondition.HunterMark));
            Assert.Throws<ContentException>(() => ModifierReader.Read(ContentNode.Root("{\"stat\":\"gold\",\"kind\":\"flat\",\"value\":1,\"when\":\"moonlight\"}", "test.json")));
            Assert.Throws<ContentException>(() => ModifierReader.Read(ContentNode.Root("{\"stat\":\"gold\",\"kind\":\"flat\",\"value\":1,\"when\":\"always\"}", "test.json")));
        }

        [Test]
        public void TheBossOpeningLastsTheFirstSecondsOfABossFight()
        {
            var seconds = TestWorld.Data.Combat.Conditions.BossOpeningSeconds;
            var boss = Hero(HeroClass.Viking, EnemyType.Boss);

            Assert.That(Holds(boss, ModifierCondition.Boss) && Holds(boss, ModifierCondition.BossOpening), Is.True);
            for (var tick = 0; tick < seconds * GameLoop.TicksPerSecond; tick++)
                boss.Tick();
            Assert.That(Holds(boss, ModifierCondition.Boss), Is.True);
            Assert.That(Holds(boss, ModifierCondition.BossOpening), Is.False);
            Assert.That(Holds(Hero(HeroClass.Viking, EnemyType.Guardian), ModifierCondition.Boss), Is.False);
        }

        [Test]
        public void ResourceConditionsFollowTheFill()
        {
            var hero = Hero(HeroClass.Viking, EnemyType.Normal);
            hero.State.Resource = 0.6 * hero.Resource.Max;
            hero.Tick();

            Assert.That(Holds(hero, ModifierCondition.ResourceAbove30) && Holds(hero, ModifierCondition.ResourceAbove50), Is.True);
            Assert.That(Holds(hero, ModifierCondition.ResourceAbove80) || Holds(hero, ModifierCondition.ResourceFull), Is.False);
            hero.State.Resource = hero.Resource.Max * 2;
            hero.Tick();
            Assert.That(Holds(hero, ModifierCondition.ResourceAbove80) && Holds(hero, ModifierCondition.ResourceFull), Is.True);
        }

        [Test]
        public void AMarkOnTheEnemyTurnsItsConditionOn()
        {
            var huntress = Hero(HeroClass.Amazon, EnemyType.Normal, new Modifier(new StatId("gold"), ModifierKind.Increased, 0.1, StatTags.Gold, ModifierPool.General, ModifierCondition.HunterMark));
            var plain = huntress.Hero.Value(new StatId("gold"), 1, StatTags.Gold);

            huntress.State.HunterMarkTicks = 100;
            huntress.Tick();

            Assert.That(Holds(huntress, ModifierCondition.HunterMark), Is.True);
            Assert.That(huntress.Hero.Value(new StatId("gold"), 1, StatTags.Gold) / plain, Is.EqualTo(1.1).Within(1e-9));
        }

        [Test]
        public void TheLineTextNamesItsCondition()
        {
            var text = ModifierText.Describe(new Modifier(new StatId("gold"), ModifierKind.Increased, 0.1, StatTags.None, ModifierPool.General, ModifierCondition.Curse));

            Assert.That(text, Does.EndWith("по проклятому врагу"));
        }
    }
}
