using System;
using System.IO;
using Game.Core.Clock;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class SaveStoreTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 1000, 1));

        private string _directory = string.Empty;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ek-save-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void RemoveDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private static string SaveAtTick(long tick) => SaveCodec.Encode(new GameState { Tick = tick }, Clock);

        private static SaveFile Decode(string text) => SaveCodec.Decode(text, TestWorld.Data.Enemies);

        [Test]
        public void WrittenSaveLoadsBack()
        {
            var store = new SaveStore(_directory);

            store.Write(SaveAtTick(42));
            var result = store.Load(Decode);

            Assert.That(result.Found, Is.True);
            Assert.That(result.Save!.State.Tick, Is.EqualTo(42));
            Assert.That(result.Path, Is.EqualTo(store.MainPath));
            Assert.That(File.Exists(store.TempPath), Is.False);
        }

        [Test]
        public void NothingSavedYet()
        {
            var result = new SaveStore(_directory).Load(Decode);

            Assert.That(result.Found, Is.False);
            Assert.That(result.Problems, Is.Empty);
        }

        [Test]
        public void ThreeBackupsRotate()
        {
            var store = new SaveStore(_directory, backupRotationSeconds: 0);

            for (var tick = 1; tick <= 6; tick++)
                store.Write(SaveAtTick(tick));

            Assert.That(Decode(File.ReadAllText(store.MainPath)).State.Tick, Is.EqualTo(6));
            Assert.That(Decode(File.ReadAllText(store.BackupPath(1))).State.Tick, Is.EqualTo(5));
            Assert.That(Decode(File.ReadAllText(store.BackupPath(2))).State.Tick, Is.EqualTo(4));
            Assert.That(Decode(File.ReadAllText(store.BackupPath(3))).State.Tick, Is.EqualTo(3));
            Assert.That(File.Exists(store.BackupPath(4)), Is.False);
        }

        [Test]
        public void OlderBackupsOnlyShiftAfterTheRotationInterval()
        {
            var store = new SaveStore(_directory);

            for (var tick = 1; tick <= 5; tick++)
                store.Write(SaveAtTick(tick));

            Assert.That(Decode(File.ReadAllText(store.BackupPath(1))).State.Tick, Is.EqualTo(4));
            Assert.That(Decode(File.ReadAllText(store.BackupPath(2))).State.Tick, Is.EqualTo(1));
            Assert.That(File.Exists(store.BackupPath(3)), Is.False);
        }

        [Test]
        public void DamagedMainSaveFallsBackToBackup()
        {
            var store = new SaveStore(_directory, backupRotationSeconds: 0);
            store.Write(SaveAtTick(10));
            store.Write(SaveAtTick(11));
            var text = File.ReadAllText(store.MainPath);
            File.WriteAllText(store.MainPath, text.Substring(0, text.Length / 3));

            var result = store.Load(Decode);

            Assert.That(result.Save!.State.Tick, Is.EqualTo(10));
            Assert.That(result.Path, Is.EqualTo(store.BackupPath(1)));
            Assert.That(result.Problems.Count, Is.EqualTo(1));
            Assert.That(result.Problems[0], Does.StartWith("save.json: "));
        }

        [Test]
        public void CompleteTempFileFromAnInterruptedWriteIsUsedWhenNewer()
        {
            var store = new SaveStore(_directory);
            store.Write(SaveAtTick(10));
            File.WriteAllText(store.TempPath, SaveAtTick(12));

            var result = store.Load(Decode);

            Assert.That(result.Save!.State.Tick, Is.EqualTo(12));
            Assert.That(result.Path, Is.EqualTo(store.TempPath));
        }

        [Test]
        public void HalfWrittenTempFileIsIgnored()
        {
            var store = new SaveStore(_directory);
            store.Write(SaveAtTick(10));
            var text = SaveAtTick(12);
            File.WriteAllText(store.TempPath, text.Substring(0, text.Length / 2));

            var result = store.Load(Decode);

            Assert.That(result.Save!.State.Tick, Is.EqualTo(10));
            Assert.That(result.Problems.Count, Is.EqualTo(1));
        }

        [Test]
        public void NextWriteReplacesLeftoverTempFile()
        {
            var store = new SaveStore(_directory);
            store.Write(SaveAtTick(10));
            File.WriteAllText(store.TempPath, "garbage");

            store.Write(SaveAtTick(11));

            Assert.That(File.Exists(store.TempPath), Is.False);
            Assert.That(store.Load(Decode).Save!.State.Tick, Is.EqualTo(11));
        }
    }
}
