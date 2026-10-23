using System.Linq;
using Game.Core.Clock;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BossHintTests
    {
        private static int AfterFails => TestWorld.Data.Stages.BossHint.AfterFails;

        private static GameLoop LoopAtBoss() => LoopAtBoss(new FakeInputSource(), new StatSheet());

        private static GameLoop LoopAtBoss(FakeInputSource input, StatSheet hero)
        {
            var strong = new Modifier(CombatStats.Damage, ModifierKind.More, 1e12);
            hero.Add(strong);
            var loop = TestWorld.Loop(input, hero: hero);
            for (var i = 0; i < TestWorld.Data.Stages.EnemiesPerStage; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            hero.Remove(strong);
            return loop;
        }

        private static void FailBoss(GameLoop loop, double damageFraction)
        {
            if (!loop.State.FightingBoss)
                Assert.That(loop.Stages.RequestBossFight(), Is.True);
            loop.State.EnemyHp = loop.State.EnemyMaxHp * (1 - damageFraction);
            var fails = loop.State.BossFailsInARow;
            while (loop.State.BossFailsInARow == fails)
                loop.Tick();
        }

        [Test]
        public void NoHintBeforeThirdFail()
        {
            var loop = LoopAtBoss();
            for (var i = 0; i < AfterFails - 1; i++)
                FailBoss(loop, 0.4);

            Assert.That(loop.BossHint, Is.Null);
        }

        [Test]
        public void HintShowsMissingDamageFromBestAttempt()
        {
            var loop = LoopAtBoss();
            FailBoss(loop, 0.5);
            FailBoss(loop, 0.7);
            FailBoss(loop, 0.6);

            var hint = loop.BossHint;

            Assert.That(hint, Is.Not.Null);
            Assert.That(loop.State.BossBestFraction, Is.EqualTo(0.7).Within(1e-6));
            Assert.That(hint!.MissingPercent, Is.EqualTo(45));
            Assert.That(hint.UnknownDamage, Is.False);
            Assert.That(hint.Lines(NumberFormatSettings.Default)[0], Is.EqualTo("Тебе не хватает ~45% урона"));
        }

        [Test]
        public void ChangeBuildFriendAndFarmAreAlwaysOffered()
        {
            var loop = LoopAtBoss();
            for (var i = 0; i < AfterFails; i++)
                FailBoss(loop, 0.4);

            var hint = loop.BossHint!;

            Assert.That(hint.Has(BossHintOptions.ChangeBuild), Is.True);
            Assert.That(hint.Has(BossHintOptions.CallFriend), Is.True);
            Assert.That(hint.Has(BossHintOptions.FarmStage), Is.True);
            Assert.That(hint.Has(BossHintOptions.Prestige), Is.False);
            Assert.That(hint.Lines(NumberFormatSettings.Default), Has.Some.Contains("Позвать друга"));
        }

        [Test]
        public void PrestigeAndUpgradeAppearWhenAvailable()
        {
            var loop = LoopAtBoss();
            for (var i = 0; i < AfterFails; i++)
                FailBoss(loop, 0.4);
            loop.State.HighestStage = TestWorld.Data.Prestige.MinStage;
            loop.State.RunGoldEarned = BigNumber.FromDouble(1e9);
            loop.State.Gold = BigNumber.FromDouble(1e9);

            var hint = loop.BossHint!;

            Assert.That(hint.Has(BossHintOptions.Prestige), Is.True);
            Assert.That(hint.Has(BossHintOptions.StrikeUpgrade), Is.True);
            Assert.That(hint.EchoGain.Sign, Is.GreaterThan(0));
            Assert.That(hint.Lines(NumberFormatSettings.Default).Any(line => line.StartsWith("• Престиж: +")), Is.True);
        }

        [Test]
        public void BossWithoutDamageGivesUnknownHint()
        {
            var loop = LoopAtBoss();
            for (var i = 0; i < AfterFails; i++)
                FailBoss(loop, 0);

            var hint = loop.BossHint!;

            Assert.That(hint.UnknownDamage, Is.True);
            Assert.That(hint.MissingPercent, Is.EqualTo(TestWorld.Data.Stages.BossHint.MaxPercent));
        }

        [Test]
        public void BeatingTheBossClearsTheHint()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            var loop = LoopAtBoss(input, hero);
            for (var i = 0; i < AfterFails; i++)
                FailBoss(loop, 0.4);
            Assert.That(loop.Stages.RequestBossFight(), Is.True);
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Stage, Is.EqualTo(2));
            Assert.That(loop.State.BossBestFraction, Is.EqualTo(0));
            Assert.That(loop.BossHint, Is.Null);
        }

        [TestCase(0.5, 100)]
        [TestCase(0.7, 45)]
        [TestCase(0.99, 5)]
        [TestCase(0.001, 1000)]
        public void MissingPercentRoundsUpToStep(double fraction, int expected)
        {
            Assert.That(TestWorld.Data.Stages.BossHint.MissingPercent(fraction), Is.EqualTo(expected));
        }

        [Test]
        public void BestFractionSurvivesSave()
        {
            var state = new GameState { BossFailsInARow = 3, BossBestFraction = 0.62 };
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, clock), TestWorld.Data).State;

            Assert.That(loaded.BossBestFraction, Is.EqualTo(0.62).Within(1e-9));
        }
    }
}
