using System;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class SaveMigrationTests
    {
        private static string OldSave() => TestData.ReadSaveFixture("save_v1.json");

        private static JsonValue DoubleWallet(JsonValue payload) =>
            JsonEdit.Update(payload, "state", state =>
                JsonEdit.Update(state, "gold", gold =>
                    JsonEdit.Set(gold, "wallet", JsonValue.Of("3.62900374772675e4"))));

        [Test]
        public void FirstReleasedSaveStillLoads()
        {
            var save = SaveCodec.Decode(OldSave(), TestWorld.Data.Enemies);

            Assert.That(save.StoredVersion, Is.EqualTo(1));
            Assert.That(save.State.Class, Is.EqualTo(HeroClass.Bard));
            Assert.That(save.State.Stage, Is.EqualTo(46));
            Assert.That(save.State.Enemy!.Id, Is.EqualTo("boar_king"));
            Assert.That(save.State.Gold.ToDouble(), Is.EqualTo(18145.01873863374).Within(1e-6));
            Assert.That(save.SavedAtUnixMs, Is.EqualTo(1761134400000));
        }

        [Test]
        public void OldSaveCanBePlayedAndSavedAgainInTheCurrentVersion()
        {
            var save = SaveCodec.Decode(OldSave(), TestWorld.Data.Enemies);
            var savedTick = save.State.Tick;
            var loop = TestWorld.Loop(state: save.State);
            for (var i = 0; i < 100; i++)
                loop.Tick();

            var again = SaveCodec.Decode(SaveCodec.Encode(loop.State, save.Clock), TestWorld.Data.Enemies);

            Assert.That(again.StoredVersion, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(again.State.Tick, Is.EqualTo(savedTick + 100));
        }

        [Test]
        public void MigrationsRunInOrderUpToTheCurrentVersion()
        {
            var calls = "";
            var migrations = new SaveMigrations(3,
                new SaveMigration(2, "second", payload => { calls += "2"; return payload; }),
                new SaveMigration(1, "double wallet", payload => { calls += "1"; return DoubleWallet(payload); }));

            var save = SaveCodec.Decode(OldSave(), TestWorld.Data.Enemies, migrations);

            Assert.That(calls, Is.EqualTo("12"));
            Assert.That(save.StoredVersion, Is.EqualTo(1));
            Assert.That(save.State.Gold.ToDouble(), Is.EqualTo(36290.0374772675).Within(1e-6));
        }

        [Test]
        public void CurrentSaveSkipsMigrations()
        {
            var calls = 0;
            var migrations = new SaveMigrations(2, new SaveMigration(1, "count", payload => { calls++; return payload; }));
            var payload = JsonEdit.Set(JsonValue.Object(new System.Collections.Generic.Dictionary<string, JsonValue>()), "version", JsonValue.Of(2));

            var upgraded = migrations.Upgrade(payload, 2);

            Assert.That(calls, Is.EqualTo(0));
            Assert.That(upgraded, Is.SameAs(payload));
        }

        [Test]
        public void UpgradeStampsEachStepVersion()
        {
            var seen = new System.Collections.Generic.List<double>();
            var migrations = new SaveMigrations(3,
                new SaveMigration(1, "a", payload => { seen.Add(payload.Fields["version"].Number); return payload; }),
                new SaveMigration(2, "b", payload => { seen.Add(payload.Fields["version"].Number); return payload; }));
            var payload = JsonEdit.Set(JsonValue.Object(new System.Collections.Generic.Dictionary<string, JsonValue>()), "version", JsonValue.Of(1));

            var upgraded = migrations.Upgrade(payload, 1);

            Assert.That(seen, Is.EqualTo(new[] { 1.0, 2.0 }));
            Assert.That(upgraded.Fields["version"].Number, Is.EqualTo(3));
        }

        [Test]
        public void MissingOrRepeatedStepsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new SaveMigrations(3, new SaveMigration(1, "only one", payload => payload)));
            Assert.Throws<ArgumentException>(() => new SaveMigrations(3,
                new SaveMigration(1, "a", payload => payload),
                new SaveMigration(1, "a again", payload => payload)));
        }

        [Test]
        public void BrokenMigrationResultIsReportedByPath()
        {
            var migrations = new SaveMigrations(2, new SaveMigration(1, "drop stage", payload =>
                JsonEdit.Update(payload, "state", state => JsonEdit.Remove(state, "stage"))));

            var error = Assert.Throws<ContentException>(() => SaveCodec.Decode(OldSave(), TestWorld.Data.Enemies, migrations))!;

            Assert.That(error.Message, Does.Contain("save.json: payload.state: required field 'stage' is missing"));
        }

        [Test]
        public void JsonEditRenamesFields()
        {
            var value = JsonEdit.Set(JsonValue.Object(new System.Collections.Generic.Dictionary<string, JsonValue>()), "old", JsonValue.Of(5));

            var renamed = JsonEdit.Rename(value, "old", "new");

            Assert.That(renamed.Fields.ContainsKey("old"), Is.False);
            Assert.That(renamed.Fields["new"].Number, Is.EqualTo(5));
            Assert.That(JsonEdit.Rename(renamed, "missing", "x"), Is.SameAs(renamed));
        }
    }
}
