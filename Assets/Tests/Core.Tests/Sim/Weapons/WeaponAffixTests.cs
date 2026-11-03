using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Weapons
{
    public class WeaponAffixTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameData Data => TestWorld.Real;
        private static ItemCatalog Items => Data.Items;
        private static GemDef Gem(string id) => Data.Gems[id];

        private static GameLoop Hero() =>
            new GameLoop(Data, new FakeInputSource(), TestWorld.WithAllSkillsOpen(new GameState(3) { Class = HeroClass.Runedancer, RecordStage = 300 }), new StatSheet());

        private static GearAffix Affix(string id, int tier = 6, string? support = null)
        {
            Assert.That(Items.TryGetAffix(id, out var def), Is.True, id);
            return new GearAffix(def, tier, def.ValueAt(tier, Items.Tiers), support == null ? null : Gem(support));
        }

        private static WeaponItem Hold(GameLoop loop, params GearAffix[] affixes)
        {
            var weapon = loop.Weapons.Give(Data.Weapons.Roll(Data.Weapons.StarterFor(HeroClass.Runedancer), ItemRarity.Legendary, 6, affixes));
            Assert.That(loop.Weapons.Equip(weapon), Is.True);
            return weapon;
        }

        private static int Row(GameLoop loop, string id) => loop.Book.RowOf(Gem(id));

        [TestCase(ItemRarity.Simple, 0)]
        [TestCase(ItemRarity.Uncommon, 2)]
        [TestCase(ItemRarity.Epic, 4)]
        [TestCase(ItemRarity.Rift, 6)]
        public void AWeaponRollsAffixesOnlyFromTheWeaponPool(ItemRarity rarity, int count)
        {
            var rng = new Rng(11);
            for (var i = 0; i < 50; i++)
            {
                var affixes = Items.RollAffixes(ItemSlot.Weapon, rarity, 6, rng);

                Assert.That(affixes.Count, Is.EqualTo(count));
                Assert.That(affixes.All(affix => affix.Def.FitsSlot(ItemSlot.Weapon) && affix.Def.Slots.Count == 1), Is.True);
                Assert.That(affixes.Select(affix => affix.Def.Id).Distinct().Count(), Is.EqualTo(count));
            }
        }

        [Test]
        public void LateWeaponAffixesWaitForTheirRarity()
        {
            var rng = new Rng(5);
            for (var i = 0; i < 300; i++)
            {
                foreach (var affix in Items.RollAffixes(ItemSlot.Weapon, ItemRarity.Uncommon, 6, rng))
                    Assert.That(affix.Def.FromRarity, Is.LessThanOrEqualTo(ItemRarity.Uncommon), affix.Def.Id);
                var epic = Items.RollAffixes(ItemSlot.Weapon, ItemRarity.Ancient, 6, rng);
                Assert.That(epic.Count(affix => affix.Def.Group == "weapon_role_level"), Is.LessThanOrEqualTo(1));
                Assert.That(epic.Where(affix => affix.Def.BuiltInRole != null).All(affix => affix.Support != null), Is.True);
            }
        }

        [Test]
        public void ArmorNeverRollsAWeaponAffix()
        {
            var rng = new Rng(9);
            for (var i = 0; i < 200; i++)
                Assert.That(Items.RollAffixes(ItemSlot.Gloves, ItemRarity.Rift, 6, rng).Any(affix => affix.Def.FitsSlot(ItemSlot.Weapon)), Is.False);
        }

        [Test]
        public void ClassSkillsCarryTheClassSkillTagAndGeneralSkillsDoNot()
        {
            Assert.That(Gem("runedancer_hit_1").Tags.HasFlag(StatTags.ClassSkill), Is.True);
            Assert.That(Gem("runedancer_path_1").Tags.HasFlag(StatTags.ClassSkill), Is.True);
            Assert.That(Gem("through_stride").Tags.HasFlag(StatTags.ClassSkill), Is.False);
            Assert.That(Gem("chain").Tags.HasFlag(StatTags.ClassSkill), Is.False);
        }

        [Test]
        public void ClassSkillDamageReachesClassSkillsOnly()
        {
            var loop = Hero();
            var classTags = Gem("runedancer_hit_1").Tags;
            var generalTags = Gem("through_stride").Tags;
            var classBefore = loop.Hero.Value(CombatStats.Damage, 100, classTags);
            var generalBefore = loop.Hero.Value(CombatStats.Damage, 100, generalTags);

            Hold(loop, Affix("weapon_class_skill_damage"));

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, classTags), Is.GreaterThan(classBefore));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, generalTags), Is.EqualTo(generalBefore).Within(1e-9));
        }

        [Test]
        public void ClassSkillCooldownShortensAClassSkill()
        {
            var plain = Hero();
            var quick = Hero();
            Hold(quick, Affix("weapon_class_skill_cooldown"));
            foreach (var loop in new[] { plain, quick })
            {
                loop.Book.Equip(Gem("runedancer_hit_1"));
                loop.State.Resource = 100;
                Assert.That(loop.Skills.TryCast(Row(loop, "runedancer_hit_1")), Is.EqualTo(CastResult.Cast));
            }

            Assert.That(quick.State.Skills[Row(quick, "runedancer_hit_1")].CooldownTicks, Is.LessThan(plain.State.Skills[Row(plain, "runedancer_hit_1")].CooldownTicks));
        }

        [Test]
        public void RoleLevelRaisesThatRoleOnlyAndGoesPastTheCap()
        {
            var loop = Hero();
            var fast = Gem("runedancer_hit_1");
            var heavy = Gem("runedancer_queue_1");
            loop.State.ProgressOf(fast).Level = Data.Gems.Levels.Max;
            var fastBefore = loop.Skills.LevelPower(fast);
            var heavyBefore = loop.Skills.LevelPower(heavy);

            Hold(loop, Affix("weapon_fast_level"));

            Assert.That(loop.Skills.LevelBonus(fast), Is.EqualTo(3));
            Assert.That(loop.Skills.LevelPower(fast), Is.EqualTo(fastBefore + 3 * Data.Gems.Levels.EffectPerLevel).Within(1e-9));
            Assert.That(loop.Skills.LevelPower(heavy), Is.EqualTo(heavyBefore).Within(1e-9));
        }

        [Test]
        public void SocketAffixOpensOneMoreSocketForItsSlotAndTakesItBack()
        {
            var loop = Hero();
            loop.Book.Equip(Gem("runedancer_hit_1"));
            var row = Row(loop, "runedancer_hit_1");
            Assert.That(loop.State.Skills[row].Role, Is.EqualTo(SkillSlotRole.Left));
            var plain = loop.Book.SocketsAt(row);

            Hold(loop, Affix("weapon_socket_left"));
            Assert.That(loop.Book.SocketsAt(row), Is.EqualTo(plain + 1));
            loop.State.OwnedGems.Add(Gem("chain"));
            Assert.That(loop.Book.SetSupport(row, plain, Gem("chain")), Is.EqualTo(Game.Core.Build.BookResult.Done));

            loop.Weapons.Unequip();
            loop.Tick();

            Assert.That(loop.Book.SocketsAt(row), Is.EqualTo(plain));
            Assert.That(loop.State.Skills[row].Supports[plain], Is.Null);
            Assert.That(loop.Book.FreeCopies(Gem("chain")), Is.EqualTo(1), "the support goes back to the bag");
        }

        [Test]
        public void BuiltInSupportWorksOnTheLeftSkillWithoutASocketOrACopy()
        {
            var loop = Hero();
            loop.Book.Equip(Gem("runedancer_hit_1"));
            loop.Book.Equip(Gem("runedancer_queue_1"));
            var left = Row(loop, "runedancer_hit_1");
            var right = Row(loop, "runedancer_queue_1");
            Hold(loop, Affix("weapon_built_in_left", support: "chain"));

            loop.Tick();

            Assert.That(loop.State.Skills[left].Supports[SkillSlot.BuiltInSocket], Is.SameAs(Gem("chain")));
            Assert.That(loop.State.Skills[right].Supports[SkillSlot.BuiltInSocket], Is.Null);
            Assert.That(loop.Book.FreeCopies(Gem("chain")), Is.EqualTo(0));

            loop.Weapons.Unequip();
            loop.Tick();

            Assert.That(loop.State.Skills[left].Supports[SkillSlot.BuiltInSocket], Is.Null);
        }

        [Test]
        public void SaveKeepsItemLevelAffixesAndTheBuiltInChoiceButNotTheBuiltInSocket()
        {
            var loop = Hero();
            loop.Book.Equip(Gem("runedancer_hit_1"));
            Hold(loop, Affix("weapon_class_skill_damage", 4), Affix("weapon_built_in_left", support: "twins"));
            loop.Tick();

            var text = SaveCodec.Encode(loop.State, Clock);
            var restored = SaveCodec.Decode(text, Data).State;

            var held = restored.Weapons.Held!;
            Assert.That(held.ItemLevel, Is.EqualTo(6));
            Assert.That(held.Affixes.Select(affix => affix.Def.Id), Is.EqualTo(new[] { "weapon_class_skill_damage", "weapon_built_in_left" }));
            Assert.That(held.Affixes[0].Tier, Is.EqualTo(4));
            Assert.That(held.Affixes[1].Support, Is.SameAs(Gem("twins")));
            Assert.That(restored.Skills[0].Supports[SkillSlot.BuiltInSocket], Is.Null);
            Assert.That(SaveCodec.Encode(restored, Clock), Is.EqualTo(text));
        }

        [Test]
        public void WeaponAffixesAreCheckedOnLoad()
        {
            var json = TestData.ReadGameDataFile(ItemCatalog.FileName);
            var mixed = json.Replace("{\"id\": \"weapon_strike_damage\", \"slots\": [\"weapon\"]", "{\"id\": \"weapon_strike_damage\", \"slots\": [\"weapon\", \"gloves\"]");
            var armorBuiltIn = json.Replace("{\"id\": \"weapon_built_in_left\", \"slots\": [\"weapon\"]", "{\"id\": \"weapon_built_in_left\", \"slots\": [\"gloves\"]");
            var unknown = ItemCatalog.Load(json.Replace("\"supports\": [\"chain\",", "\"supports\": [\"nothing\","));

            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(mixed))!.Message, Does.Contain("weapon slot only"));
            Assert.That(Assert.Throws<ContentException>(() => ItemCatalog.Load(armorBuiltIn))!.Message, Does.Contain("only a weapon"));
            Assert.That(Assert.Throws<ContentException>(() => unknown.BindGems(Data.Gems))!.Message, Does.Contain("nothing"));
        }
    }
}
