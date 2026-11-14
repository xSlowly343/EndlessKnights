using System.Collections.Generic;
using System.Linq;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyBannerTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyBannerRules Rules => TestWorld.Data.Party.Growth.Banner;

        private static GameLoop Member()
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            return loop;
        }

        [Test]
        public void TwentySevenCrestsFromTrialsAndTheColossus()
        {
            Assert.That(Rules.Crests.Count, Is.EqualTo(27));
            Assert.That(Rules.Crest("choir_10")!.Modifier.Value, Is.EqualTo(0.08));
            Assert.That(Rules.Crest("choir_20")!.Modifier.Value, Is.EqualTo(0.16));
            Assert.That(Rules.Crest("marathon_15")!.Modifier.Stat, Is.EqualTo(CombatStats.PartyPeaceful));
            Assert.That(Rules.Crest("marathon_15")!.Modifier.Value, Is.EqualTo(0.14));
            Assert.That(Rules.Crest("colossus_100")!.FromColossus, Is.True);
            var growth = TestWorld.Data.Party.Growth;
            Assert.That(Rules.Slots(growth, 39), Is.Zero);
            Assert.That(Rules.Slots(growth, 40), Is.EqualTo(1));
            Assert.That(Rules.Slots(growth, 85), Is.EqualTo(5));
            Assert.That(Rules.Slots(growth, 100), Is.EqualTo(6));
            var best = new Dictionary<string, int> { ["choir"] = 15 };
            Assert.That(Rules.Earned(Rules.Crest("choir_15")!, best, 0), Is.True);
            Assert.That(Rules.Earned(Rules.Crest("choir_20")!, best, 0), Is.False);
            Assert.That(Rules.Earned(Rules.Crest("colossus_50")!, best, 50), Is.True);
        }

        [Test]
        public void PlacedCrestsGiveTheirPowerInThePartyBasket()
        {
            var loop = Member();
            var damage = loop.Hero.Value(CombatStats.Damage, 1);
            var boss = loop.Hero.Value(new StatId("damageVsBoss"), 1);
            loop.SetPartyBanner(new[] { "choir_20", "pulse_10", "marathon_10", "", "choir_20" });
            Assert.That(loop.PartyGrowth.Crests.Count, Is.EqualTo(3), "empty places and doubles are skipped");
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / damage, Is.EqualTo(1.16).Within(1e-9));
            Assert.That(loop.Hero.Value(new StatId("damageVsBoss"), 1) - boss, Is.EqualTo(0.08 * boss).Within(1e-9));
            Assert.That(loop.PartyGrowth.Peaceful, Is.EqualTo(0.1).Within(1e-12));
        }

        [Test]
        public void ShieldAndBannerAddsFivePercentOnlyWithACrestOnTheBanner()
        {
            var loop = Member();
            var damage = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyTree(new Dictionary<int, int> { [48] = 5 }, new string[0]);
            var withTree = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyBanner(new[] { "relay_10" });
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / withTree, Is.EqualTo(1.05).Within(1e-9));
            Assert.That(loop.Hero.Value(PartyGrowthLayer.BannerDamage, 0), Is.Zero, "the tree line stays out of the hero's sheet");
            loop.SetPartyBanner(new string[0]);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(withTree).Within(1e-9));
            Assert.That(damage, Is.GreaterThan(0));
        }

        [Test]
        public void ACrestShowsItsGainAndTheWindowListsTheBanner()
        {
            Game.Core.Text.Texts.UseLanguage("ru");
            var loop = Member();
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.Crest("choir_15"));
            Assert.That(gain.After.Damage, Is.EqualTo(1.12).Within(1e-9));
            loop.SetPartyBanner(new[] { "choir_15" });
            var lines = Game.Core.Ui.PartyGrowthView.Window(loop.PartyGrowth);
            Assert.That(lines.Any(line => line.Label == "Гербы знамени" && line.Value == Game.Core.Ui.HeroStatsView.Factor(1.12)), Is.True);
        }
    }
}
