using System.Collections.Generic;
using System.Linq;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartyGrowthViewTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        [SetUp]
        public void UseRussian()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static GameLoop Member(int members = 4)
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, members, Now);
            return loop;
        }

        private static string ValueOf(IReadOnlyList<HeroStatLine> lines, string key) =>
            lines.FirstOrDefault(line => line.Label == Texts.Get(key)).Value ?? string.Empty;

        [Test]
        public void TheBannerShowsThePartyMultiplier()
        {
            var loop = TestWorld.Loop();
            Assert.That(PartyGrowthView.Banner(loop.PartyGrowth), Is.Empty, "no party, no banner number");
            loop = Member();
            loop.SetPartyTree(new Dictionary<int, int> { [1] = 2 }, new string[0]);
            Assert.That(PartyGrowthView.Banner(loop.PartyGrowth), Is.EqualTo("×1.06"));
        }

        [Test]
        public void ATreeNodeShowsDamageBeforeAndAfterWithoutChangingTheParty()
        {
            var loop = Member();
            loop.SetPartyTree(new Dictionary<int, int> { [1] = 2 }, new string[0]);
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.TreeNode(1));
            Assert.That(gain.Before.Damage, Is.EqualTo(1.06).Within(1e-9));
            Assert.That(gain.After.Damage, Is.EqualTo(1.06).Within(1e-9), "level 3 adds line B, not damage");
            var first = PartyGrowthPreview.Of(Member().PartyGrowth, PartyUpgrade.TreeNode(1));
            Assert.That(PartyGrowthView.GainSummary(first), Is.EqualTo("Урон отряда ×1 → ×1.03 (+3%)"));
            Assert.That(loop.PartyGrowth.TreeLevels[1], Is.EqualTo(2), "the preview works on a copy");
            Assert.That(loop.PartyGrowth.DamageMultiplier, Is.EqualTo(1.06).Within(1e-9));
        }

        [Test]
        public void ARelicShowsOnlyWhatItChanges()
        {
            var loop = Member();
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.Relic("hunt_horn"));
            var lines = PartyGrowthView.Gain(gain);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Label, Is.EqualTo("Урон по боссам"));
            Assert.That(lines[0].Value, Is.EqualTo("×1 → ×1.02 (+1.5%)"));
            gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.Relic("plenty_horn"));
            Assert.That(ValueOf(PartyGrowthView.Gain(gain), "party.gain.peaceful"), Is.EqualTo("+0% → +1.5%"));
        }

        [Test]
        public void MascotLevelsRitesAndTrialTiersShowTheirGain()
        {
            var loop = Member(1);
            loop.SetPartyMascot("bear", 1);
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.MascotLevels(99));
            Assert.That(gain.After.VsBoss, Is.EqualTo(1.4).Within(1e-9));
            Assert.That(ValueOf(PartyGrowthView.Gain(gain), "party.gain.peaceful"), Is.Not.Empty, "the peaceful gift grows too");
            gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.Rite("power_aura"));
            Assert.That(gain.After.VsBoss / gain.Before.VsBoss, Is.EqualTo((1 + 0.02 * 1.05) / 1.02).Within(1e-9), "a rite +5% of the aura");
            gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.TrialTier(0));
            Assert.That(gain.After.Damage, Is.EqualTo(1.005).Within(1e-9));
            Assert.That(gain.After.Of(PartyGrowthSource.Seals), Is.EqualTo(1.005).Within(1e-9));
            gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.Mascot("phoenix"));
            Assert.That(gain.After.Colossus, Is.GreaterThan(gain.Before.Colossus));
        }

        [Test]
        public void ABigTalentReplacesTheOneInItsSlot()
        {
            var loop = Member();
            var levels = Enumerable.Range(1, 18).ToDictionary(node => node, node => 5);
            loop.SetPartyTree(levels, new[] { "party_m90_trials" });
            var gain = PartyGrowthPreview.Of(loop.PartyGrowth, PartyUpgrade.TreeMajor("party_m90_colossus"));
            Assert.That(gain.After.Colossus / gain.Before.Colossus, Is.EqualTo(1.3 / 0.9).Within(1e-9));
        }

        [Test]
        public void TheWindowShowsTotalsSourcesTargetsAndPeace()
        {
            var loop = Member();
            loop.SetPartyTree(new Dictionary<int, int> { [1] = 2, [4] = 1 }, new string[0]);
            loop.SetPartyTrials(new[] { 10 });
            loop.SetPartyRelics(new Dictionary<string, int> { ["hunt_horn"] = 10, ["plenty_horn"] = 2 });
            var lines = PartyGrowthView.Window(loop.PartyGrowth);
            Assert.That(lines[0].IsHeader, Is.True);
            Assert.That(lines[0].Value, Is.EqualTo(HeroStatsView.Factor(loop.PartyGrowth.DamageMultiplier)));
            Assert.That(ValueOf(lines, "party.view.source.tree"), Is.EqualTo("×1.06"));
            Assert.That(ValueOf(lines, "party.view.source.seals"), Is.EqualTo("×1.05"));
            Assert.That(ValueOf(lines, "party.gain.boss"), Is.EqualTo("×1.15"));
            Assert.That(ValueOf(lines, "party.view.peaceful"), Is.EqualTo("+3%"));
            Assert.That(ValueOf(lines, "party.view.newcomer"), Is.Empty);
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(ValueOf(PartyGrowthView.Window(loop.PartyGrowth), "party.view.newcomer"), Is.EqualTo("30%"));
            loop.SetPartyGrowthSources(new[] { new Game.Core.Stats.Modifier(CombatStats.Damage, Game.Core.Stats.ModifierKind.More, 5) });
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            Assert.That(ValueOf(PartyGrowthView.Window(loop.PartyGrowth), "party.view.capped"), Is.EqualTo("×3"));
        }

        [Test]
        public void TheCallPopupNamesTheBeast()
        {
            var rules = TestWorld.Data.Party.Growth.Mascot;
            Assert.That(PartyGrowthView.CallPopup(rules, "wolf", 30), Is.EqualTo("Волк Охоты: Зов! 30 с"));
            Assert.That(PartyGrowthView.CallPopup(rules, "nope", 30), Is.Empty);
        }
    }
}
