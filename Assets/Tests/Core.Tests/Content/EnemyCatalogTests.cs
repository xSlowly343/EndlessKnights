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
      ""modifiers"": [ { ""stat"": ""attackPower"", ""kind"": ""more"", ""value"": -0.5, ""tags"": [""Strike""] } ] }
  ]
}";

        private static EnemyCatalog Load(string json) => EnemyCatalog.Load(json, Biomes);

        [Test]
        public void RealDataFileHasEveryBiomeWithEnemiesBossesAndAGuardian()
        {
            var stages = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));
            var catalog = EnemyCatalog.Load(TestData.ReadGameDataFile(EnemyCatalog.FileName), stages.Biomes);

            Assert.That(stages.Biomes.Count, Is.EqualTo(6));
            Assert.That(catalog.Normals("green_hills").Count, Is.EqualTo(7));
            Assert.That(catalog.Boss("green_hills")!.Id, Is.EqualTo("boar_king"));
            Assert.That(catalog.Guardian("green_hills")!.Id, Is.EqualTo("ancient_ent"));
            foreach (var biome in stages.Biomes)
            {
                Assert.That(catalog.Normals(biome).Count, Is.GreaterThanOrEqualTo(4), biome);
                Assert.That(catalog.Guardian(biome), Is.Not.Null, biome);
                for (var slot = 1; slot <= 4; slot++)
                    Assert.That(catalog.BossAt(biome, slot)!.Slot, Is.EqualTo(slot), biome + " slot " + slot);
            }
        }

        [Test]
        public void TheBossOfAStageComesFromItsSlotAndTheHeartJoinsAtStageTwentyThousand()
        {
            var stages = StageTable.Load(TestData.ReadGameDataFile(StageTable.FileName));
            var catalog = EnemyCatalog.Load(TestData.ReadGameDataFile(EnemyCatalog.FileName), stages.Biomes);

            Assert.That(stages.BossSlot(1), Is.EqualTo(0));
            Assert.That(stages.BossSlot(10), Is.EqualTo(1));
            Assert.That(stages.BossSlot(49), Is.EqualTo(4));
            Assert.That(stages.BossSlot(60), Is.EqualTo(1));
            Assert.That(catalog.BossAt(stages.Biome(10), stages.BossSlot(10))!.Id, Is.EqualTo("gryzlo"));
            Assert.That(catalog.BossAt(stages.Biome(5), stages.BossSlot(5))!.Id, Is.EqualTo("boar_king"));
            Assert.That(catalog.BossAt(stages.Biome(155), stages.BossSlot(155))!.Id, Is.EqualTo("jackal_treasurer"), "no slot 0 falls back to the first boss");
            Assert.That(catalog.BossAt(stages.Biome(180), stages.BossSlot(180))!.Id, Is.EqualTo("pincer"));
            Assert.That(stages.Biome(19_999), Is.Not.EqualTo("rift_heart"));
            Assert.That(stages.Pool, Does.Contain("rift_heart"));
            Assert.That(stages.OpensAt(stages.Pool.Count - 1), Is.EqualTo(20_000));
            Assert.That(stages.Biome(20_051), Is.EqualTo("rift_heart"));
            Assert.That(catalog.Guardian("rift_heart")!.Id, Is.EqualTo("quiet_storm_dragon"));
        }

        [Test]
        public void TwoBossesInOneSlotAreRejected()
        {
            var json = Valid.Replace(@"""type"": ""guardian""", @"""type"": ""boss""");
            Assert.Throws<ContentException>(() => Load(json));
        }

        [Test]
        public void GuardianCarriesItsModifier()
        {
            var guardian = Load(Valid).Guardian("green_hills")!;
            var modifier = guardian.Modifiers.Single();

            Assert.That(modifier.Stat, Is.EqualTo(new StatId("attackPower")));
            Assert.That(modifier.Kind, Is.EqualTo(ModifierKind.More));
            Assert.That(modifier.Value, Is.EqualTo(-0.5));
            Assert.That(modifier.Tags, Is.EqualTo(StatTags.Strike));
        }

        [Test]
        public void GuardianModifierHalvesKeyboardDamageOnly()
        {
            var sheet = new StatSheet();
            sheet.AddRange(Load(Valid).Guardian("green_hills")!.Modifiers);
            var damage = new StatId("attackPower");

            Assert.That(sheet.Compute(damage, 10, StatTags.Strike).ToDouble(), Is.EqualTo(5).Within(1e-9));
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
        [TestCase("[\"Strike\"]", "[\"Typing\"]", "enemies[2].modifiers[0].tags[0]: unknown tag 'Typing'")]
        [TestCase("\"type\": \"normal\"", "\"type\": \"boss\"", "enemies: biome 'green_hills' has no normal enemies")]
        [TestCase("\"hpMultiplier\": 1.5", "\"hpMultiplier\": 0", "enemies[2].hpMultiplier: 0 is outside the allowed range")]
        public void BrokenEnemiesAreReportedByPath(string original, string broken, string message)
        {
            var error = Assert.Throws<ContentException>(() => Load(Valid.Replace(original, broken)))!;

            Assert.That(error.Message, Does.Contain("enemies.json: " + message));
        }
    }
}
