using System.Linq;
using Game.Core.Content;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class EnemyCatalogTests
    {
        private static readonly string[] Biomes = { "green_hills", "sand_ruins" };

        private const string Valid = @"{
  ""version"": 1,
  ""enemies"": [
    { ""id"": ""goblin"", ""biome"": ""green_hills"", ""type"": ""normal"", ""hpMultiplier"": 1, ""goldMultiplier"": 1, ""sprite"": ""s1"" },
    { ""id"": ""boar_king"", ""biome"": ""green_hills"", ""type"": ""boss"", ""hpMultiplier"": 1, ""goldMultiplier"": 1, ""sprite"": ""s2"" },
    { ""id"": ""ent"", ""biome"": ""green_hills"", ""type"": ""guardian"", ""hpMultiplier"": 1.5, ""goldMultiplier"": 2, ""sprite"": ""s3"",
      ""modifiers"": [ { ""stat"": ""damage"", ""kind"": ""more"", ""value"": -0.5, ""tags"": [""Keyboard""] } ] }
  ]
}";

        private static EnemyCatalog Load(string json) => EnemyCatalog.Load(json, Biomes);

        [Test]
        public void RealDataFileHasGreenHillsSetForMvp()
        {
            var stages = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));
            var catalog = EnemyCatalog.Load(TestData.ReadGameDataFile(EnemyCatalog.FileName), new[] { stages.Biome(1) });

            Assert.That(catalog.Normals("green_hills").Count, Is.EqualTo(6));
            Assert.That(catalog.Boss("green_hills")!.Id, Is.EqualTo("boar_king"));
            Assert.That(catalog.Guardian("green_hills")!.Id, Is.EqualTo("ancient_ent"));
        }

        [Test]
        public void GuardianCarriesItsModifier()
        {
            var guardian = Load(Valid).Guardian("green_hills")!;
            var modifier = guardian.Modifiers.Single();

            Assert.That(modifier.Stat, Is.EqualTo(new StatId("damage")));
            Assert.That(modifier.Kind, Is.EqualTo(ModifierKind.More));
            Assert.That(modifier.Value, Is.EqualTo(-0.5));
            Assert.That(modifier.Tags, Is.EqualTo(StatTags.Keyboard));
        }

        [Test]
        public void GuardianModifierHalvesKeyboardDamageOnly()
        {
            var sheet = new StatSheet();
            sheet.AddRange(Load(Valid).Guardian("green_hills")!.Modifiers);
            var damage = new StatId("damage");

            Assert.That(sheet.Compute(damage, 10, StatTags.Keyboard).ToDouble(), Is.EqualTo(5).Within(1e-9));
            Assert.That(sheet.Compute(damage, 10, StatTags.Click).ToDouble(), Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void LookupsWork()
        {
            var catalog = Load(Valid);

            Assert.That(catalog["goblin"].Type, Is.EqualTo(EnemyType.Normal));
            Assert.That(catalog.All.Count, Is.EqualTo(3));
            Assert.That(catalog.Normals("sand_ruins"), Is.Empty);
            Assert.That(catalog.Boss("sand_ruins"), Is.Null);
        }

        [TestCase("\"biome\": \"green_hills\", \"type\": \"boss\"", "\"biome\": \"moon\", \"type\": \"boss\"", "enemies[1].biome: unknown biome 'moon'")]
        [TestCase("\"type\": \"boss\"", "\"type\": \"elite\"", "enemies[1].type: unknown type 'elite'")]
        [TestCase("\"id\": \"boar_king\"", "\"id\": \"goblin\"", "enemies[1].id: enemy id 'goblin' is used twice")]
        [TestCase("\"kind\": \"more\"", "\"kind\": \"times\"", "enemies[2].modifiers[0].kind: unknown modifier kind 'times'")]
        [TestCase("[\"Keyboard\"]", "[\"Typing\"]", "enemies[2].modifiers[0].tags[0]: unknown tag 'Typing'")]
        [TestCase("\"type\": \"normal\"", "\"type\": \"boss\"", "enemies: biome 'green_hills' has no normal enemies")]
        [TestCase("\"hpMultiplier\": 1.5", "\"hpMultiplier\": 0", "enemies[2].hpMultiplier: 0 is outside the allowed range")]
        public void BrokenEnemiesAreReportedByPath(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => Load(Valid.Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("enemies.json: " + message));
        }
    }
}
