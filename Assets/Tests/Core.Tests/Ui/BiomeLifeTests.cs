using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Content;
using Game.Core.Tests.Content;
using Game.Core.Settings;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class BiomeLifeTests
    {
        private const long Minute = 60_000;
        private const long Hour = 60 * Minute;
        private static readonly long Start = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static BiomeLifeRules Rules => TestWorld.Real.BiomeLife;
        private static EffectLevels Full => EffectLevels.Of(new GameSettings(), false);

        private static BiomeLook Look(string id) => Rules.Looks.First(look => look.Id == id);

        private static GameState In(string biome) => new GameState(1) { Biome = biome };

        private static BiomeLife Read(GameState state, long ms, double hour = 12, EffectLevels? levels = null)
        {
            var life = new BiomeLife(Rules);
            life.Read(state, levels ?? Full, ms, hour);
            return life;
        }

        private static long FirstWith(BiomeLook look, Func<WorldSpan, bool> wanted)
        {
            for (var ms = Start; ms < Start + 2000 * Hour; )
            {
                var span = WorldWeather.At(look, Rules, ms);
                if (wanted(span))
                    return span.StartMs;
                ms = span.EndMs;
            }
            Assert.Fail("no such weather in 2000 hours");
            return 0;
        }

        [Test]
        public void EveryBiomeOfTheStageTableHasALookAndTheRiftHasItsOwn()
        {
            foreach (var biome in TestWorld.Real.Stages.Biomes)
                Assert.That(Rules.Knows(biome), Is.True, biome);
            Assert.That(Rules.LookOf("green_hills").Id, Is.EqualTo("hills"));
            Assert.That(Rules.LookOf("rift_heart").AlwaysNight, Is.True);
            Assert.That(Rules.RiftLook.Weathers, Is.EqualTo(new[] { "riftStorm" }));
            Assert.That(Rules.Looks.Count, Is.EqualTo(7));
            foreach (var look in Rules.Looks)
                Assert.That(look.Weathers.Count, Is.GreaterThan(0), look.Id);
        }

        [Test]
        public void TwoPlayersSeeTheSameWeatherAtTheSameServerTime()
        {
            foreach (var look in Rules.Looks)
            {
                for (var step = 0; step < 300; step++)
                {
                    var ms = Start + step * 7 * Minute + step * 311;
                    var mine = WorldWeather.At(look, Rules, ms);
                    var theirs = WorldWeather.At(look, Rules, ms);
                    Assert.That(mine.Pick, Is.EqualTo(theirs.Pick));
                    Assert.That(mine.StartMs, Is.EqualTo(theirs.StartMs));
                }
            }
            var first = Read(In("green_hills"), Start + 5 * Hour);
            var second = Read(new GameState(999) { Biome = "green_hills", Stage = 40 }, Start + 5 * Hour);
            Assert.That(second.WeatherId, Is.EqualTo(first.WeatherId));
            Assert.That(second.BackgroundX(0), Is.EqualTo(first.BackgroundX(0)));
        }

        [Test]
        public void WeatherHoldsFromFifteenToThirtyMinutesWithoutGapsAcrossBlocks()
        {
            foreach (var look in Rules.Looks)
            {
                var span = WorldWeather.At(look, Rules, Start);
                for (var i = 0; i < 400; i++)
                {
                    var length = span.EndMs - span.StartMs;
                    Assert.That(length, Is.InRange(Rules.WeatherMinMs, Rules.WeatherMaxMs), look.Id);
                    Assert.That(WorldWeather.At(look, Rules, span.EndMs - 1).StartMs, Is.EqualTo(span.StartMs));
                    var next = WorldWeather.At(look, Rules, span.EndMs);
                    Assert.That(next.StartMs, Is.EqualTo(span.EndMs), "no gap");
                    Assert.That(next.PreviousPick, Is.EqualTo(span.Pick), "the next span remembers what came before");
                    span = next;
                }
            }
        }

        [Test]
        public void WeathersComeRoughlyByTheirWeightsAndBiomesDoNotMoveInStep()
        {
            var hills = Look("hills");
            var counts = new int[hills.Weathers.Count];
            var span = WorldWeather.At(hills, Rules, Start);
            for (var i = 0; i < 4000; i++)
            {
                counts[span.Pick]++;
                span = WorldWeather.At(hills, Rules, span.EndMs);
            }
            Assert.That(counts[0] / 4000.0, Is.EqualTo(0.4).Within(0.05), "clear");
            Assert.That(counts[3] / 4000.0, Is.EqualTo(0.1).Within(0.04), "wind");

            var autumn = Look("autumn");
            var different = 0;
            for (var i = 0; i < 50; i++)
            {
                if (WorldWeather.At(hills, Rules, Start + i * Hour).StartMs != WorldWeather.At(autumn, Rules, Start + i * Hour).StartMs)
                    different++;
            }
            Assert.That(different, Is.GreaterThan(25));
        }

        [Test]
        public void ANewWeatherFadesInOverHalfAMinute()
        {
            var hills = Look("hills");
            var start = WorldWeather.At(hills, Rules, Start).EndMs;
            Assert.That(Read(In("green_hills"), start).WeatherBlend, Is.EqualTo(0));
            Assert.That(Read(In("green_hills"), start + 15_000).WeatherBlend, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(Read(In("green_hills"), start + 40_000).WeatherBlend, Is.EqualTo(1));
        }

        [Test]
        public void TheDayHasFourPhasesWithSoftEdgesAndBeyondTheRiftItIsAlwaysNight()
        {
            Assert.That(Rules.LightAt(6).To, Is.EqualTo(DayPhase.Morning));
            Assert.That(Rules.LightAt(12).To, Is.EqualTo(DayPhase.Day));
            Assert.That(Rules.LightAt(18).To, Is.EqualTo(DayPhase.Evening));
            Assert.That(Rules.LightAt(23).To, Is.EqualTo(DayPhase.Night));
            Assert.That(Rules.LightAt(2).To, Is.EqualTo(DayPhase.Night));
            Assert.That(Rules.LightAt(2).Blend, Is.EqualTo(1));

            var dawn = Rules.LightAt(5.25);
            Assert.That(dawn.From, Is.EqualTo(DayPhase.Night));
            Assert.That(dawn.To, Is.EqualTo(DayPhase.Morning));
            Assert.That(dawn.Blend, Is.EqualTo(0.5).Within(1e-9));

            Assert.That(Read(In("rift_heart"), Start, 12).Light.IsNight, Is.True);
            var off = EffectLevels.Of(new GameSettings { DayNightShown = false }, false);
            Assert.That(Read(In("green_hills"), Start, 23, off).Light.To, Is.EqualTo(DayPhase.Day), "the switch keeps the day on");
            Assert.That(TestWorld.Real.Combat.Conditions.ServerDayHours(90 * Minute), Is.EqualTo((1.5 + TestWorld.Real.Combat.Conditions.ServerUtcOffsetHours) % 24).Within(1e-9));
        }

        [Test]
        public void DayAndNightBringDifferentCreatures()
        {
            var day = Read(In("green_hills"), Start, 12);
            var night = Read(In("green_hills"), Start, 23);

            Assert.That(day.Background, Is.EqualTo(new[] { "butterflies", "starlings" }));
            Assert.That(night.Background, Is.EqualTo(new[] { "fireflies", "owl" }));
            Assert.That(day.GroundCount, Is.EqualTo(3));
            Assert.That(night.GroundCount, Is.EqualTo(0), "nobody lives on the ground at night here");
            var shown = Enumerable.Range(0, day.BackgroundCount).Select(day.BackgroundAt).Distinct();
            Assert.That(shown, Is.EquivalentTo(new[] { "butterflies", "starlings" }));
        }

        [Test]
        public void TheSliderBudgetIsNeverExceededAndTheEnemyZoneStaysEmpty()
        {
            foreach (var world in new[] { 0, 0.25, 0.5, 0.8, 1.0 })
            {
                var levels = EffectLevels.Of(new GameSettings { LivingWorld = world }, false);
                foreach (var biome in TestWorld.Real.Stages.Biomes)
                {
                    for (var step = 0; step < 40; step++)
                    {
                        var life = Read(In(biome), Start + step * 23 * Minute, step % 24, levels);
                        Assert.That(life.WeatherLayers, Is.LessThanOrEqualTo(levels.WeatherLayers));
                        Assert.That(life.BackgroundCount, Is.LessThanOrEqualTo(levels.BackgroundCreatures));
                        Assert.That(life.GroundCount, Is.LessThanOrEqualTo(levels.GroundCreatures));
                        for (var i = 0; i < life.BackgroundCount; i++)
                            Assert.That(Rules.InEnemyZone(life.BackgroundX(i), 0), Is.False);
                        for (var i = 0; i < life.GroundCount; i++)
                            Assert.That(life.GroundX(i), Is.InRange(0, Rules.FreeWidthPx));
                    }
                }
            }
            Assert.That(Rules.InEnemyZone(400, 30), Is.True);
            Assert.That(Rules.InEnemyZone(400, 90), Is.False);
            Assert.That(Rules.InEnemyZone(300, 30), Is.False);
            Assert.That(Read(In("green_hills"), Start, 12, EffectLevels.Of(new GameSettings { LivingWorld = 0 }, false)).Strength, Is.EqualTo(0));
        }

        [Test]
        public void ABossDimsTheWeatherAndAlarmsTheWildlifeAndAGuardianHidesIt()
        {
            var state = In("green_hills");
            Assert.That(Read(state, Start).Mood, Is.EqualTo(LifeMood.Calm));
            Assert.That(Read(state, Start).Strength, Is.EqualTo(1));

            state.SpawnEnemy(null, EnemyType.Boss, Game.Core.Math.BigNumber.Create(1, 5));
            var boss = Read(state, Start);
            Assert.That(boss.Mood, Is.EqualTo(LifeMood.Alarmed));
            Assert.That(boss.Strength, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(boss.BackgroundCount, Is.EqualTo(6));

            state.SpawnEnemy(null, EnemyType.Guardian, Game.Core.Math.BigNumber.Create(1, 5));
            var guardian = Read(state, Start);
            Assert.That(guardian.Mood, Is.EqualTo(LifeMood.Hidden));
            Assert.That(guardian.BackgroundCount + guardian.GroundCount, Is.EqualTo(0));
        }

        [Test]
        public void RestBringsAVisitAndAnInputStreakSwaysTheGrassInThreeSteps()
        {
            var state = In("green_hills");
            var life = new BiomeLife(Rules);
            state.IdleTicks = 599;
            life.Read(state, Full, Start, 12);
            Assert.That(life.Mood, Is.EqualTo(LifeMood.Calm));
            state.IdleTicks = 600;
            life.Read(state, Full, Start, 12);
            life.Read(state, Full, Start, 12);
            Assert.That(life.Mood, Is.EqualTo(LifeMood.Friendly));
            Assert.That(life.TryTakeReaction(out var visit), Is.True);
            Assert.That(visit, Is.EqualTo(LifeReaction.RestVisit));
            Assert.That(life.TryTakeReaction(out _), Is.False, "one visit per rest");

            state.IdleTicks = 0;
            foreach (var (ticks, tier) in new[] { (0L, 0), (29L, 0), (30L, 1), (100L, 2), (300L, 3), (9000L, 3) })
            {
                state.InputStreakTicks = ticks;
                life.Read(state, Full, Start, 12);
                Assert.That(life.Sway, Is.EqualTo(tier), ticks.ToString());
            }
        }

        [Test]
        public void BattleEventsBecomeReactionsOnceEachAndNoneWhenReactionsAreOff()
        {
            var life = Read(In("green_hills"), Start);
            life.Note(GameEvent.ForStage(GameEventKind.BossStarted, 0, 10));
            life.Note(GameEvent.ForStage(GameEventKind.BossStarted, 0, 10));
            life.Note(GameEvent.ForStage(GameEventKind.BossDefeated, 0, 10));
            life.Note(GameEvent.ForStage(GameEventKind.Prestige, 0, 10));
            life.Note(GameEvent.ForStage(GameEventKind.EnemySpawned, 0, 10));
            life.Note(GameEvent.ForStage(GameEventKind.GoldGained, 0, 10));

            var taken = new List<LifeReaction>();
            while (life.TryTakeReaction(out var reaction))
                taken.Add(reaction);
            Assert.That(taken, Is.EquivalentTo(new[] { LifeReaction.BossOut, LifeReaction.BossDown, LifeReaction.PrestigeWave, LifeReaction.EnemyFlash }));

            var quiet = EffectLevels.Of(new GameSettings { WorldReactions = false }, false);
            var state = In("green_hills");
            state.SpawnEnemy(null, EnemyType.Boss, Game.Core.Math.BigNumber.Create(1, 5));
            state.InputStreakTicks = 500;
            var off = Read(state, Start, 12, quiet);
            off.Note(GameEvent.ForStage(GameEventKind.BossStarted, 0, 10));
            Assert.That(off.TryTakeReaction(out _), Is.False);
            Assert.That(off.Mood, Is.EqualTo(LifeMood.Calm));
            Assert.That(off.Sway, Is.EqualTo(0));
        }

        [Test]
        public void TheRainbowComesOnlyRightAfterRainAndTheAuroraOnlyAtNight()
        {
            var hills = Look("hills");
            var rain = hills.Weathers.ToList().IndexOf("rain");
            var after = FirstWith(hills, span => span.PreviousPick == rain && span.Pick != rain);
            Assert.That(Read(In("green_hills"), after + Minute).Special, Is.True);
            Assert.That(Read(In("green_hills"), after + 6 * Minute).Special, Is.False, "five minutes only");
            var dry = FirstWith(hills, span => span.PreviousPick != rain);
            Assert.That(Read(In("green_hills"), dry + Minute).Special, Is.False);
            Assert.That(Read(In("green_hills"), after + Minute, 12, EffectLevels.Of(new GameSettings { WeatherShown = false }, false)).Special, Is.False);

            Assert.That(Read(In("ice_peak"), Start, 23).Special, Is.True);
            Assert.That(Read(In("ice_peak"), Start, 12).Special, Is.False);
        }

        [Test]
        public void ARareSceneComesOnceInHalfAnHourToAnHourForTwentySecondsForEveryone()
        {
            var hills = Look("hills");
            var span = WorldWeather.RareAt(hills, Rules, Start);
            for (var i = 0; i < 100; i++)
            {
                Assert.That(span.EndMs - span.StartMs, Is.InRange(Rules.RareMinMs, Rules.RareMaxMs));
                span = WorldWeather.RareAt(hills, Rules, span.EndMs);
            }
            var playing = Read(In("green_hills"), span.StartMs + 10_000);
            Assert.That(playing.RareScene, Is.True);
            Assert.That(playing.RareProgress, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(playing.Look.Rare, Is.EqualTo("courierDragon"));
            Assert.That(Read(In("green_hills"), span.StartMs + 21_000).RareScene, Is.False);
            Assert.That(Read(In("green_hills"), span.StartMs + 10_000, 12, EffectLevels.Of(new GameSettings { LivingWorld = 0.4 }, false)).RareScene, Is.False, "scenes start at 50%");
            var boss = In("green_hills");
            boss.SpawnEnemy(null, EnemyType.Boss, Game.Core.Math.BigNumber.Create(1, 5));
            Assert.That(Read(boss, span.StartMs + 10_000).RareScene, Is.False, "not during a boss");
        }

        [Test]
        public void InsideTheRiftTheStormNeverChanges()
        {
            var state = In("green_hills");
            state.Rift.Inside = true;
            for (var step = 0; step < 30; step++)
            {
                var life = Read(state, Start + step * 20 * Minute, step % 24);
                Assert.That(life.Look.Id, Is.EqualTo("rift"));
                Assert.That(life.WeatherId, Is.EqualTo("riftStorm"));
                Assert.That(life.Special, Is.True);
                Assert.That(life.Light.IsNight, Is.True);
                Assert.That(life.BackgroundCount + life.GroundCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void BrokenDataIsRefusedWithAReason()
        {
            var text = TestData.ReadGameDataFile(BiomeLifeRules.FileName);
            var biomes = TestWorld.Real.Stages.Biomes;
            Assert.That(() => BiomeLifeRules.Load(text.Replace("\"green_hills\": \"hills\",", ""), biomes), Throws.InstanceOf<ContentException>().With.Message.Contains("green_hills"));
            Assert.That(() => BiomeLifeRules.Load(text.Replace("\"after\": \"rain\"", "\"after\": \"hail\""), biomes), Throws.InstanceOf<ContentException>().With.Message.Contains("hail"));
            Assert.That(() => BiomeLifeRules.Load(text.Replace("\"maxMinutes\": 30", "\"maxMinutes\": 20"), biomes), Throws.InstanceOf<ContentException>());
            Assert.That(() => BiomeLifeRules.Load(text.Replace("\"rift\": \"rift\"", "\"rift\": \"void\""), biomes), Throws.InstanceOf<ContentException>().With.Message.Contains("void"));
        }

        [Test]
        public void ReadingEveryTickDoesNotAllocate()
        {
            var state = In("green_hills");
            var life = new BiomeLife(Rules);
            var levels = Full;
            var boss = GameEvent.ForStage(GameEventKind.BossStarted, 0, 10);
            life.Read(state, levels, Start, 12);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                state.IdleTicks = i % 700;
                state.InputStreakTicks = i;
                life.Note(boss);
                life.Read(state, levels, Start + i * 37_000L, (i / 60.0) % 24);
                life.TryTakeReaction(out _);
                if (life.BackgroundCount > 0)
                    life.BackgroundAt(0);
            }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
