using System.Collections.Generic;
using Game.Core.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class GameDataStoreTests
    {
        private static Dictionary<string, string> RealFiles()
        {
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
                files[name] = TestData.ReadGameDataFile(name);
            return files;
        }

        [Test]
        public void LoadsAllRealFilesTogether()
        {
            var files = RealFiles();
            var data = GameData.Load(name => files[name]);

            Assert.That(data.Stages.EnemiesPerStage, Is.EqualTo(10));
            Assert.That(data.Enemies.Normals(data.Stages.Biome(1)).Count, Is.GreaterThan(0));
            Assert.That(data.InputCaps.Keys.FullRatePerSecond, Is.GreaterThan(0));
        }

        [Test]
        public void ChangedNumberIsPickedUpOnReload()
        {
            var files = RealFiles();
            var store = new GameDataStore(name => files[name]);
            GameData? reloaded = null;
            store.Reloaded += data => reloaded = data;

            files[StageTable.FileName] = files[StageTable.FileName].Replace("\"timerSeconds\": 30", "\"timerSeconds\": 45");

            Assert.That(store.TryReload(), Is.True);
            Assert.That(store.Current.Stages.BossTimerSeconds, Is.EqualTo(45));
            Assert.That(reloaded, Is.SameAs(store.Current));
            Assert.That(store.Version, Is.EqualTo(2));
        }

        [Test]
        public void BrokenEditKeepsPreviousDataAndExplains()
        {
            var files = RealFiles();
            var store = new GameDataStore(name => files[name]);
            var before = store.Current;

            files[EnemyCatalog.FileName] = files[EnemyCatalog.FileName].Replace("\"type\": \"boss\"", "\"type\": \"elite\"");

            Assert.That(store.TryReload(), Is.False);
            Assert.That(store.Current, Is.SameAs(before));
            Assert.That(store.LastError, Does.Contain("enemies.json: enemies[6].type: unknown type 'elite'"));
            Assert.That(store.Version, Is.EqualTo(1));
        }

        [Test]
        public void EnemyBiomesAreCheckedAgainstStages()
        {
            var files = RealFiles();
            files[StageTable.FileName] = files[StageTable.FileName].Replace("\"green_hills\", ", string.Empty);

            var error = Assert.Throws<ContentException>(() => GameData.Load(name => files[name]))!;

            Assert.That(error.Message, Does.Contain("unknown biome 'green_hills'"));
        }
    }
}
