using System;
using System.Text.RegularExpressions;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OddityTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static string RealCombat => TestData.ReadGameDataFile(CombatRules.FileName);

        private static string Only(string id)
        {
            var json = RealCombat.Replace("\"golden\": { \"chance\": 0.002", "\"golden\": { \"chance\": 0")
                .Replace("\"wanderer\": { \"chance\": 0.05", "\"wanderer\": { \"chance\": 0");
            json = Regex.Replace(json, "\\{ \"id\": \"(\\w+)\", \"chance\": [0-9.]+, \"minStage\": [0-9]+",
                m => m.Groups[1].Value == id ? $"{{ \"id\": \"{id}\", \"chance\": 1, \"minStage\": 1" : $"{{ \"id\": \"{m.Groups[1].Value}\", \"chance\": 0, \"minStage\": 1");
            return Regex.Replace(json, "\"everySeconds\": [0-9.]+, \"flySeconds\"", "\"everySeconds\": 86400, \"flySeconds\"");
        }

        private static EncounterRules Encounters(string json) => CombatRules.Load(json).Encounters;

        private static (GameLoop Loop, FakeInputSource Input) Hero(string combat, long stage = 40, double damage = 1e12, StatSheet? hero = null)
        {
            var input = new FakeInputSource();
            hero ??= new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, damage));
            var state = new GameState(9) { Class = HeroClass.Runedancer, Stage = stage, HighestStage = stage, RecordStage = stage };
            var loop = TestWorld.Loop(input, state, hero);
            loop.Stages.Encounters = Encounters(combat);
            return (loop, input);
        }

        private static bool PlayUntil(GameLoop loop, FakeInputSource input, GameEventKind kind, OddityKind oddity, int ticks = 4000, int keys = 3)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                if (keys > 0)
                    input.PressKeys(keys);
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                {
                    var e = loop.Events[i];
                    if (e.Kind == kind && (int)e.Amount.ToDouble() == (int)oddity)
                    {
                        loop.Events.Clear();
                        return true;
                    }
                }
                loop.Events.Clear();
            }
            return false;
        }

        [Test]
        public void TheRealDataListsEveryOddityAndKeepsThemOffTheLimitStage()
        {
            var rules = Encounters(RealCombat).Oddities;

            Assert.That(rules.Order.Length, Is.EqualTo(6));
            Assert.That(rules.Order[0].Kind, Is.EqualTo(OddityKind.Splitter));
            Assert.That(rules.Excludes(50000), Is.True);
            Assert.That(rules.WispRewards.Length, Is.EqualTo(5));
            var (loop, _) = Hero(RealCombat);
            Assert.That(loop.Stages.OdditiesAllowed(50000), Is.False);
            Assert.That(loop.Stages.OdditiesAllowed(49999), Is.True);
        }

        [Test]
        public void TheMiserLeavesAGoldenTrailForFiveStages()
        {
            var (loop, input) = Hero(Only("miser"));

            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Miser), Is.True);
            Assert.That(loop.State.Oddities.TrailStages, Is.EqualTo(5));

            var stage = loop.State.Stage;
            while (loop.State.Stage == stage)
            {
                input.PressKeys(3);
                loop.Tick();
            }
            Assert.That(loop.State.Oddities.TrailStages, Is.EqualTo(4));
        }

        [Test]
        public void TrailGoldIsFiveTimesNormalGold()
        {
            var (plain, plainInput) = Hero(Only("bee"));
            var (trail, trailInput) = Hero(Only("bee"));
            trail.State.Oddities.TrailStages = 3;
            plain.State.EnemyHp = BigNumber.FromDouble(1e-9);
            trail.State.EnemyHp = BigNumber.FromDouble(1e-9);
            plainInput.PressKeys(1);
            trailInput.PressKeys(1);

            plain.Tick();
            trail.Tick();

            Assert.That((trail.State.Gold / plain.State.Gold).ToDouble(), Is.EqualTo(5).Within(1e-6));
        }

        [Test]
        public void TrailIncenseLengthensTheTrail()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.TrailStages, ModifierKind.Flat, 3));
            var (loop, input) = Hero(Only("miser"), hero: hero);

            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Miser), Is.True);
            Assert.That(loop.State.Oddities.TrailStages, Is.EqualTo(8));
        }

        [Test]
        public void TheBeeRefillsAQuarterOfTheResource()
        {
            var (loop, input) = Hero(Only("bee"));
            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Bee), Is.True);
            var pending = loop.State.Oddities.PendingResourceShare;
            Assert.That(pending, Is.EqualTo(0.25).Within(1e-9));

            loop.State.Resource = 0;
            loop.Tick();

            Assert.That(loop.State.Oddities.PendingResourceShare, Is.EqualTo(0));
            Assert.That(loop.State.Resource, Is.GreaterThanOrEqualTo(loop.Resource.Max * 0.25 - 1e-6));
        }

        [Test]
        public void TheBarrelGivesHalfOfAMiniBossShards()
        {
            var (loop, input) = Hero(Only("barrel"));
            var before = loop.State.Drops.Shards;
            var barrels = 0;
            for (var i = 0; i < 4; i++)
            {
                if (PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Barrel))
                    barrels++;
            }

            var specialBoss = TestWorld.Data.Drops.Shards.SpecialBoss;
            Assert.That(barrels, Is.EqualTo(4));
            Assert.That(loop.State.Drops.Shards - before, Is.GreaterThanOrEqualTo((long)System.Math.Floor(specialBoss * 0.5 * 4) - 1));
        }

        [Test]
        public void TheDummyTurnsDamageIntoExperienceAndLeavesWithoutDying()
        {
            var (loop, input) = Hero(Only("dummy"), damage: 1e3);
            Assert.That(PlayUntil(loop, input, GameEventKind.OddityAppeared, OddityKind.Dummy), Is.True);
            var xp = loop.State.HeroXp + loop.State.HeroLevel * 1e9;
            var killedWhileStanding = 0;
            var left = false;
            for (var tick = 0; tick < 200 && !left; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
                left = loop.Events.CountOf(GameEventKind.OddityRewarded) > 0;
                if (!left)
                    killedWhileStanding += loop.Events.CountOf(GameEventKind.EnemyKilled);
                loop.Events.Clear();
            }

            Assert.That(left, Is.True);
            Assert.That(killedWhileStanding, Is.EqualTo(0));
            Assert.That(loop.State.Oddities.DummyKills, Is.GreaterThan(0).And.LessThanOrEqualTo(50));
            Assert.That(loop.State.HeroXp + loop.State.HeroLevel * 1e9, Is.GreaterThan(xp));
            Assert.That(loop.State.Oddities.Kind, Is.Not.EqualTo(OddityKind.Dummy));
        }

        [Test]
        public void KillingEverySplitterPartInTimeThinsTheCrowd()
        {
            var (loop, input) = Hero(Only("splitter"), 120);
            var full = loop.Stages.EnemiesOnThisStage;

            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Splitter), Is.True);
            Assert.That(loop.State.Oddities.Kind, Is.EqualTo(OddityKind.SplitterPart));
            for (var tick = 0; tick < 100 && loop.State.Oddities.CrowdStages == 0; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
            }

            Assert.That(loop.State.Oddities.CrowdStages, Is.EqualTo(10));
            Assert.That(loop.Stages.EnemiesOnThisStage, Is.LessThan(full));
        }

        [Test]
        public void SplitterPartsRunAwayWhenTimeIsUp()
        {
            var (loop, input) = Hero(Only("splitter"), 120);
            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Splitter), Is.True);

            for (var tick = 0; tick < 60; tick++)
                loop.Tick();

            Assert.That(loop.State.Oddities.Kind, Is.Not.EqualTo(OddityKind.SplitterPart));
            Assert.That(loop.State.Oddities.CrowdStages, Is.EqualTo(0));
        }

        [Test]
        public void SproutsStackDamageUntilPrestige()
        {
            var (loop, input) = Hero(Only("sprout"), 600);
            var before = loop.Hero.Value(CombatStats.Damage, 1);

            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Sprout), Is.True);
            loop.Tick();

            Assert.That(loop.State.Oddities.SproutStacks, Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(1.02).Within(1e-9));

            loop.State.Oddities.ResetRun();
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void NoOdditiesOnTheLimitStage()
        {
            var (loop, input) = Hero(Only("miser"), 50000);

            Assert.That(PlayUntil(loop, input, GameEventKind.OddityAppeared, OddityKind.Miser, 300), Is.False);
        }

        [Test]
        public void TheRiftLureMultipliesTheChance()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.OddityChance, ModifierKind.Increased, 0.2));
            var (loop, _) = Hero(RealCombat, hero: hero);

            Assert.That(loop.Stages.OddityChanceMultiplier, Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void TheWispIsCaughtByInputAndGivesAReward()
        {
            var json = Regex.Replace(Only("bee"), "\"everySeconds\": [0-9.]+, \"flySeconds\"", "\"everySeconds\": 1, \"flySeconds\"");
            var (loop, input) = Hero(json);
            loop.State.Oddities.WispCooldownTicks = 1;
            loop.Tick();
            Assert.That(loop.State.Oddities.WispFlying, Is.True);

            var caught = false;
            for (var tick = 0; tick < 20 && !caught; tick++)
            {
                input.PressKeys(5);
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                    caught |= loop.Events[i].Kind == GameEventKind.WispCaught;
                loop.Events.Clear();
            }

            Assert.That(caught, Is.True);
            Assert.That(loop.State.Oddities.WispFlying, Is.False);
            Assert.That(loop.State.Oddities.WispCooldownTicks, Is.EqualTo(10));
        }

        [Test]
        public void TheWispFliesAwayFromAnIdlePlayer()
        {
            var (loop, _) = Hero(Only("bee"));
            loop.State.Oddities.WispCooldownTicks = 1;
            var escaped = false;
            for (var tick = 0; tick < 200 && !escaped; tick++)
            {
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                    escaped |= loop.Events[i].Kind == GameEventKind.WispEscaped;
                loop.Events.Clear();
            }

            Assert.That(escaped, Is.True);
        }

        [Test]
        public void ADamageSpreeDoublesDamageWhileItLasts()
        {
            var (loop, _) = Hero(Only("bee"));
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            loop.State.Oddities.DamageSpreeMultiplier = 2;
            loop.State.Oddities.DamageSpreeTicks = 5;

            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1) / before, Is.EqualTo(2).Within(1e-9));

            for (var tick = 0; tick < 6; tick++)
                loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void OddityStateSurvivesSaving()
        {
            var (loop, input) = Hero(Only("splitter"), 120);
            Assert.That(PlayUntil(loop, input, GameEventKind.OddityRewarded, OddityKind.Splitter), Is.True);
            loop.State.Oddities.TrailStages = 3;
            loop.State.Oddities.SproutStacks = 7;
            loop.State.Oddities.WispCooldownTicks = 123;

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Data).State;

            Assert.That(loaded.Oddities.Kind, Is.EqualTo(OddityKind.SplitterPart));
            Assert.That(loaded.Oddities.PartsLeft, Is.EqualTo(loop.State.Oddities.PartsLeft));
            Assert.That(loaded.Oddities.SplitTicks, Is.EqualTo(loop.State.Oddities.SplitTicks));
            Assert.That(loaded.Oddities.TrailStages, Is.EqualTo(3));
            Assert.That(loaded.Oddities.SproutStacks, Is.EqualTo(7));
            Assert.That(loaded.Oddities.WispCooldownTicks, Is.EqualTo(123));
            Assert.That(loaded.Oddities.Rng.State, Is.EqualTo(loop.State.Oddities.Rng.State));
        }
    
        private static string Crowded()
        {
            var json = Regex.Replace(RealCombat, "\\{ \"id\": \"(\\w+)\", \"chance\": [0-9.]+, \"minStage\": [0-9]+",
                m => $"{{ \"id\": \"{m.Groups[1].Value}\", \"chance\": 0.15, \"minStage\": 1");
            return Regex.Replace(json, "\"everySeconds\": [0-9.]+, \"flySeconds\"", "\"everySeconds\": 2, \"flySeconds\"");
        }

        private static void Fight(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                input.PressKeys(1 + tick % 2);
                input.ClickLeft(tick % 3 == 0 ? 1 : 0);
                input.MoveMouse(150);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        [Test]
        public void OdditiesAndTheWispDoNotAllocate()
        {
            var probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (warmUp, warmUpInput) = Hero(Crowded(), 40, 1e3);
            Fight(warmUp, warmUpInput, 3000);
            var (loop, input) = Hero(Crowded(), 40, 1e3);
            Fight(loop, input, 20);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Fight(loop, input, 3000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(loop.State.Oddities.SproutStacks, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(0));
        }
}
}
