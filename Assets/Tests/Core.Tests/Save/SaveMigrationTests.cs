using System;
using System.Linq;
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
            var save = SaveCodec.Decode(OldSave(), TestWorld.Data);

            Assert.That(save.StoredVersion, Is.EqualTo(1));
            Assert.That(save.State.Class, Is.EqualTo(HeroClass.Metalhead));
            Assert.That(save.State.Stage, Is.EqualTo(46));
            Assert.That(save.State.Enemy!.Id, Is.EqualTo("boar_king"));
            Assert.That(save.State.Gold.ToDouble(), Is.EqualTo(18145.01873863374).Within(1e-6));
            Assert.That(save.SavedAtUnixMs, Is.EqualTo(1761134400000));
            Assert.That(save.State.Skills, Is.Empty);
            Assert.That(save.State.ChainDamage.IsZero, Is.True);
        }

        [Test]
        public void SecondVersionSaveWithSkillsStillLoads()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data);

            Assert.That(save.StoredVersion, Is.EqualTo(2));
            Assert.That(save.State.Stage, Is.EqualTo(38));
            Assert.That(save.State.Skills.Count, Is.EqualTo(3));
            Assert.That(save.State.Skills[0].Skill.Id, Is.EqualTo("stenography"));
            Assert.That(save.State.Skills[0].Trigger!.Id, Is.EqualTo("cast_on_timer"));
            Assert.That(save.State.Skills[0].HitsLeft, Is.EqualTo(18));
            Assert.That(save.State.Skills[0].CooldownTicks, Is.EqualTo(221));
            Assert.That(save.State.ChainDamage.ToDouble(), Is.EqualTo(480));
        }

        [Test]
        public void FourthVersionSaveWithTreeAndLevelStillLoads()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v4.json"), TestWorld.Data);

            Assert.That(save.StoredVersion, Is.EqualTo(4));
            Assert.That(save.State.Class, Is.EqualTo(HeroClass.Metalhead));
            Assert.That(save.State.HeroLevel, Is.EqualTo(15));
            Assert.That(save.State.Tree.StartClass, Is.EqualTo("metalhead"));
            Assert.That(save.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(save.State.Tree.MasteryChoices, Is.Empty);
            Assert.That(save.State.OwnedGems.Count, Is.EqualTo(3));
        }

        [Test]
        public void SixthVersionSaveWithPrestigePresetsAndStatsStillLoads()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v6.json"), TestWorld.Data);

            Assert.That(save.StoredVersion, Is.EqualTo(6));
            Assert.That(save.State.Prestiges, Is.EqualTo(1));
            Assert.That(save.State.RecordStage, Is.EqualTo(61));
            Assert.That(save.State.Artifacts.Count, Is.EqualTo(1));
            Assert.That(save.State.ActivePreset, Is.EqualTo(1));
            Assert.That(save.State.OtherPreset.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(save.State.TotalInput.Keys, Is.EqualTo(10000));
        }

        [Test]
        public void TypistBecomesSwordsmanWithEmptyTreesOnTheLargeTree()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v6.json"), TestWorld.Data);

            Assert.That(save.State.Class, Is.EqualTo(HeroClass.Ronin));
            Assert.That(save.State.Tree.StartClass, Is.EqualTo("ronin"));
            Assert.That(save.State.OtherPreset.Tree.StartClass, Is.EqualTo("ronin"));
            Assert.That(save.State.Tree.Allocated, Is.Empty);
            Assert.That(save.State.Ascendancy.Subclass, Is.Null);
        }

        [Test]
        public void BardSaveOpensAsMetalheadWithEnergyAsDrive()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v9.json"), TestWorld.Data);

            Assert.That(save.StoredVersion, Is.EqualTo(9));
            Assert.That(save.State.Class, Is.EqualTo(HeroClass.Metalhead));
            Assert.That(save.State.Resource, Is.EqualTo(55));
            Assert.That(save.State.Tree.StartClass, Is.EqualTo("metalhead"));
            Assert.That(save.State.OtherPreset.Tree.StartClass, Is.EqualTo("metalhead"));
            Assert.That(save.State.Tree.Allocated, Is.Empty);
            Assert.That(save.State.Tree.PointsEarned, Is.EqualTo(11));
            Assert.That(save.State.Skills[0].Skill.Id, Is.EqualTo("march"));
            Assert.That(save.State.HeroLevel, Is.EqualTo(12));
        }

        [Test]
        public void AlchemistSubclassPlagueDoctorBecomesPlaguebringer()
        {
            var payload = JsonParser.Parse(TestData.ReadSaveFixture("save_v9.json"), "save").Fields["payload"];
            var alchemist = JsonEdit.Update(payload, "state", state => JsonEdit.Update(state, "ascendancy", a =>
                JsonEdit.Set(JsonEdit.Set(a, "subclass", JsonValue.Of("plague_doctor")), "allocated", JsonValue.Array(new[] { JsonValue.Of("plague_doctor_1") }))));

            var ascendancy = SaveMigrations.Default.Steps[8].Upgrade(alchemist).Fields["state"].Fields["ascendancy"];

            Assert.That(ascendancy.Fields["subclass"].Text, Is.EqualTo("plague_bringer"));
            Assert.That(ascendancy.Fields["allocated"].Items.Single().Text, Is.EqualTo("plague_bringer_1"));
        }

        [TestCase("berserker")]
        [TestCase("hermit")]
        [TestCase("conductor")]
        [TestCase("seer")]
        [TestCase("bombardier")]
        public void SubclassThatStayedAPathKeepsItsChoiceButNotItsNodes(string subclass)
        {
            var ascendancy = UpgradedFromTenth(subclass);

            Assert.That(ascendancy.Fields["subclass"].Text, Is.EqualTo(subclass));
            Assert.That(ascendancy.Fields["allocated"].Items, Is.Empty);
        }

        [TestCase("duellist")]
        [TestCase("plague_bringer")]
        [TestCase("chronomancer")]
        public void SubclassThatIsNoLongerAPathIsCleared(string subclass)
        {
            var ascendancy = UpgradedFromTenth(subclass);

            Assert.That(ascendancy.Fields["subclass"].Kind, Is.EqualTo(JsonKind.Null));
            Assert.That(ascendancy.Fields["allocated"].Items, Is.Empty);
        }

        private static JsonValue UpgradedFromTenth(string subclass)
        {
            var payload = JsonParser.Parse(TestData.ReadSaveFixture("save_v9.json"), "save").Fields["payload"];
            var tenth = SaveMigrations.Default.Steps[8].Upgrade(payload);
            var chosen = JsonEdit.Update(tenth, "state", state => JsonEdit.Update(state, "ascendancy", a =>
                JsonEdit.Set(JsonEdit.Set(a, "subclass", JsonValue.Of(subclass)), "allocated", JsonValue.Array(new[] { JsonValue.Of(subclass + "_1") }))));
            return SaveMigrations.Default.Upgrade(chosen, 10).Fields["state"].Fields["ascendancy"];
        }

        [TestCase("swordsman", HeroClass.Ronin)]
        [TestCase("bard", HeroClass.Metalhead)]
        [TestCase("barbarian", HeroClass.Viking)]
        [TestCase("ranger", HeroClass.Amazon)]
        [TestCase("sorcerer", HeroClass.Stormweaver)]
        [TestCase("wanderer", HeroClass.Nomad)]
        [TestCase("priest", HeroClass.Oracle)]
        [TestCase("witch", HeroClass.Voodoo)]
        [TestCase("alchemist", HeroClass.PlagueDoctor)]
        public void EveryOldClassBecomesItsCharacter(string oldClass, HeroClass character)
        {
            var payload = JsonParser.Parse(TestData.ReadSaveFixture("save_v9.json"), "save").Fields["payload"];
            var renamed = JsonEdit.Update(payload, "state", state => JsonEdit.Update(state, "hero", hero => JsonEdit.Set(hero, "class", JsonValue.Of(oldClass))));

            var upgraded = SaveMigrations.Default.Upgrade(renamed, 9);

            Assert.That(upgraded.Fields["state"].Fields["hero"].Fields["class"].Text, Is.EqualTo(HeroClassNames.Of(character)));
        }

        [Test]
        public void OldSaveCanBePlayedAndSavedAgainInTheCurrentVersion()
        {
            var save = SaveCodec.Decode(OldSave(), TestWorld.Data);
            var savedTick = save.State.Tick;
            var loop = TestWorld.Loop(state: save.State);
            for (var i = 0; i < 100; i++)
                loop.Tick();

            var again = SaveCodec.Decode(SaveCodec.Encode(loop.State, save.Clock), TestWorld.Data);

            Assert.That(again.StoredVersion, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(again.State.Tick, Is.EqualTo(savedTick + 100));
        }

        [Test]
        public void MigrationsRunInOrderUpToTheCurrentVersion()
        {
            var calls = "";
            var steps = new System.Collections.Generic.List<SaveMigration>();
            foreach (var real in SaveMigrations.Default.Steps)
                steps.Add(new SaveMigration(real.FromVersion, real.Description, payload => { calls += real.FromVersion; return real.Upgrade(payload); }));
            var last = SaveMigrations.Default.CurrentVersion;
            steps.Insert(0, new SaveMigration(last, "double wallet", payload => { calls += last; return DoubleWallet(payload); }));
            var migrations = new SaveMigrations(last + 1, steps.ToArray());

            var save = SaveCodec.Decode(OldSave(), TestWorld.Data, migrations);

            Assert.That(calls, Is.EqualTo(string.Concat(System.Linq.Enumerable.Range(1, SaveMigrations.Default.CurrentVersion))));
            Assert.That(save.StoredVersion, Is.EqualTo(1));
            Assert.That(save.State.Gold.ToDouble(), Is.EqualTo(36290.0374772675).Within(1e-6));
            Assert.That(save.State.Skills, Is.Empty);
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

            var error = Assert.Throws<ContentException>(() => SaveCodec.Decode(OldSave(), TestWorld.Data, migrations))!;

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
