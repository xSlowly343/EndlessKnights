using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyColossusTests
    {
        private const long Day = 86_400_000;
        private const long Now = 1_763_000_000_000;

        private static PartyGrowthRules Growth => TestWorld.Data.Party.Growth;
        private static PartyColossusRules Rules => Growth.Colossus;

        private static BigNumber Boss(long stage) => TestWorld.Data.Stages.BossHp(stage);

        private static (GameLoop Loop, FakeInputSource Input) Member()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            loop.SetPartyStanding(true, Now - 10 * Day, 4, Now);
            loop.SetPartyColossusOpen(true);
            return (loop, input);
        }

        private static void Type(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.PressKeys(3);
                loop.Tick();
            }
        }

        [Test]
        public void ColossusRulesAreRead()
        {
            Assert.That(Rules.PointsPerBoss, Is.EqualTo(100));
            Assert.That(Rules.Background, Is.EqualTo(0.1));
            Assert.That(Rules.Offline, Is.EqualTo(0.25));
            Assert.That(Rules.Hp(1), Is.EqualTo(15000));
            Assert.That(Rules.Hp(3), Is.EqualTo(15000 * 1.45 * 1.45).Within(1e-6));
            Assert.That(Rules.Ceiling(Growth, 9), Is.EqualTo(0));
            Assert.That(Rules.Ceiling(Growth, 10), Is.EqualTo(10));
            Assert.That(Rules.Ceiling(Growth, 15), Is.EqualTo(20));
            Assert.That(Rules.Ceiling(Growth, 95), Is.EqualTo(70));
        }

        [Test]
        public void HitsAreSortedIntoKeysMouseSkillsAndMinions()
        {
            Assert.That(CombatSystem.KindOf(DamageSource.Strike, StatTags.Strike), Is.EqualTo(PartyTrialDamage.Keys));
            Assert.That(CombatSystem.KindOf(DamageSource.Skill, StatTags.Strike), Is.EqualTo(PartyTrialDamage.Keys), "key procs of a class count as keys");
            Assert.That(CombatSystem.KindOf(DamageSource.Mouse, StatTags.Mouse), Is.EqualTo(PartyTrialDamage.Mouse));
            Assert.That(CombatSystem.KindOf(DamageSource.Click, StatTags.None), Is.EqualTo(PartyTrialDamage.Mouse));
            Assert.That(CombatSystem.KindOf(DamageSource.Skill, StatTags.Skill), Is.EqualTo(PartyTrialDamage.Skills));
            Assert.That(CombatSystem.KindOf(DamageSource.Skill, StatTags.Skill | StatTags.DoT), Is.EqualTo(PartyTrialDamage.Minions));
            Assert.That(CombatSystem.KindOf(DamageSource.Skill, StatTags.Minion), Is.EqualTo(PartyTrialDamage.Minions));
        }

        [Test]
        public void TypingAtTheColossusGivesKeyPointsWorthTheBossOfTheRecordStage()
        {
            var (loop, input) = Member();
            Assert.That(loop.EnterPartyColossus(), Is.True);
            var before = loop.State.TotalDamage;
            Type(loop, input, 100);
            var dealt = loop.State.TotalDamage - before;
            var expected = (dealt / Boss(loop.State.RecordStage)).ToDouble() * 100;
            Assert.That(loop.PartyColossus.DirectOf(PartyTrialDamage.Keys), Is.GreaterThan(0));
            Assert.That(loop.PartyColossus.Direct, Is.EqualTo(expected).Within(expected * 1e-6));
            var direct = new double[4];
            var background = new double[4];
            loop.PartyColossus.TakePending(direct, background);
            Assert.That(direct[0], Is.EqualTo(expected).Within(expected * 1e-6));
            Assert.That(loop.PartyColossus.Pending, Is.Zero);
            loop.PartyColossus.Return(direct, background);
            Assert.That(loop.PartyColossus.Direct, Is.EqualTo(expected).Within(expected * 1e-6), "points come back when the report fails");
        }

        [Test]
        public void TwelveHardshipsAndUpToThreeDrawnOnEachTier()
        {
            Assert.That(Rules.Hardships.Count, Is.EqualTo(12));
            Assert.That(Rules.MaxHardships, Is.EqualTo(3));
            Assert.That(Rules.MandatoryRange(1), Is.EqualTo((1, 1)));
            Assert.That(Rules.MandatoryRange(15), Is.EqualTo((1, 1)));
            Assert.That(Rules.MandatoryRange(16), Is.EqualTo((2, 2)));
            Assert.That(Rules.MandatoryRange(30), Is.EqualTo((2, 2)));
            Assert.That(Rules.MandatoryRange(31), Is.EqualTo((2, 3)));
            Assert.That(Rules.MandatoryRange(45), Is.EqualTo((2, 3)));
            Assert.That(Rules.MandatoryRange(46), Is.EqualTo((3, 3)));
            Assert.That(Rules.MandatoryRange(500), Is.EqualTo((3, 3)));
            Assert.That(Rules.OptionalSlots(1), Is.EqualTo(2));
            Assert.That(Rules.OptionalSlots(2), Is.EqualTo(1));
            Assert.That(Rules.OptionalSlots(3), Is.Zero);
            Assert.That(Rules.OptionalMax, Is.EqualTo(2));
            Assert.That(Rules.OptionalTrophyBonus, Is.EqualTo(0.25));
            Assert.That(Rules.Hardship("limitArmor")!.Number("hp"), Is.EqualTo(50));
            Assert.That(Rules.Hardship("curse")!.NameRu, Is.EqualTo("Проклятие"));
        }

        [Test]
        public void TheCurseWeakensTheNormalFightForTenMinutesAfterAColossusHit()
        {
            var (loop, input) = Member();
            Type(loop, input, 20);
            var normal = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyColossusHardships(new[] { "curse" });
            loop.EnterPartyColossus();
            Type(loop, input, 50);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(normal).Within(normal * 1e-9), "no curse while fighting the colossus");
            loop.LeavePartyColossus();
            Type(loop, input, 1);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(normal * 0.9).Within(normal * 1e-9));
            Assert.That(loop.ColossusCurseTicks, Is.EqualTo(6000 - 1));
            for (var i = 0; i < 6000; i++)
                loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(normal).Within(normal * 1e-9));
            loop.SetPartyColossusHardships(new string[0]);
            Assert.That(loop.ColossusCurseTicks, Is.Zero);
        }

        [Test]
        public void OfflineKillsGiveAQuarter()
        {
            var meter = new PartyColossusMeter(Rules);
            var stages = TestWorld.Data.Stages;
            var result = new OfflineResult(3600, 70, BigNumber.Zero, 50, 50, 0, false, false, false);
            meter.NoteOffline(result, stages, 50, true, 1);
            var expected = (stages.NormalHp(50) * 70.0 / Boss(50)).ToDouble() * 100 * 0.25;
            var taken = meter.TakeOffline();
            Assert.That(taken.Points, Is.EqualTo(expected).Within(1e-9));
            Assert.That(taken.Seconds, Is.EqualTo(3600));
            meter.NoteOffline(result, stages, 50, false, 1);
            Assert.That(meter.TakeOffline().Points, Is.Zero, "without a party nothing goes to the colossus");
        }

        [Test]
        public void TheColossusTabNeedsAPartyWithTheColossusOpen()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.EnterPartyColossus(), Is.False);
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.EnterPartyColossus(), Is.False, "the colossus opens on party level 10");
            loop.SetPartyColossusOpen(true);
            Assert.That(loop.EnterPartyColossus(), Is.True);
            loop.SetPartyColossusOpen(false);
            Assert.That(loop.AtPartyColossus, Is.False);
        }

        [Test]
        public void FightingTheColossusStopsTheStageAndEveryHitCounts()
        {
            var (loop, input) = Member();
            Type(loop, input, 30);
            var stage = loop.State.Stage;
            var kills = loop.State.Kills;
            loop.PartyColossus.TakePending(new double[4], new double[4]);
            Assert.That(loop.EnterPartyColossus(), Is.True);
            Type(loop, input, 600);
            Assert.That(loop.State.Stage, Is.EqualTo(stage));
            Assert.That(loop.State.Kills, Is.EqualTo(kills), "the colossus never dies in the tab");
            Assert.That(loop.State.EnemyAlive, Is.True);
            Assert.That(loop.PartyColossus.Direct, Is.GreaterThan(0));
            Assert.That(loop.PartyColossus.Background, Is.Zero);
            loop.LeavePartyColossus();
            Type(loop, input, 600);
            Assert.That(loop.State.Kills, Is.GreaterThan(kills));
            Assert.That(loop.PartyColossus.Background, Is.GreaterThan(0), "a tenth of the normal fight goes to the colossus");
        }

        [Test]
        public void AnIdleTwinkGivesTheColossusNothing()
        {
            var (loop, input) = Member();
            Type(loop, input, 10);
            for (var i = 0; i < 400; i++)
                loop.Tick();
            loop.PartyColossus.TakePending(new double[4], new double[4]);
            for (var i = 0; i < 1200; i++)
                loop.Tick();
            Assert.That(loop.PartyColossus.Pending, Is.Zero);
        }

        [Test]
        public void ColossusLinesOfThePartyTreeRaiseColossusDamageOnly()
        {
            var (loop, _) = Member();
            loop.SetPartyTree(new Dictionary<int, int> { [6] = 1, [17] = 1 }, new string[0]);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.11).Within(1e-12));
            Assert.That(loop.Hero.Value(PartyGrowthLayer.ColossusDamage, 0), Is.Zero, "the line stays out of the hero's sheet");
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1));
        }

        [Test]
        public void TwentyFourRelicsOpenEveryTwoTiersAndALevelCostsItsNumber()
        {
            Assert.That(Rules.Relics.Count, Is.EqualTo(24));
            Assert.That(Rules.RelicsOpen(1), Is.Zero);
            Assert.That(Rules.RelicsOpen(2), Is.EqualTo(1));
            Assert.That(Rules.RelicsOpen(48), Is.EqualTo(24));
            Assert.That(Rules.RelicsOpen(300), Is.EqualTo(24));
            Assert.That(Rules.RelicPrice(0), Is.EqualTo(1));
            Assert.That(Rules.RelicPrice(29), Is.EqualTo(30));
            Assert.That(Rules.RelicPrice(30), Is.Zero);
            Assert.That(Rules.RelicTotal(30), Is.EqualTo(465));
            Assert.That(Rules.Trophies(1), Is.EqualTo(1));
            Assert.That(Rules.Trophies(10), Is.EqualTo(3));
            var multipliers = 0;
            foreach (var relic in Rules.Relics)
            {
                if (relic.Compound)
                    multipliers++;
            }
            Assert.That(multipliers, Is.EqualTo(6));
            Assert.That(Rules.Relic("colossus_fang")!.ValueAt(30), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(Rules.Relic("eternal_heart")!.ValueAt(30), Is.EqualTo(System.Math.Pow(1.005, 30) - 1).Within(1e-12), "a multiplier compounds to about x1.16");
            Assert.That(Rules.Relic("limit_brand")!.Works, Is.False, "the party limit comes later");
        }

        [Test]
        public void RelicsFeedThePartyBasketAndColossusDamage()
        {
            var (loop, _) = Member();
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.SetPartyRelics(new Dictionary<string, int> { ["colossus_fang"] = 10, ["eternal_heart"] = 30, ["colossus_eye"] = 5, ["colossus_chain"] = 10, ["storm_eye"] = 30 });
            var heart = System.Math.Pow(1.005, 30) - 1;
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.1 * (1 + heart)).Within(1e-9));
            Assert.That(loop.PartyGrowth.ColossusPower, Is.EqualTo(1.1 * System.Math.Pow(1.008, 10)).Within(1e-12));
            Assert.That(loop.Hero.Value(PartyGrowthLayer.ColossusDamage, 0), Is.Zero);
            loop.SetPartyStanding(true, Now, 4, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo((1 + 0.03) * (1 + heart * 0.3)).Within(1e-9), "a newcomer gets a share");
            loop.SetPartyStanding(false, 0, 0, Now);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void TheOvercritMaskMultipliesOvercritDamage()
        {
            var (loop, _) = Member();
            var before = loop.Combat.OvercritMultiplier(StatTags.Strike);
            loop.SetPartyRelics(new Dictionary<string, int> { ["overcrit_mask"] = 30 });
            Assert.That(loop.Combat.OvercritMultiplier(StatTags.Strike) / before, Is.EqualTo(System.Math.Pow(1.005, 30)).Within(1e-9));
        }
    }
}
