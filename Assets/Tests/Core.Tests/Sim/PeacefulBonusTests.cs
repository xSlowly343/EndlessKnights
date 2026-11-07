using System.Linq;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PeacefulBonusTests
    {
        private static GameLoop With(params Modifier[] modifiers)
        {
            var hero = new StatSheet();
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1), hero);
            loop.Tick();
            return loop;
        }

        [Test]
        public void EachCharacteristicIsItsOwnBasketAndThePartyMultipliesAllFour()
        {
            var loop = With(
                new Modifier(CombatStats.PeacefulSpeed, ModifierKind.Increased, 0.1),
                new Modifier(CombatStats.PeacefulSpeed, ModifierKind.Increased, 0.1),
                new Modifier(CombatStats.PeacefulYield, ModifierKind.Increased, 0.2),
                new Modifier(CombatStats.PeacefulYield, ModifierKind.More, 0.5),
                new Modifier(CombatStats.PeacefulLuck, ModifierKind.Flat, 0.3),
                new Modifier(CombatStats.PeacefulMastery, ModifierKind.Increased, 0.04),
                new Modifier(CombatStats.PartyPeaceful, ModifierKind.Increased, 0.1));
            var bonus = loop.State.PeacefulBonus;
            Assert.That(bonus.Speed, Is.EqualTo(0.2 * 1.1).Within(1e-9));
            Assert.That(bonus.Yield, Is.EqualTo((1.2 * 1.5 - 1) * 1.1).Within(1e-9));
            Assert.That(bonus.Luck, Is.EqualTo(0.3 * 1.1).Within(1e-9));
            Assert.That(bonus.Mastery, Is.EqualTo(0.04 * 1.1).Within(1e-9));
        }

        [Test]
        public void NoBonusLeavesEveryActivityAsItWas()
        {
            var bonus = With().State.PeacefulBonus;
            Assert.That(bonus.TimeFactor, Is.EqualTo(1));
            Assert.That(bonus.YieldFactor, Is.EqualTo(1));
            Assert.That(bonus.LuckFactor, Is.EqualTo(1));
            Assert.That(bonus.More(7), Is.EqualTo(7));
            Assert.That(bonus.RollMastery(new Rng(1)), Is.False);
        }

        [Test]
        public void KnackShortensTripsDigsAndChopping()
        {
            var plain = With();
            var quick = With(new Modifier(CombatStats.PeacefulSpeed, ModifierKind.Increased, 0.25));
            Assert.That(quick.Expeditions.DurationFactor("camp_dog"), Is.EqualTo(plain.Expeditions.DurationFactor("camp_dog") / 1.25).Within(1e-9));
            Assert.That(quick.Digs.BrushNeeded, Is.EqualTo(plain.Digs.BrushNeeded / 1.25).Within(1e-9));
            Assert.That(quick.Forge.HammersNeeded, Is.LessThan(plain.Forge.HammersNeeded));
        }

        [Test]
        public void BountyRaisesTheHarvest()
        {
            var loop = With(new Modifier(CombatStats.PeacefulYield, ModifierKind.Increased, 0.5));
            var plant = TestWorld.Data.Garden.Plant("knockbloom")!;
            loop.Garden.Plant(0, "knockbloom");
            loop.Garden.Advance(plant.GrowSeconds);
            var before = loop.Camp.Count(plant.Herb);
            loop.Garden.Harvest(0);
            Assert.That(loop.Camp.Count(plant.Herb) - before, Is.EqualTo((long)System.Math.Floor(plant.Yield * 1.5 + 1e-9)));
        }

        [Test]
        public void FullMasteryAlwaysRaisesTheQuality()
        {
            var bonus = With(new Modifier(CombatStats.PeacefulMastery, ModifierKind.Flat, 2)).State.PeacefulBonus;
            Assert.That(bonus.MasteryChance, Is.EqualTo(1));
            Assert.That(bonus.RollMastery(new Rng(1)), Is.True);
        }

        [Test]
        public void TheHeroSheetShowsBeforeAndAfter()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
            var loop = With(new Modifier(CombatStats.PeacefulSpeed, ModifierKind.Increased, 0.2));
            var lines = HeroStatsView.Lines(loop, NumberFormatSettings.Default);
            var knack = lines.First(line => line.Label == "Сноровка");
            Assert.That(knack.Value, Does.StartWith("+20%: поклёвка "));
            Assert.That(knack.Value, Does.Contain(" → "));
            Assert.That(HeroStatsView.Clock(240), Is.EqualTo("4:00"));
            Assert.That(HeroStatsView.Clock(200), Is.EqualTo("3:20"));
        }
    }
}
