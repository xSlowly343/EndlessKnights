using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Weapons
{
    public class WeaponTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static WeaponCatalog Catalog => TestWorld.Real.Weapons;

        private static GameLoop Hero(HeroClass heroClass) =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState { Class = heroClass }, new StatSheet());

        private static WeaponBase Base(string id)
        {
            Catalog.TryGetBase(id, out var weapon);
            return weapon;
        }

        [Test]
        public void NewHeroHoldsTheStarterOfOwnClass()
        {
            var loop = Hero(HeroClass.Metalhead);

            Assert.That(loop.Weapons.In(WeaponHand.Right)!.Id, Is.EqualTo("out_of_tune_guitar"));
            Assert.That(loop.Weapons.In(WeaponHand.Left), Is.Null);
            Assert.That(loop.Weapons.Bag, Is.Empty);
        }

        [Test]
        public void TakingTheWeaponOffDimsTheSkillAndBlocksTheCast()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Skills.Equip(TestWorld.Real.Gems["iron_rim_bash"]);
            loop.State.Resource = 100;

            loop.Weapons.Unequip(WeaponHand.Right);

            Assert.That(loop.Skills.Armed(0), Is.False);
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.NoWeapon));
            Assert.That(loop.State.Skills[0].Ready, Is.True);
            Assert.That(loop.State.Resource, Is.EqualTo(100));
            Assert.That(loop.Weapons.Bag[0].Id, Is.EqualTo("woodcutter_axe"));

            loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Bag[0]);
            Assert.That(loop.Skills.Armed(0), Is.True);
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void ActiveAuraGoesOutWithItsWeapon()
        {
            var loop = Hero(HeroClass.Metalhead);
            loop.Skills.Equip(TestWorld.Real.Gems["hearth_song"]);
            loop.State.Resource = 100;
            var before = loop.Hero.Value(CombatStats.Damage, 1);
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.GreaterThan(before));

            loop.Weapons.Unequip(WeaponHand.Right);

            Assert.That(loop.State.Skills[0].Active, Is.False);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1), Is.EqualTo(before).Within(1e-9));
        }

        [Test]
        public void SecondHandOpensAnotherClassSkills()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Skills.Equip(TestWorld.Real.Gems["hearth_song"]);
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.NoWeapon));

            var guitar = Base("spark_guitar");
            loop.Weapons.Give(guitar);
            Assert.That(loop.Weapons.Equip(WeaponHand.Left, guitar), Is.True);

            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
            Assert.That(loop.Weapons.In(WeaponHand.Right)!.Type.Id, Is.EqualTo("axe"));
        }

        [Test]
        public void CommonSkillsNeedNoWeapon()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Weapons.Unequip(WeaponHand.Right);
            loop.Skills.Equip(TestWorld.Real.Gems["through_stride"]);
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void InnatePropertiesOfTwoWeaponsAddUp()
        {
            var loop = Hero(HeroClass.Viking);
            var keyDamage = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            loop.Weapons.Unequip(WeaponHand.Right);
            var bare = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            var axe = loop.Weapons.Bag[0];
            loop.Weapons.Equip(WeaponHand.Right, axe);
            var second = Base("battle_axe");
            loop.Weapons.Give(second);
            loop.Weapons.Equip(WeaponHand.Left, second);

            Assert.That(keyDamage - bare, Is.EqualTo(10).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard) - bare, Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void LateBaseAddsItsSecondLine()
        {
            var loop = Hero(HeroClass.Viking);
            var rage = new StatId("rageEveryTwentyHits");
            Assert.That(loop.Hero.Value(rage, 0), Is.EqualTo(0));
            var greataxe = Base("jarl_axe");
            loop.Weapons.Give(greataxe);

            loop.Weapons.Equip(WeaponHand.Right, greataxe);

            Assert.That(loop.Hero.Value(rage, 0), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.Weapons.Bag[0].Id, Is.EqualTo("woodcutter_axe"));
        }

        [Test]
        public void EquippingSomethingNotInTheBagFails()
        {
            var loop = Hero(HeroClass.Viking);

            Assert.That(loop.Weapons.Equip(WeaponHand.Left, Base("battle_axe")), Is.False);
            Assert.That(loop.Weapons.Unequip(WeaponHand.Left), Is.False);
        }

        [Test]
        public void SaveKeepsHandsAndBag()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Weapons.Give(Base("spark_guitar"));
            loop.Weapons.Give(Base("jarl_axe"));
            loop.Weapons.Equip(WeaponHand.Left, Base("spark_guitar"));

            var text = SaveCodec.Encode(loop.State, Clock);
            var restored = SaveCodec.Decode(text, TestWorld.Real).State;
            var reloaded = new GameLoop(TestWorld.Real, new FakeInputSource(), restored, new StatSheet());

            Assert.That(SaveCodec.Encode(restored, Clock), Is.EqualTo(text));
            Assert.That(reloaded.Weapons.In(WeaponHand.Right)!.Id, Is.EqualTo("woodcutter_axe"));
            Assert.That(reloaded.Weapons.In(WeaponHand.Left)!.Id, Is.EqualTo("spark_guitar"));
            Assert.That(reloaded.Weapons.Bag[0].Id, Is.EqualTo("jarl_axe"));
        }

        [Test]
        public void SaveWithoutWeaponsGetsTheStarterOnce()
        {
            var loop = Hero(HeroClass.Voodoo);
            var payload = JsonParser.Parse(SaveCodec.Encode(loop.State, Clock), "save").Fields["payload"];
            var withoutWeapons = JsonEdit.Update(payload, "state", state => JsonEdit.Remove(state, "weapons"));
            var state = GameStateJson.Read(new ContentNode(withoutWeapons, "save", "payload").Field("state"), TestWorld.Real.Enemies, TestWorld.Real.Gems, Catalog);

            Assert.That(state.Weapons.StarterGiven, Is.False);
            var reloaded = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            Assert.That(reloaded.Weapons.In(WeaponHand.Right)!.Id, Is.EqualTo("rag_doll"));

            reloaded.Weapons.Unequip(WeaponHand.Right);
            var again = new GameLoop(TestWorld.Real, new FakeInputSource(), reloaded.State, new StatSheet());
            Assert.That(again.Weapons.In(WeaponHand.Right), Is.Null);
        }
    }
}
