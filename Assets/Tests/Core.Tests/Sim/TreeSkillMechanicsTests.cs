using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class TreeSkillMechanicsTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers) =>
            Hero(heroClass, true, modifiers);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, bool withoutCrits, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            if (withoutCrits)
                hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.Book.GrantStartingGems();
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.HitsOnEnemy = TestWorld.Data.Combat.Conditions.FirstHits;
            return (loop, input);
        }

        private static int SlotOf(GameLoop loop, string gemId)
        {
            var index = loop.State.Skills.FindIndex(slot => slot.Skill.Id == gemId);
            if (index >= 0)
                return index;
            var gem = TestWorld.Data.Gems[gemId];
            if (!loop.State.OwnedGems.Contains(gem))
                loop.State.OwnedGems.Add(gem);
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(Game.Core.Build.BookResult.Done), gemId);
            return loop.State.Skills.FindIndex(slot => slot.Skill.Id == gemId);
        }

        private static double CastDamage(GameLoop loop, string gemId)
        {
            var index = SlotOf(loop, gemId);
            loop.State.Resource = 100;
            loop.Events.Clear();
            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast), gemId);
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        private static Modifier ForSkill(StatId stat, string gemId, ModifierKind kind, double value) =>
            new Modifier(StatId.ForSkill(stat, gemId), kind, value);

        [Test]
        public void SkillPowerOfOneSkillMakesOnlyThatSkillStronger()
        {
            var power = ForSkill(CombatStats.SkillPower, "iron_rim_bash", ModifierKind.Increased, 0.5);
            var (plain, _) = Hero(HeroClass.Viking);
            var (strong, _) = Hero(HeroClass.Viking, power);

            Assert.That(CastDamage(strong, "iron_rim_bash") / CastDamage(plain, "iron_rim_bash"), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(CastDamage(strong, "no_mercy") / CastDamage(plain, "no_mercy"), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void SkillPowerOfAnAuraMakesItsEffectStronger()
        {
            var gem = TestWorld.Data.Gems["hearth_song"];
            var (plain, _) = Hero(HeroClass.Metalhead);
            var (strong, _) = Hero(HeroClass.Metalhead, ForSkill(CombatStats.SkillPower, gem.Id, ModifierKind.Increased, 0.5));

            CastDamage(plain, gem.Id);
            CastDamage(strong, gem.Id);

            var weak = plain.State.Skills[SlotOf(plain, gem.Id)].AuraModifiers.Sum(modifier => modifier.Value);
            var boosted = strong.State.Skills[SlotOf(strong, gem.Id)].AuraModifiers.Sum(modifier => modifier.Value);
            Assert.That(weak, Is.GreaterThan(0));
            Assert.That(boosted / weak, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void SkillDurationOfOneSkillAddsSecondsToItsEffect()
        {
            var gem = TestWorld.Data.Gems["hearth_song"];
            var (plain, _) = Hero(HeroClass.Metalhead);
            var (longer, _) = Hero(HeroClass.Metalhead, ForSkill(CombatStats.SkillDurationSeconds, gem.Id, ModifierKind.Flat, 2));

            CastDamage(plain, gem.Id);
            CastDamage(longer, gem.Id);

            var plainTicks = plain.State.Skills[SlotOf(plain, gem.Id)].ActiveTicks;
            var longerTicks = longer.State.Skills[SlotOf(longer, gem.Id)].ActiveTicks;
            Assert.That(longerTicks - plainTicks, Is.EqualTo(2 * GameLoop.TicksPerSecond));
        }

        [Test]
        public void AFastSkillGivesResource()
        {
            var (loop, _) = Hero(HeroClass.Viking, new Modifier(CombatStats.ResourcePerFastSkill, ModifierKind.Flat, 5));
            var index = SlotOf(loop, "iron_rim_bash");
            loop.State.Resource = 50;
            var cost = TestWorld.Data.Gems["iron_rim_bash"].Skill!.ResourceCost;

            Assert.That(loop.Skills.TryCast(index), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.State.Resource, Is.EqualTo(50 - cost + 5).Within(1e-9));
        }

        [Test]
        public void AHeavySkillGivesNoResourceForFastSkills()
        {
            var (plain, _) = Hero(HeroClass.Viking);
            var (loop, _) = Hero(HeroClass.Viking, new Modifier(CombatStats.ResourcePerFastSkill, ModifierKind.Flat, 5));

            CastDamage(plain, "no_mercy");
            CastDamage(loop, "no_mercy");

            Assert.That(loop.State.Resource, Is.EqualTo(plain.State.Resource).Within(1e-9));
        }

        [Test]
        public void AKeyCritGivesResource()
        {
            var (loop, input) = Hero(HeroClass.Ronin, false,
                new Modifier(CombatStats.CritChance, ModifierKind.Flat, 10),
                new Modifier(CombatStats.ResourcePerCrit, ModifierKind.Flat, 2, StatTags.Keyboard));

            input.PressKeys(1);
            loop.Tick();
            loop.Tick();

            Assert.That(loop.State.Crits, Is.GreaterThan(0));
            Assert.That(loop.State.Resource, Is.EqualTo(2 * loop.State.Crits).Within(1e-9));
        }

        [Test]
        public void CirclesAndFlicksGiveResource()
        {
            var (loop, input) = Hero(HeroClass.Ronin,
                new Modifier(CombatStats.ResourcePerCircle, ModifierKind.Flat, 3, StatTags.Mouse),
                new Modifier(CombatStats.ResourcePerFlick, ModifierKind.Flat, 2, StatTags.Mouse));

            loop.State.Resource = 0;
            input.Circle(1);
            input.Flick(2);
            loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(3 + 2 * 2).Within(1e-9));
        }

        [Test]
        public void ATreeLineNamesAKnownSkill()
        {
            var tree = TestWorld.Real.ClassTreeOf("ronin")!;
            var stats = tree.Nodes.SelectMany(node => node.Template.First.Modifiers.Concat(node.Template.Second.Modifiers).Concat(node.Template.Third.Modifiers))
                .Select(modifier => modifier.Stat).Where(stat => stat.IsForSkill).ToList();

            Assert.That(stats.Select(stat => stat.Name), Does.Contain("skillPower:thin_cut"));
            Assert.That(stats.All(stat => TestWorld.Real.Gems.TryGet(stat.SkillId, out _)), Is.True);
        }
    }
}
