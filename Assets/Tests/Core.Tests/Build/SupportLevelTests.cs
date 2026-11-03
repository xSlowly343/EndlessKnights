using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SupportLevelTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];
        private static GemLevels Levels => TestWorld.Data.Gems.Levels;

        private static (GameLoop Loop, int Slot) Supported(string support, int level, params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), TestWorld.WithAllSkillsOpen(new GameState { Class = HeroClass.Runedancer }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            var slot = loop.Skills.Equip(Gem("runedancer_hit_1"), role: SkillSlotRole.Left);
            Assert.That(loop.Book.SetSupport(slot, 0, Gem(support)), Is.EqualTo(BookResult.Done));
            loop.State.ProgressOf(Gem(support)).Level = level;
            return (loop, slot);
        }

        private static double CastPower(GameLoop loop, int slot)
        {
            loop.State.Resource = 100;
            loop.State.Skills[slot].CooldownTicks = 0;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            return loop.State.Skills[slot].CastPower;
        }

        [Test]
        public void ASupportGrowsTwoPercentOfItsBonusPerLevel()
        {
            var (low, lowSlot) = Supported("acceleration", 1);
            var (high, highSlot) = Supported("acceleration", 11);
            low.State.FocusMultiplier = 2;
            high.State.FocusMultiplier = 2;

            Assert.That(CastPower(low, lowSlot), Is.EqualTo(1.16).Within(1e-9));
            Assert.That(CastPower(high, highSlot), Is.EqualTo(1 + 0.16 * Levels.EffectMultiplier(11)).Within(1e-9));
            Assert.That(high.State.Skills[highSlot].SupportScale[0], Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void TheLevelGrowsTheBonusButNotThePrice()
        {
            var (loop, slot) = Supported("pawnbroker", 21);

            Assert.That(CastPower(loop, slot), Is.EqualTo(0.7).Within(1e-9), "the price stays");
            Assert.That(SupportEffects.KillReward(loop.State.Skills[slot], loop.State), Is.EqualTo(1 + 0.4 * 1.4).Within(1e-9));
        }

        [Test]
        public void TheHelmetRaisesSupportsAboveTheCap()
        {
            var (loop, slot) = Supported("acceleration", Levels.Max, new Modifier(CombatStats.SupportLevel, ModifierKind.Flat, 3));
            loop.State.FocusMultiplier = 2;

            CastPower(loop, slot);

            Assert.That(loop.State.Skills[slot].SupportScale[0], Is.EqualTo(Levels.EffectMultiplier(Levels.Max, 3)).Within(1e-9));
            Assert.That(loop.State.Skills[slot].SupportScale[0], Is.GreaterThan(Levels.EffectMultiplier(Levels.Max)));
        }

        [Test]
        public void TheHelmetHasASupportLevelAffix()
        {
            Assert.That(TestWorld.Data.Items.TryGetAffix("helmet_support_level", out var affix), Is.True);
            Assert.That(affix.Stats.Single().Stat, Is.EqualTo(CombatStats.SupportLevel));
            Assert.That(affix.FromRarity, Is.EqualTo(ItemRarity.Rare));
        }

        [Test]
        public void EverySlotCanRollMaxResourceAndResourceGain()
        {
            var items = TestWorld.Data.Items;
            foreach (var slot in new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots, ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Weapon })
            {
                var pool = items.Affixes.Where(affix => affix.FitsSlot(slot)).ToList();
                Assert.That(pool.Any(affix => affix.Group == "resource_max"), Is.True, slot + " max");
                Assert.That(pool.Any(affix => affix.Group == "resource_gain"), Is.True, slot + " gain");
            }
            foreach (var slot in new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Gloves, ItemSlot.Boots })
                Assert.That(items.ImplicitsOf(slot).Any(rolled => rolled.Lines.Any(line => line.Stat == CombatStats.ResourceMax || line.Stat == CombatStats.ResourceGain)), Is.True, slot.ToString());
        }

        [Test]
        public void OneItemNeverRollsTwoResourceMaxAffixes()
        {
            var rng = new Rng(13);
            for (var i = 0; i < 300; i++)
            {
                var helmet = TestWorld.Data.Items.Roll(ItemSlot.Helmet, "red", ItemRarity.Rift, 6, rng);
                Assert.That(helmet.Affixes.Count(affix => affix.Def.Group == "resource_max"), Is.LessThanOrEqualTo(1));
                Assert.That(helmet.Affixes.Count(affix => affix.Def.Group == "resource_gain"), Is.LessThanOrEqualTo(1));
            }
        }
    }
}
