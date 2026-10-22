using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class SaveCodecTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameLoop PlayedLoop(HeroClass heroClass, int ticks, FakeInputSource? input = null)
        {
            input ??= new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState(5) { Class = heroClass });
            Play(loop, input, ticks);
            return loop;
        }

        private static void Play(GameLoop loop, FakeInputSource input, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                if (tick % 2 == 0)
                    input.PressKeys(1);
                if (tick % 5 == 0)
                    input.ClickLeft();
                input.MoveMouse(120);
                loop.Tick();
                loop.Events.Clear();
            }
        }

        private static SaveFile RoundTrip(GameState state) => SaveCodec.Decode(SaveCodec.Encode(state, Clock), TestWorld.Data);

        [TestCase(HeroClass.Typist)]
        [TestCase(HeroClass.Bard)]
        public void SaveThenLoadGivesEqualState(HeroClass heroClass)
        {
            var state = PlayedLoop(heroClass, 3000).State;

            var loaded = RoundTrip(state).State;

            Assert.That(SaveCodec.Encode(loaded, Clock), Is.EqualTo(SaveCodec.Encode(state, Clock)));
            Assert.That(loaded.Stage, Is.EqualTo(state.Stage));
            Assert.That(loaded.Gold, Is.EqualTo(state.Gold));
            Assert.That(loaded.EnemyHp, Is.EqualTo(state.EnemyHp));
            Assert.That(loaded.Enemy, Is.SameAs(state.Enemy));
            Assert.That(loaded.Focus, Is.EqualTo(state.Focus));
            Assert.That(loaded.Rng.State, Is.EqualTo(state.Rng.State));
            Assert.That(loaded.Class, Is.EqualTo(heroClass));
        }

        [Test]
        public void LoadedGameContinuesExactlyLikeTheOriginal()
        {
            var input = new FakeInputSource();
            var original = PlayedLoop(HeroClass.Typist, 2000, input);
            var restoredInput = new FakeInputSource();
            var restored = TestWorld.Loop(restoredInput, RoundTrip(original.State).State);

            Play(original, input, 2000);
            Play(restored, restoredInput, 2000);

            Assert.That(SaveCodec.Encode(restored.State, Clock), Is.EqualTo(SaveCodec.Encode(original.State, Clock)));
        }

        [Test]
        public void ClockStampIsKept()
        {
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 99, 3, serverUnixMs: 1_760_000_000_500)).Next(new ClockReading(1_750_000_000_000, 120, 3));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(new GameState(), clock), TestWorld.Data);

            Assert.That(loaded.Clock.Reading.WallUnixMs, Is.EqualTo(1_750_000_000_000));
            Assert.That(loaded.Clock.HighestWallUnixMs, Is.EqualTo(1_760_000_000_000));
            Assert.That(loaded.Clock.Reading.ServerUnixMs, Is.Null);
            Assert.That(loaded.SavedAtUnixMs, Is.EqualTo(1_750_000_000_000));
            Assert.That(loaded.StoredVersion, Is.EqualTo(SaveCodec.CurrentVersion));
        }

        [Test]
        public void AwayTimeComesFromTheSavedClock()
        {
            var loaded = SaveCodec.Decode(SaveCodec.Encode(new GameState(), Clock), TestWorld.Data);

            var away = loaded.AwayTime(new ClockReading(Clock.Reading.WallUnixMs + 7_200_000, Clock.Reading.MonotonicMs + 7_200_000, Clock.Reading.BootId));

            Assert.That(away.Seconds, Is.EqualTo(7200));
            Assert.That(away.Source, Is.EqualTo(ElapsedSource.Monotonic));
        }

        [Test]
        public void HugeNumbersSurvive()
        {
            var state = new GameState { Gold = BigNumber.Create(1.2345678901234, 4321), RunGoldEarned = BigNumber.Create(9.87, 5000) };

            var loaded = RoundTrip(state).State;

            Assert.That(loaded.Gold, Is.EqualTo(state.Gold));
            Assert.That(loaded.RunGoldEarned, Is.EqualTo(state.RunGoldEarned));
        }

        [Test]
        public void GuardianModifiersAreBackAfterLoadingMidFight()
        {
            var hero = new StatSheet();
            var state = new GameState { Stage = TestWorld.Data.Stages.BiomeLength, HighestStage = TestWorld.Data.Stages.BiomeLength };
            state.SpawnEnemy(TestWorld.Data.Enemies.Guardian(TestWorld.Data.Stages.Biome(state.Stage)), EnemyType.Guardian, BigNumber.FromDouble(1e9));

            var loop = TestWorld.Loop(state: RoundTrip(state).State, hero: hero);

            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Guardian));
            Assert.That(hero.Compute(CombatStats.Damage, 10, StatTags.Keyboard).ToDouble(), Is.EqualTo(5).Within(1e-9));
        }

        [Test]
        public void EditedSaveIsRejected()
        {
            var text = SaveCodec.Encode(PlayedLoop(HeroClass.Typist, 500).State, Clock);
            var edited = text.Replace("\"current\": ", "\"current\": 9");

            var error = Assert.Throws<ContentException>(() => SaveCodec.Decode(edited, TestWorld.Data))!;

            Assert.That(error.Message, Does.Contain("save.json: checksum: does not match"));
        }

        [Test]
        public void CutSaveIsRejected()
        {
            var text = SaveCodec.Encode(PlayedLoop(HeroClass.Typist, 500).State, Clock);

            Assert.Throws<ContentException>(() => SaveCodec.Decode(text.Substring(0, text.Length / 2), TestWorld.Data));
        }

        [Test]
        public void SaveFromNewerGameIsRejected()
        {
            var text = SaveCodec.Encode(new GameState(), Clock).Replace("\"version\": " + SaveCodec.CurrentVersion, "\"version\": 99");
            var payloadStart = text.IndexOf("\"payload\": ") + "\"payload\": ".Length;
            var payload = text.Substring(payloadStart, text.LastIndexOf('}') - payloadStart).TrimEnd();
            var resigned = ReplaceChecksum(text, payload);

            var error = Assert.Throws<ContentException>(() => SaveCodec.Decode(resigned, TestWorld.Data))!;

            Assert.That(error.Message, Does.Contain("payload.version: version 99 is newer"));
        }

        [Test]
        public void UnknownEnemyIdFromOldContentBecomesUnnamedEnemy()
        {
            var state = new GameState();
            state.SpawnEnemy(new EnemyDef("removed_enemy", "green_hills", EnemyType.Normal, 1, 1, "x", new Modifier[0]), EnemyType.Normal, BigNumber.FromDouble(50));

            var loaded = RoundTrip(state).State;

            Assert.That(loaded.Enemy, Is.Null);
            Assert.That(loaded.EnemyHp.ToDouble(), Is.EqualTo(50));
        }

        [Test]
        public void WriterAndParserAgree()
        {
            var text = SaveCodec.Encode(PlayedLoop(HeroClass.Bard, 300).State, Clock);

            var parsed = JsonParser.Parse(text, SaveCodec.FileName);

            Assert.That(parsed.Fields.ContainsKey("checksum"), Is.True);
            Assert.That(parsed.Fields["payload"].Fields["state"].Fields["hero"].Fields["class"].Text, Is.EqualTo("bard"));
        }

        private static string ReplaceChecksum(string text, string payload)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var hex = System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            var start = text.IndexOf("\"checksum\": \"") + "\"checksum\": \"".Length;
            return text.Substring(0, start) + hex + text.Substring(start + 64);
        }
    }
}
