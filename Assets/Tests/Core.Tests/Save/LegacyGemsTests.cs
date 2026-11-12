using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class LegacyGemsTests
    {
        private const int HeroLevel = 20;
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GemCatalog Gems => TestWorld.Data.Gems;

        private static GemDef Gem(string id)
        {
            Assert.That(Gems.TryGet(id, out var gem), Is.True, id);
            return gem;
        }

        private static GemDef Support(GemDef skill, bool open) =>
            Gems.All.First(gem => gem.Kind == GemKind.Support && gem.Support != null && gem.Socket <= 0 && gem.CanSupport(skill) && (gem.UnlockLevel <= HeroLevel) == open);

        private static GameState OldDuelist()
        {
            var state = new GameState { Class = HeroClass.Duelist, HeroLevel = HeroLevel };
            var own = Gem("duelist_hit_1");
            var hit = new SkillSlot(own) { Role = SkillSlotRole.Left };
            hit.Supports[0] = Support(own, open: true);
            hit.Supports[1] = Support(own, open: false);
            state.Skills.Add(hit);
            state.Skills.Add(new SkillSlot(Gem("runedancer_hit_1")) { Role = SkillSlotRole.Right });
            state.Skills.Add(new SkillSlot(Gem("golden_fever")) { Role = SkillSlotRole.Aura });
            state.ProgressOf(own).Level = 7;
            state.OwnedGems.AddRange(new[] { own, Gem("runedancer_hit_1"), Gem("golden_fever"), Support(own, open: true), Gem("lull") });
            state.OwnedGems.Add(Gems.All.First(gem => gem.Ascendancy == "runedancer_trait_1"));
            return state;
        }

        private static string AsVersion17(GameState state)
        {
            var text = SaveCodec.Encode(state, Clock)
                .Replace("\"version\": " + SaveCodec.CurrentVersion, "\"version\": 17")
                .Replace("\"legacyGems\": false,", string.Empty);
            var start = text.IndexOf("\"payload\": ") + "\"payload\": ".Length;
            var payload = text.Substring(start, text.LastIndexOf('}') - start).TrimEnd();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hex = System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty).ToLowerInvariant();
            var at = text.IndexOf("\"checksum\": \"") + "\"checksum\": \"".Length;
            return text.Substring(0, at) + hex + text.Substring(at + 64);
        }

        private static GameLoop LoadOld()
        {
            var loaded = SaveCodec.Decode(AsVersion17(OldDuelist()), TestWorld.Data).State;
            Assert.That(loaded.LegacyGems, Is.True);
            return new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), loaded, new StatSheet());
        }

        [Test]
        public void AnOldSaveKeepsOnlySkillsTheHeroMayUseNow()
        {
            var loop = LoadOld();

            Assert.That(loop.State.Skills.Select(slot => slot.Skill.Root.Id), Is.EqualTo(new[] { "duelist_hit_1" }), "another class and a common skill above the hero level leave the Book");
            Assert.That(loop.State.LegacyGems, Is.False);
        }

        [Test]
        public void SkillLevelsAndOpenSupportsStay()
        {
            var loop = LoadOld();
            var hit = loop.State.Skills.Single();

            Assert.That(loop.State.ProgressOf(hit.Skill).Level, Is.EqualTo(7));
            Assert.That(hit.Supports[0], Is.SameAs(Support(hit.Skill, open: true)));
            Assert.That(hit.Supports[1], Is.Null, "a support above the hero level leaves its socket");
        }

        [Test]
        public void OwnedGemsKeepOnlyTheSkillOfTheChosenPath()
        {
            var loop = LoadOld();

            Assert.That(loop.State.OwnedGems.Where(gem => gem.Kind != GemKind.Skill || gem.Ascendancy == null), Is.Empty);
            Assert.That(loop.State.OwnedGems.Any(gem => gem.Ascendancy == "runedancer_trait_1"), Is.False);
        }

        [Test]
        public void ANewSaveIsNotCleaned()
        {
            var state = OldDuelist();
            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, Clock), TestWorld.Data).State;

            Assert.That(loaded.LegacyGems, Is.False);
            Assert.That(loaded.Skills.Count, Is.EqualTo(3));
        }
    }
}
