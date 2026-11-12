using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SkillEvolutionTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop Runedancer(int heroLevel)
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = heroLevel });
            loop.Book.GrantStartingGems();
            return loop;
        }

        private static SkillSlot SlotOf(GameLoop loop, string family) => loop.State.Skills.Single(slot => slot.Skill.Root == Gem(family));

        [Test]
        public void ARunedancerHasFourFamiliesOfThreeForms()
        {
            var roots = TestWorld.Data.Gems.All.Where(gem => SkillBook.IsClassSkill(gem) && gem.Type == SkillType.Active && gem.Skill!.Weapon == "rune_daggers" && gem.Root == gem).ToList();

            Assert.That(roots.Select(gem => gem.Id), Is.EquivalentTo(new[] { "runedancer_hit_1", "runedancer_queue_1", "runedancer_auto_1", "runedancer_strong_1" }));
            Assert.That(roots.Count, Is.EqualTo(SkillRoster.FamiliesPerClass));
            Assert.That(roots.All(gem => gem.Forms.Count == 3), Is.True);
            Assert.That(Gem("runedancer_hit_1").Forms.Select(gem => gem.Id), Is.EqualTo(new[] { "runedancer_hit_1", "runedancer_hit_2", "runedancer_hit_3" }));
            Assert.That(Gem("runedancer_hit_3").ProgressId, Is.EqualTo("runedancer_hit_1"));
            Assert.That(Gem("runedancer_hit_3").PowerStat, Is.EqualTo(Gem("runedancer_hit_1").PowerStat));
            Assert.That(Gem("runedancer_aura_2").Evolves, Is.False);
            Assert.That(Gem("duelist_path_1").Evolves, Is.False);
        }

        [Test]
        public void EveryFormIsStrongerThanItsPredecessor()
        {
            foreach (var root in TestWorld.Data.Gems.All.Where(gem => gem.Evolves && gem.Root == gem))
            {
                for (var i = 1; i < root.Forms.Count; i++)
                {
                    var before = root.Forms[i - 1].Skill!;
                    var after = root.Forms[i].Skill!;
                    Assert.That(CastDamage(after), Is.GreaterThan(CastDamage(before)), root.Forms[i].Id + " per cast");
                    Assert.That(CastDamage(after) / after.CooldownSeconds, Is.GreaterThanOrEqualTo(CastDamage(before) / before.CooldownSeconds - 1e-9), root.Forms[i].Id + " per second");
                }
            }
        }

        private static double CastDamage(SkillDef skill) =>
            skill.Effect switch
            {
                StrikeEffect strike => strike.Multiplier * (1 + strike.QueueTargets),
                FlurryEffect flurry => flurry.Hits * flurry.Multiplier * (1 + flurry.QueueTargets),
                _ => throw new System.InvalidOperationException("a form deals damage by a strike or a flurry")
            };

        private static readonly string[] Fed =
        {
            "bait", "dream", "nets", "dozeSeconds", "light", "oil", "fireflies", "knots", "curse", "wind", "reagents", "layers", "corrode",
            "veinHits", "luck", "nuggets", "veinGold", "souls", "cannons", "sailors", "moths", "cards", "arcana", "seeds", "venom", "sealHits",
            "golemSeconds", "cutCooldowns"
        };

        [TestCase("dreamcatcher")]
        [TestCase("lanternbearer")]
        [TestCase("whisperer")]
        [TestCase("clockwright")]
        [TestCase("alchemist")]
        [TestCase("dowser")]
        [TestCase("soulbinder")]
        [TestCase("ghostcaptain")]
        [TestCase("mothcaller")]
        [TestCase("cartomancer")]
        [TestCase("herbwitch")]
        [TestCase("golemancer")]
        public void AFormKeepsWhatThePreviousFormFeeds(string character)
        {
            var gems = System.Text.Json.JsonDocument.Parse(TestData.ReadGameDataFile(GemCatalog.FileName)).RootElement.GetProperty("gems");
            var families = gems.EnumerateArray().Where(gem => gem.GetProperty("id").GetString()!.StartsWith(character + "_", System.StringComparison.Ordinal) && gem.TryGetProperty("evolutions", out _)).ToList();
            Assert.That(families, Has.Count.EqualTo(SkillRoster.FamiliesPerClass));
            foreach (var family in families)
            {
                var before = family.GetProperty("effect");
                foreach (var form in family.GetProperty("evolutions").EnumerateArray())
                {
                    var after = form.GetProperty("effect");
                    foreach (var field in Fed.Where(field => before.TryGetProperty(field, out _)))
                    {
                        Assert.That(after.TryGetProperty(field, out var kept), form.GetProperty("id").GetString() + " keeps " + field);
                        Assert.That(Amount(kept), Is.GreaterThanOrEqualTo(Amount(before.GetProperty(field))), form.GetProperty("id").GetString() + " " + field);
                    }
                    before = after;
                }
            }
        }

        [TestCase("lanternbearer_hit_1", 3)]
        [TestCase("clockwright_hit_1", 3)]
        public void TheFastFamilyKeepsTheStrikeThatGrowsWithWhatSheGathers(string id, double rider)
        {
            foreach (var form in Gem(id).Forms)
            {
                var strike = (StrikeEffect)form.Skill!.Effect;
                Assert.That(System.Math.Max(strike.LitHit, strike.WindHit), Is.GreaterThanOrEqualTo(rider), form.Id);
            }
        }

        private static double Amount(System.Text.Json.JsonElement value) =>
            value.ValueKind == System.Text.Json.JsonValueKind.True ? 1 : value.GetDouble();

        [Test]
        public void TheSkillInTheSlotEvolvesWhenTheHeroReachesTheLevelOfTheNextForm()
        {
            var loop = Runedancer(1);
            var slot = SlotOf(loop, "runedancer_hit_1");
            var role = slot.Role;
            var support = TestWorld.Data.Gems.All.First(gem => gem.Kind == GemKind.Support && gem.Support != null && gem.Socket <= 0 && gem.CanSupport(Gem("runedancer_hit_1")) && gem.CanSupport(Gem("runedancer_hit_2")));
            slot.Supports[0] = support;
            loop.State.ProgressOf(slot.Skill).Level = 9;
            loop.State.ProgressOf(slot.Skill).Branches[0] = 1;
            loop.State.HeroLevel = TestWorld.Data.SkillBook.SkillUnlockLevel(SkillTier.Rare) - 1;

            loop.Tick();
            Assert.That(slot.Skill, Is.SameAs(Gem("runedancer_hit_1")));
            Assert.That(loop.Book.NextFormOf(slot.Skill), Is.SameAs(Gem("runedancer_hit_2")));

            loop.State.HeroLevel++;
            loop.Tick();

            Assert.That(slot.Skill, Is.SameAs(Gem("runedancer_hit_2")));
            Assert.That(slot.Role, Is.EqualTo(role));
            Assert.That(slot.Supports[0], Is.SameAs(support));
            Assert.That(loop.State.LevelOf(slot.Skill), Is.EqualTo(9));
            Assert.That(slot.Def, Is.SameAs(Gem("runedancer_hit_2").VariantOf(SkillBranches.Index(1, SkillBranches.NoChoice, SkillBranches.NoChoice))), "the branch choice carries over to the new form");
            Assert.That(loop.State.Skills.Count(other => other.Skill.Root == Gem("runedancer_hit_1")), Is.EqualTo(1));
        }

        [Test]
        public void TheLastFormHasNoNextForm()
        {
            var loop = Runedancer(TestWorld.Data.SkillBook.SkillUnlockLevel(SkillTier.Mythic));

            Assert.That(SlotOf(loop, "runedancer_hit_1").Skill, Is.SameAs(Gem("runedancer_hit_3")));
            Assert.That(loop.Book.NextFormOf(Gem("runedancer_hit_1")), Is.Null);
            Assert.That(loop.Book.FormOf(Gem("runedancer_strong_1")), Is.SameAs(Gem("runedancer_strong_3")));
        }

        [Test]
        public void AFamilyTakesOneSlotAndAnyOfItsFormsPutsTheCurrentOne()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 50, RecordStage = 200 });

            Assert.That(loop.Book.Equip(Gem("runedancer_strong_1")), Is.EqualTo(BookResult.Done));

            Assert.That(SlotOf(loop, "runedancer_strong_1").Skill, Is.SameAs(Gem("runedancer_strong_2")));
            Assert.That(loop.Book.Equip(Gem("runedancer_strong_3")), Is.EqualTo(BookResult.AlreadyInBook));
            Assert.That(loop.Book.RowOf(Gem("runedancer_strong_1")), Is.EqualTo(0));
            Assert.That(loop.Book.SkillOpen(Gem("runedancer_strong_2")), Is.True);
            Assert.That(loop.Book.SkillOpen(Gem("runedancer_strong_1")), Is.False, "an outgrown form is not offered");
            Assert.That(loop.Book.SkillOpen(Gem("runedancer_strong_3")), Is.False);
        }

        [Test]
        public void ASkillInUseEvolvesAfterItEnds()
        {
            var loop = Runedancer(1);
            var slot = SlotOf(loop, "runedancer_hit_1");
            slot.ActiveTicks = 2;
            loop.State.HeroLevel = 15;

            loop.Book.Evolve();
            Assert.That(slot.Skill, Is.SameAs(Gem("runedancer_hit_1")));

            slot.ActiveTicks = 0;
            loop.Book.Evolve();
            Assert.That(slot.Skill, Is.SameAs(Gem("runedancer_hit_2")));
        }

        [Test]
        public void AnOldSaveWithSeveralRanksKeepsOneSlotAndTheBestProgress()
        {
            var state = new GameState { Class = HeroClass.Runedancer, HeroLevel = 20 };
            state.Skills.Add(new SkillSlot(Gem("runedancer_hit_1")) { Role = SkillSlotRole.Left });
            state.Skills.Add(new SkillSlot(Gem("runedancer_queue_1")) { Role = SkillSlotRole.Right });
            state.Skills.Add(new SkillSlot(Gem("runedancer_hit_2")) { Role = SkillSlotRole.Auto });
            state.GemProgress["runedancer_hit_1"] = new GemProgress { Level = 3 };
            state.GemProgress["runedancer_hit_2"] = new GemProgress { Level = 12, Experience = 5 };
            state.GemProgress["runedancer_hit_3"] = new GemProgress { Level = 12, Experience = 1 };

            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, Clock), TestWorld.Data).State;
            var loop = new GameLoop(TestWorld.Data, new FakeInputSource(), loaded, new StatSheet());

            Assert.That(loaded.GemProgress.Keys.Where(id => id.StartsWith("runedancer_hit")), Is.EqualTo(new[] { "runedancer_hit_1" }));
            Assert.That(loaded.LevelOf(Gem("runedancer_hit_2")), Is.EqualTo(12));
            Assert.That(loaded.GemProgress["runedancer_hit_1"].Experience, Is.EqualTo(5));
            Assert.That(loaded.Skills.Select(slot => slot.Skill.Id), Is.EqualTo(new[] { "runedancer_hit_2", "runedancer_queue_2" }));
            Assert.That(loaded.Skills[0].Role, Is.EqualTo(SkillSlotRole.Left));
            Assert.That(loop.Book.Equip(Gem("runedancer_hit_1")), Is.EqualTo(BookResult.AlreadyInBook));
        }

        [TestCase(HeroClass.Duelist)]
        [TestCase(HeroClass.Golemancer)]
        public void ChangingTheClassAndBackKeepsTheLevelOfAFamily(HeroClass other)
        {
            var loop = Runedancer(30);
            loop.State.ProgressOf(Gem("runedancer_hit_1")).Level = 9;

            Assert.That(loop.ChangeClassFree(other), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.GemProgress.Keys.Any(id => id.StartsWith("runedancer_")), Is.False);
            Assert.That(loop.State.Skills.All(slot => loop.State.LevelOf(slot.Skill) == 9 || slot.Skill.Skill!.Role != SkillRole.Fast), Is.True);

            Assert.That(loop.ChangeClassPaid(HeroClass.Runedancer), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.LevelOf(Gem("runedancer_hit_3")), Is.EqualTo(9));
            Assert.That(loop.State.Skills.Select(slot => slot.Skill.Root).Distinct().Count(), Is.EqualTo(loop.State.Skills.Count));
        }

        [Test]
        public void AnOldSaveWithARemovedAuraFreesItsPlace()
        {
            var state = new GameState { Class = HeroClass.Duelist, HeroLevel = 20 };
            state.Skills.Add(new SkillSlot(Gem("duelist_hit_1")) { Role = SkillSlotRole.Left });
            state.Skills.Add(new SkillSlot(Gem("duelist_aura_2")) { Role = SkillSlotRole.Aura });
            state.GemProgress["duelist_aura_2"] = new GemProgress { Level = 6 };
            var old = WithChecksum(SaveCodec.Encode(state, Clock).Replace("duelist_aura_2", "duelist_aura_1"));

            var loaded = SaveCodec.Decode(old, TestWorld.Data).State;
            var loop = new GameLoop(TestWorld.Data, new FakeInputSource(), loaded, new StatSheet());

            Assert.That(loop.State.Skills.Select(slot => slot.Skill.Root.Id), Is.EqualTo(new[] { "duelist_hit_1" }));
            Assert.That(loop.Book.Equip(Gem("duelist_aura_2")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.LevelOf(Gem("duelist_aura_2")), Is.EqualTo(1));
        }

        private static string WithChecksum(string text)
        {
            var start = text.IndexOf("\"payload\": ") + "\"payload\": ".Length;
            var payload = text.Substring(start, text.LastIndexOf('}') - start).TrimEnd();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hex = System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty).ToLowerInvariant();
            var at = text.IndexOf("\"checksum\": \"") + "\"checksum\": \"".Length;
            return text.Substring(0, at) + hex + text.Substring(at + 64);
        }

        [Test]
        public void EveryClassSkillPairsWithTheSameFormOfEveryOtherClass()
        {
            var weapons = TestWorld.Data.Weapons;
            foreach (var from in weapons.Types)
            {
                var skills = TestWorld.Data.Gems.All.Where(gem => gem.Skill?.Weapon == from.Id && gem.Ascendancy == null).ToList();
                foreach (var to in weapons.Types.Where(type => type != from))
                {
                    foreach (var gem in skills)
                    {
                        var pair = weapons.Reforge.Gem(gem, from, to);
                        Assert.That(pair, Is.Not.Null, gem.Id + " > " + to.Id);
                        Assert.That(pair!.Type, Is.EqualTo(gem.Type), gem.Id + " > " + to.Id);
                        Assert.That(pair.Root.Forms.ToList().IndexOf(pair), Is.EqualTo(gem.Root.Forms.ToList().IndexOf(gem)), gem.Id + " > " + pair.Id);
                        Assert.That(weapons.Reforge.Gem(gem.Root, from, to), Is.SameAs(pair.Root), gem.Id + " > " + pair.Id);
                    }
                }
            }
        }

        [Test]
        public void ChangingTheClassMovesTheLevelOfAFamilyToOneFamily()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Golemancer, HeroLevel = 30 });
            loop.Book.GrantStartingGems();
            loop.State.ProgressOf(Gem("golemancer_hit_2")).Level = 9;

            Assert.That(loop.ChangeClassFree(HeroClass.Dreamcatcher), Is.EqualTo(ClassChangeResult.Changed));

            Assert.That(loop.State.LevelOf(Gem("dreamcatcher_hit_1")), Is.EqualTo(9));
            Assert.That(loop.State.GemProgress.Count(pair => pair.Value.Level == 9), Is.EqualTo(1));

            Assert.That(loop.ChangeClassPaid(HeroClass.Golemancer), Is.EqualTo(ClassChangeResult.Changed));
            Assert.That(loop.State.LevelOf(Gem("golemancer_hit_3")), Is.EqualTo(9));
        }

        [Test]
        public void TheLoaderRejectsBrokenEvolutions()
        {
            var file = TestData.ReadGameDataFile(GemCatalog.FileName);
            const string second = "\"id\": \"runedancer_hit_2\",\n          \"kind\": \"active\",\n          \"weapon\": \"rune_daggers\",\n          \"tier\": \"rare\"";
            Assert.That(file, Does.Contain(second));

            var lowerTier = Assert.Throws<ContentException>(() => GemCatalog.Load(file.Replace(second, second.Replace("\"rare\"", "\"common\""))))!;
            var otherWeapon = Assert.Throws<ContentException>(() => GemCatalog.Load(file.Replace(second, second.Replace("rune_daggers", "spectral_rapier"))))!;

            Assert.That(lowerTier.Message, Does.Contain("higher tier"));
            Assert.That(otherWeapon.Message, Does.Contain("keeps its weapon"));
        }
    }
}
