using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class DigTests
    {
        private static DigRules Rules => TestWorld.Real.Digs;
        private const float PixelsPerMeter = (float)(1 / InputStats.DefaultMetersPerPixel);

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        private static (GameLoop Loop, FakeInputSource Input) Digger()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.RealLoop(input);
            loop.State.RecordStage = 120;
            loop.Tick();
            loop.Camp.SetGuilds(new[] { "diggers" });
            return (loop, input);
        }

        private static void Guardian(GameLoop loop, string biome)
        {
            var guardian = TestWorld.Real.Enemies.All.First(enemy => enemy.Type == EnemyType.Guardian && enemy.Biome == biome);
            var first = loop.Events.Count;
            loop.Events.Add(GameEvent.ForEnemy(GameEventKind.BossDefeated, 0, 50, guardian));
            loop.Digs.NoteGuardians(first);
        }

        private static void Brush(GameLoop loop, FakeInputSource input, double metersPerTick)
        {
            for (var i = 0; i < 2000 && loop.State.Digs.Phase != DigPhase.Exposed; i++)
            {
                input.MoveMouse((float)(PixelsPerMeter * metersPerTick));
                loop.Tick();
            }
            Assert.That(loop.State.Digs.Phase, Is.EqualTo(DigPhase.Exposed));
        }

        private static void Camp(GameLoop loop)
        {
            loop.Peaceful.SetMode(PeacefulMode.Camp);
            loop.Peaceful.SetActivity(DigSystem.Activity);
        }

        [Test]
        public void BrokenDigRulesAreRejected()
        {
            var json = TestData.ReadGameDataFile(DigRules.FileName);
            var camp = TestWorld.Real.Camp;
            var biomes = TestWorld.Real.Stages.Biomes;
            Assert.That(DigRules.Load(json, camp, biomes).Sites.Count, Is.EqualTo(5));
            Assert.Throws<ContentException>(() => DigRules.Load(json.Replace("\"building\": \"dig_site\"", "\"building\": \"moat\""), camp, biomes));
            Assert.Throws<ContentException>(() => DigRules.Load(json.Replace("\"biome\": \"ice_peak\"", "\"biome\": \"green_hills\""), camp, biomes));
            Assert.Throws<ContentException>(() => DigRules.Load(json.Replace("\"maxSites\": 3", "\"maxSites\": 50"), camp, biomes));
        }

        [Test]
        public void AGuardianLeavesRemainsAndOnlyAFewWait()
        {
            var (loop, _) = Digger();
            Assert.That(DigView.Status(loop.Digs), Is.EqualTo("Раскапывать нечего: останки приходят после хозяина биома"));
            for (var i = 0; i < Rules.MaxSites + 2; i++)
                Guardian(loop, "sand_ruins");
            Assert.That(loop.State.Digs.Sites.Count, Is.EqualTo(Rules.MaxSites));
            Assert.That(loop.Digs.CurrentSite, Is.EqualTo("sand_ruins"));
        }

        [Test]
        public void SlowBrushingKeepsTheFindWholeAndAClickLiftsIt()
        {
            var (loop, input) = Digger();
            Guardian(loop, "green_hills");
            Camp(loop);
            Brush(loop, input, 0.2);
            Assert.That(loop.State.Digs.Quality, Is.EqualTo(1));
            Assert.That(DigView.Status(loop.Digs), Does.EndWith("кликни, чтобы достать"));
            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.State.Digs.Sites, Is.Empty);
            Assert.That(loop.State.Digs.Dug, Is.EqualTo(1));
            Assert.That(loop.Camp.Count("bone_hills"), Is.InRange(Rules.BonesMin, Rules.BonesMax));
        }

        [Test]
        public void FastBrushingCracksTheFind()
        {
            var (loop, input) = Digger();
            Guardian(loop, "green_hills");
            Camp(loop);
            Brush(loop, input, 1);
            Assert.That(loop.State.Digs.Cracks, Is.GreaterThan(0));
            Assert.That(loop.State.Digs.Quality, Is.LessThan(1));
            Assert.That(loop.State.Digs.Quality, Is.GreaterThanOrEqualTo(Rules.MinQuality));
        }

        [Test]
        public void TheTalentsOfShardcallerAndSoulbinderWork()
        {
            var talents = TestWorld.Real.Talents;
            Assert.That(talents.Of(HeroClass.Shardcaller)!.Works, Is.True);
            Assert.That(talents.Of(HeroClass.Soulbinder)!.Works, Is.True);
            var (loop, _) = Digger();
            loop.State.TalentPerks[(int)MuseumPerk.LighterBrush] = 0.2;
            Assert.That(loop.Digs.BrushNeeded, Is.EqualTo(Rules.BrushMeters * 0.8).Within(1e-9));
        }

        [Test]
        public void BonesBuildASkeletonForTheMuseumAndTheSaveKeepsTheDig()
        {
            var (loop, _) = Digger();
            var site = Rules.Site("green_hills")!;
            Assert.That(loop.Digs.Assemble("green_hills"), Is.EqualTo(DigResult.NotEnoughBones));
            loop.Camp.Add(site.Bone, Rules.SkeletonBones);
            Assert.That(loop.Digs.Assemble("green_hills"), Is.EqualTo(DigResult.Done));
            Assert.That(loop.Digs.Assemble("green_hills"), Is.EqualTo(DigResult.AlreadyBuilt));
            Assert.That(DigView.Skeletons(loop.Digs)[0], Is.EqualTo("Останки Короля-кабана: скелет собран"));
            foreach (var other in Rules.Sites.Skip(1))
            {
                loop.Camp.Add(other.Bone, Rules.SkeletonBones);
                loop.Digs.Assemble(other.Biome);
            }
            Guardian(loop, "ice_peak");
            loop.Museum.Check();
            Assert.That(loop.State.Museum.Done, Does.Contain("colossus_bones"));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Digs.Skeletons.Count, Is.EqualTo(Rules.Sites.Count));
            Assert.That(copy.Digs.Sites, Is.EqualTo(new[] { "ice_peak" }));
        }
    }
}
