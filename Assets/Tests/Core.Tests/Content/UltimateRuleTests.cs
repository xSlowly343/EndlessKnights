using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Items;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class UltimateRuleTests
    {
        private static ItemCatalog Catalog => TestWorld.Data.Items;

        private static System.Collections.Generic.List<Modifier> LinesOf(string id, int tier = 6)
        {
            Assert.That(Catalog.TryGetAffix(id, out var def), Is.True, id);
            var lines = new System.Collections.Generic.List<Modifier>();
            new GearAffix(def, tier, def.ValueAt(tier, Catalog.Tiers)).AddModifiers(lines);
            return lines;
        }

        [TestCase("ring_deep_gauge", "resourceMax", "skillCost")]
        [TestCase("ring_double_hit", "doubleHitChance", "damageVsBoss")]
        [TestCase("chest_long_series", "longStreakPauseSeconds", "damage")]
        [TestCase("jewel_full_gauge", "damage", "resourceGain")]
        [TestCase("boots_moving_mouse", "damage", "damage")]
        [TestCase("amulet_extra_aura", "extraAuraSlots", "auraEffect")]
        public void ARuleGivesABonusAndAPrice(string id, string bonus, string price)
        {
            var lines = LinesOf(id);

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Stat.Name, Is.EqualTo(bonus));
            Assert.That(lines[1].Stat.Name, Is.EqualTo(price));
            Assert.That(Catalog.Affixes.Single(affix => affix.Id == id).Group, Is.EqualTo("ultimate_rule"));
        }

        [Test]
        public void TheLongSeriesRuleCostsDamageOnlyOutsideALongStreak()
        {
            var lines = LinesOf("chest_long_series");
            var sheet = new StatSheet();
            sheet.AddRange(lines);

            sheet.SetConditions(ModifierConditions.Bit(ModifierCondition.ShortStreak));
            var outside = sheet.Value(CombatStats.Damage, 100);
            sheet.SetConditions(ModifierConditions.Bit(ModifierCondition.LongStreak));
            var inside = sheet.Value(CombatStats.Damage, 100);

            Assert.That(outside, Is.EqualTo(95).Within(1e-9));
            Assert.That(inside, Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void ShortStreakIsSetWheneverTheLongStreakIsNot()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);

            loop.Tick();
            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.ShortStreak), Is.Not.Zero);
            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.LongStreak), Is.Zero);
        }

        [Test]
        public void TheExtraAuraRuleOpensAFifthAuraSlot()
        {
            var hero = new StatSheet();
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 100 }, hero: hero);
            Assert.That(loop.Book.AuraSlotCount, Is.EqualTo(SkillLoadout.LeveledAuraSlots));

            hero.AddRange(LinesOf("amulet_extra_aura"));

            Assert.That(loop.Book.AuraSlotCount, Is.EqualTo(SkillLoadout.AuraSlots));
            Assert.That(loop.Book.Capacity(SkillSlotRole.Aura), Is.EqualTo(5));
            Assert.That(hero.Value(CombatStats.AuraEffect, 1), Is.EqualTo(0.92).Within(1e-9));
        }

        [Test]
        public void TheLoadoutPacketCarriesNinePlaces()
        {
            var message = new Game.Core.Party.Wire.PartyLoadoutMessage();
            message.Set(8, new Game.Core.Party.Wire.LoadoutSkill { GemNetId = 7, Level = 3 });

            Assert.That(Game.Core.Party.Wire.PartyLoadoutMessage.PositionOf(SkillSlotRole.Aura, 4), Is.EqualTo(8));
            Assert.That(message.At(8).GemNetId, Is.EqualTo(7));
            Assert.That(SkillLoadout.MaxSlots, Is.EqualTo(9));
        }
    }
}
