using System.Collections.Generic;
using System.Linq;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyTrialTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyGrowthRules Growth => TestWorld.Data.Party.Growth;
        private static PartyTrialRules Rules => Growth.Trials;
        private static PartyTrialDef Trial(string id) => Rules.Find(id)!;

        private static PartyTrialHit[] Hits(int fighters, double keys = 0, double mouse = 0, double skills = 0, double minions = 0, bool typing = true, int reactions = 0)
        {
            var hits = new PartyTrialHit[fighters];
            for (var i = 0; i < fighters; i++)
                hits[i] = new PartyTrialHit(keys, mouse, skills, minions, typing, reactions);
            return hits;
        }

        private static PartyTrialFight Start(string id, int tier = 1, int fighters = 2, double power = 1, bool night = false)
        {
            var fight = new PartyTrialFight();
            fight.Start(Rules, Trial(id), tier, fighters, power, night);
            return fight;
        }

        private static double Run(PartyTrialFight fight, PartyTrialHit[] hits, int ticks)
        {
            var dealt = 0.0;
            for (var i = 0; i < ticks && fight.Running; i++)
                dealt += fight.Advance(hits, hits.Length);
            return dealt;
        }

        [Test]
        public void EightTrialsWithTheirRulesAreRead()
        {
            Assert.That(Rules.List.Select(t => t.Id), Is.EqualTo(new[] { "choir", "relay", "mirror", "silence", "pulse", "shields", "chain", "marathon" }));
            Assert.That(Rules.List.Select(t => t.Rule).Distinct().Count(), Is.EqualTo(8));
            Assert.That(Rules.PerWeek, Is.EqualTo(2));
            Assert.That(Rules.Tries, Is.EqualTo(5));
            Assert.That(Rules.MinFighters, Is.EqualTo(2));
            Assert.That(Rules.Seal, Is.EqualTo(0.005));
            Assert.That(Trial("marathon").Waves, Is.EqualTo(20));
            Assert.That(Rules.ComplicationList.Count, Is.EqualTo(5));
        }

        [Test]
        public void TwoTrialsAreOpenAWeekAndAllEightComeRoundInFourWeeks()
        {
            var week = PartyTrialRules.Week(Now);
            var seen = new List<string>();
            for (var w = week; w < week + 4; w++)
            {
                var open = Rules.List.Where(t => Rules.OpenIn(t, w)).Select(t => t.Id).ToList();
                Assert.That(open.Count, Is.EqualTo(2));
                seen.AddRange(open);
            }
            Assert.That(seen.OrderBy(id => id), Is.EqualTo(Rules.List.Select(t => t.Id).OrderBy(id => id)));
            Assert.That(System.DateTimeOffset.FromUnixTimeMilliseconds(PartyTrialRules.WeekEndsAt(Now)).DayOfWeek, Is.EqualTo(System.DayOfWeek.Monday));
        }

        [Test]
        public void TrialsOpenOnPartyLevelTwentyAndTheCeilingGrowsWithTheLevel()
        {
            Assert.That(Rules.Ceiling(Growth, 19), Is.EqualTo(0));
            Assert.That(Rules.Ceiling(Growth, 20), Is.EqualTo(10));
            Assert.That(Rules.Ceiling(Growth, 25), Is.EqualTo(15));
            Assert.That(Rules.Ceiling(Growth, 45), Is.EqualTo(20));
            Assert.That(Rules.Ceiling(Growth, 100), Is.EqualTo(35));
            Assert.That(Rules.NextTier(3, 10), Is.EqualTo(4));
            Assert.That(Rules.NextTier(10, 10), Is.EqualTo(10));
        }

        [Test]
        public void EachTierHasMoreHpAndOneComplication()
        {
            var stages = TestWorld.Data.Stages;
            var one = Rules.Hp(stages, 1, 1);
            Assert.That((Rules.Hp(stages, 2, 1) / one).ToDouble(), Is.EqualTo(1.35).Within(1e-9));
            Assert.That((Rules.Hp(stages, 1, 1.5) / one).ToDouble(), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(Rules.ComplicationOf(Trial("choir"), 1), Is.Null);
            Assert.That(Rules.ComplicationOf(Trial("choir"), 2)!.Kind, Is.EqualTo(PartyTrialComplication.Carapace));
            Assert.That(Rules.ComplicationOf(Trial("choir"), 3)!.Kind, Is.EqualTo(PartyTrialComplication.Shadow));
            Assert.That(Rules.ComplicationOf(Trial("relay"), 2)!.Kind, Is.EqualTo(PartyTrialComplication.Shadow));
            Assert.That(Rules.ComplicationOf(Trial("marathon"), 9), Is.Null);
        }

        [Test]
        public void ATrialIsClearedWhenTheBossFallsInTimeAndFailsWhenTimeRunsOut()
        {
            var fight = Start("silence", tier: 1);
            Run(fight, Hits(2, mouse: 0.001), 10_000);
            Assert.That(fight.Outcome, Is.EqualTo(PartyTrialOutcome.Cleared));
            Assert.That(fight.ResultTier, Is.EqualTo(1));
            Assert.That(fight.Tick, Is.EqualTo(500));

            var slow = Start("silence", tier: 1);
            Run(slow, Hits(2, mouse: 0.0001), 10_000);
            Assert.That(slow.Outcome, Is.EqualTo(PartyTrialOutcome.Failed));
            Assert.That(slow.Tick, Is.EqualTo(2400));
            Assert.That(slow.ResultTier, Is.Zero);
        }

        [Test]
        public void ChoirHitsOnlyWhileEveryoneTypes()
        {
            var fight = Start("choir");
            Assert.That(fight.Advance(Hits(2, keys: 0.01), 2), Is.EqualTo(0.02).Within(1e-12));
            var hits = Hits(2, keys: 0.01);
            hits[1].Typing = false;
            Assert.That(fight.Advance(hits, 2), Is.Zero);
        }

        [Test]
        public void RelayHitsWithTheBatonHolderAndFiresTheBankOnThePass()
        {
            var fight = Start("relay");
            var dealt = Run(fight, Hits(2, keys: 0.0001), 200);
            Assert.That(dealt, Is.EqualTo(0.02).Within(1e-9), "only the holder hits for 20 s");
            Assert.That(fight.Bank, Is.EqualTo(0.02).Within(1e-9));
            Assert.That(fight.Advance(Hits(2, keys: 0.0001), 2), Is.EqualTo(0.0201).Within(1e-9), "the pass fires the bank");
            Assert.That(fight.Holder, Is.EqualTo(1));
            Assert.That(fight.Bank, Is.EqualTo(0.0001).Within(1e-12));
        }

        [Test]
        public void MirrorResistsTheMostUsedInputAfterEachWindow()
        {
            var fight = Start("mirror");
            Run(fight, Hits(2, keys: 0.0001, mouse: 0.00005), 300);
            Assert.That(fight.Resisted, Is.EqualTo((int)PartyTrialDamage.Keys));
            Assert.That(fight.Advance(Hits(2, keys: 0.0001), 2), Is.EqualTo(0.00008).Within(1e-12));
            Assert.That(fight.Advance(Hits(2, mouse: 0.0001), 2), Is.EqualTo(0.0002).Within(1e-12));
        }

        [Test]
        public void SilenceLetsOnlyMouseClicksAndSkillsHit()
        {
            var fight = Start("silence");
            Assert.That(fight.Advance(Hits(2, keys: 0.01), 2), Is.Zero);
            Assert.That(fight.Advance(Hits(2, mouse: 0.01, skills: 0.01), 2), Is.EqualTo(0.04).Within(1e-12));
        }

        [Test]
        public void PulseHitsHarderInsideItsWindows()
        {
            var fight = Start("pulse");
            Assert.That(fight.PulseOpen, Is.True);
            Assert.That(fight.Advance(Hits(2, keys: 0.0001), 2), Is.EqualTo(0.0006).Within(1e-12));
            Run(fight, Hits(2), 29);
            Assert.That(fight.PulseOpen, Is.False);
            Assert.That(fight.Advance(Hits(2, keys: 0.0001), 2), Is.EqualTo(0.00006).Within(1e-12));
            Run(fight, Hits(2), 69);
            Assert.That(fight.PulseOpen, Is.True);
        }

        [Test]
        public void FourShieldsEachFallOnlyToTheirOwnDamageAndGuardTheBoss()
        {
            var fight = Start("shields");
            Run(fight, Hits(2, keys: 0.01, mouse: 0.01, skills: 0.01), 50);
            Assert.That(fight.HpLeft, Is.EqualTo(1), "minions and poison are missing, the boss stays shielded");
            Assert.That(fight.ShieldLeft(PartyTrialDamage.Keys), Is.Zero);
            Assert.That(fight.ShieldLeft(PartyTrialDamage.Minions), Is.EqualTo(0.1).Within(1e-12));
            Run(fight, Hits(2, minions: 0.01), 5);
            Assert.That(fight.ShieldLeft(PartyTrialDamage.Minions), Is.Zero);
            fight.Advance(Hits(2, keys: 0.01), 2);
            Assert.That(fight.HpLeft, Is.EqualTo(0.98).Within(1e-12));
        }

        [Test]
        public void ChainStartsWeakAndGrowsWithReactionsInARow()
        {
            var fight = Start("chain");
            Assert.That(fight.Advance(Hits(2, keys: 0.001), 2), Is.EqualTo(0.001).Within(1e-12));
            fight.Advance(Hits(2, reactions: 1), 2);
            Assert.That(fight.Chain, Is.EqualTo(2));
            Assert.That(fight.ChainFactor, Is.EqualTo(1.4).Within(1e-12));
            for (var i = 0; i < 10; i++)
                fight.Advance(Hits(1, reactions: 1), 1);
            Assert.That(fight.ChainFactor, Is.EqualTo(3));
            Run(fight, Hits(2), 60);
            Assert.That(fight.ChainFactor, Is.EqualTo(0.5), "six quiet seconds break the chain");
        }

        [Test]
        public void MarathonCountsWavesHeldAndTheirHpGrows()
        {
            var fight = Start("marathon", tier: 7);
            Assert.That(fight.Tier, Is.EqualTo(1));
            Run(fight, Hits(2, keys: 0.01), 300);
            Assert.That(fight.Wave, Is.EqualTo(2));
            Assert.That(fight.HpFull, Is.EqualTo(1.35).Within(1e-12));
            Run(fight, Hits(2, keys: 0.00001), 10_000);
            Assert.That(fight.Outcome, Is.EqualTo(PartyTrialOutcome.Cleared));
            Assert.That(fight.ResultTier, Is.EqualTo(1));

            var strong = Start("marathon");
            Run(strong, Hits(4, keys: 1), 10_000);
            Assert.That(strong.ResultTier, Is.EqualTo(20));
            Assert.That(strong.Tick, Is.EqualTo(6000));
        }

        [Test]
        public void ComplicationsChangeTheFight()
        {
            Assert.That(Start("choir", tier: 2).Advance(Hits(2, keys: 0.001), 2), Is.EqualTo(0.0012).Within(1e-12), "carapace");
            Assert.That(Start("choir", tier: 3).Advance(Hits(2, mouse: 0.001), 2), Is.EqualTo(0.0012).Within(1e-12), "shadow");
            var shield = Start("choir", tier: 4);
            Assert.That(shield.Advance(Hits(2, keys: 0.01), 2), Is.Zero, "only skills break the skill shield");
            Assert.That(shield.Advance(Hits(2, skills: 0.01), 2), Is.EqualTo(0.02).Within(1e-12));
            Assert.That(shield.SkillShieldLeft, Is.EqualTo(0.03).Within(1e-12));
            Assert.That(Start("choir", tier: 5, night: true).Advance(Hits(2, keys: 0.001), 2), Is.EqualTo(0.0026).Within(1e-12), "night");
            Assert.That(Start("choir", tier: 5).Advance(Hits(2, keys: 0.001), 2), Is.EqualTo(0.0014).Within(1e-12), "day");
            var regen = Start("choir", tier: 6);
            regen.Advance(Hits(2, keys: 0.1), 2);
            Run(regen, Hits(2), 40);
            Assert.That(regen.HpLeft, Is.EqualTo(0.8 + 0.0005 * 11).Within(1e-9), "the boss heals after three quiet seconds");
        }

        [Test]
        public void TrialPowerScalesTheWholeFight()
        {
            Assert.That(Start("silence", power: 0.9).Advance(Hits(2, mouse: 0.01), 2), Is.EqualTo(0.018).Within(1e-12));
        }

        [Test]
        public void SealsGiveThePartyDamageAndTrialTakersRaiseThem()
        {
            var loop = TestWorld.Loop();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyTrials(new[] { 10, 6, 0, 0, 0, 0, 0, 4 });
            Assert.That(loop.PartyGrowth.Seals, Is.EqualTo(0.1).Within(1e-12));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.1).Within(1e-9));
            loop.SetPartyTree(new Dictionary<int, int> { [31] = 1 }, new string[0]);
            Assert.That(loop.PartyGrowth.Seals, Is.EqualTo(0.105).Within(1e-12));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.105).Within(1e-9));
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1 + 0.105 * 0.3).Within(1e-9), "a newcomer gets a share of the seals");
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void BigTalentsOnNinetyChangeSealsAndTrialPower()
        {
            var loop = TestWorld.Loop();
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyTrials(new[] { 20 });
            var levels = Enumerable.Range(1, 18).ToDictionary(node => node, node => 5);
            loop.SetPartyTree(levels, new[] { "party_m90_trials" });
            Assert.That(loop.PartyGrowth.Seals, Is.EqualTo(0.1 * 1.3).Within(1e-12));
            Assert.That(loop.PartyGrowth.TrialPower, Is.EqualTo(1));
            loop.SetPartyTree(levels, new[] { "party_m90_colossus" });
            Assert.That(loop.PartyGrowth.TrialPower, Is.EqualTo(0.9).Within(1e-12));
            Assert.That(loop.PartyGrowth.Applied.Any(m => m.Stat == PartyGrowthLayer.TrialPowerStat), Is.False, "trial lines stay out of the hero's sheet");
        }
    }
}
