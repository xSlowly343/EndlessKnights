using System.Linq;
using Game.Core.Build;
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
            var loop = Hero(HeroClass.Minstrel);

            Assert.That(loop.Weapons.Held!.Id, Is.EqualTo("shepherd_lute"));
            Assert.That(loop.Weapons.Bag, Is.Empty);
        }

        [Test]
        public void TakingTheWeaponOffKeepsTheSkillCastable()
        {
            var loop = Hero(HeroClass.Runedancer);
            loop.Skills.Equip(TestWorld.Real.Gems["runedancer_hit_1"]);
            loop.State.Resource = 100;

            loop.Weapons.Unequip();

            Assert.That(loop.State.Weapons.Held, Is.Null);
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void ActiveAuraStaysWhenTheWeaponGoesOff()
        {
            var loop = Hero(HeroClass.Minstrel);
            var before = loop.Hero.Value(CombatStats.ResourceGain, 1);
            loop.Skills.Equip(TestWorld.Real.Gems["minstrel_aura_1"]);
            loop.State.Resource = 100;
            loop.Tick();
            var lit = loop.Hero.Value(CombatStats.ResourceGain, 1);
            Assert.That(lit, Is.GreaterThan(before));

            loop.Weapons.Unequip();
            loop.Tick();

            Assert.That(loop.State.Skills[0].Active, Is.True);
            Assert.That(loop.Hero.Value(CombatStats.ResourceGain, 1), Is.GreaterThan(before));
        }

        [Test]
        public void AnotherClassWeaponDoesNotOpenItsSkills()
        {
            var loop = Hero(HeroClass.Runedancer);
            loop.Weapons.Equip(loop.Weapons.Give(Base("butterfly_lute")));

            Assert.That(loop.Book.Equip(TestWorld.Real.Gems["minstrel_hit_1"]), Is.EqualTo(BookResult.WrongClass));
        }

        [Test]
        public void SavedWeaponsCarryNoSkills()
        {
            var loop = Hero(HeroClass.Runedancer);

            var json = SaveCodec.Encode(loop.State, Clock);
            var weapon = json.Substring(json.IndexOf("\"apprentice_daggers\"", System.StringComparison.Ordinal));
            weapon = weapon.Substring(0, weapon.IndexOf('}'));

            Assert.That(weapon, Does.Contain("\"rarity\""));
            Assert.That(weapon, Does.Not.Contain("\"skills\""));
        }

        [Test]
        public void CommonSkillsNeedNoWeapon()
        {
            var loop = Hero(HeroClass.Runedancer);
            loop.Weapons.Unequip();
            loop.Skills.Equip(TestWorld.Real.Gems["through_stride"]);
            loop.State.Resource = 100;

            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void OnlyTheHeldWeaponGivesItsInnateAndTheOldOneGoesToTheBag()
        {
            var loop = Hero(HeroClass.Runedancer);
            var keyDamage = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Strike);
            loop.Weapons.Unequip();
            var bare = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Strike);
            loop.Weapons.Equip(loop.Weapons.Bag[0]);
            var second = loop.Weapons.Give(Base("twin_daggers"));
            loop.Weapons.Equip(second);

            Assert.That(keyDamage - bare, Is.EqualTo(10).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Strike) - bare, Is.EqualTo(10).Within(1e-9));
            Assert.That(loop.Weapons.Held, Is.SameAs(second));
            Assert.That(loop.Weapons.Bag[0].Id, Is.EqualTo("apprentice_daggers"));
        }

        [Test]
        public void AWeaponOfAnotherClassCannotBeTaken()
        {
            var loop = Hero(HeroClass.Runedancer);
            var lute = loop.Weapons.Give(Base("butterfly_lute"));

            Assert.That(loop.Weapons.Equip(lute), Is.False);
            Assert.That(loop.Weapons.Held!.Id, Is.EqualTo("apprentice_daggers"));
            Assert.That(loop.Weapons.Bag, Does.Contain(lute));
        }

        [Test]
        public void LateBaseAddsItsSecondLine()
        {
            var loop = Hero(HeroClass.Runedancer);
            var word = RunedancerStats.RuneWord;
            Assert.That(loop.Hero.Value(word, 0), Is.EqualTo(0));
            var quills = loop.Weapons.Give(Base("colossus_quills"));

            loop.Weapons.Equip(quills);

            Assert.That(loop.Hero.Value(word, 0), Is.EqualTo(1).Within(1e-9));
            Assert.That(loop.Weapons.Bag[0].Id, Is.EqualTo("apprentice_daggers"));
        }

        [Test]
        public void EquippingSomethingNotInTheBagFails()
        {
            var loop = Hero(HeroClass.Runedancer);

            Assert.That(loop.Weapons.Equip(Catalog.Starter(Base("twin_daggers"))), Is.False);
            Assert.That(loop.Weapons.Unequip(), Is.True);
            Assert.That(loop.Weapons.Unequip(), Is.False);
        }

        [Test]
        public void SaveKeepsTheHeldWeaponAndTheBag()
        {
            var loop = Hero(HeroClass.Runedancer);
            var twin = loop.Weapons.Give(Base("twin_daggers"));
            loop.Weapons.Give(Base("colossus_quills"));
            loop.Weapons.Equip(twin);

            var text = SaveCodec.Encode(loop.State, Clock);
            var restored = SaveCodec.Decode(text, TestWorld.Real).State;
            var reloaded = new GameLoop(TestWorld.Real, new FakeInputSource(), restored, new StatSheet());

            Assert.That(SaveCodec.Encode(restored, Clock), Is.EqualTo(text));
            Assert.That(reloaded.Weapons.Held!.Id, Is.EqualTo("twin_daggers"));
            Assert.That(reloaded.Weapons.Bag.Select(weapon => weapon.Id), Is.EquivalentTo(new[] { "colossus_quills", "apprentice_daggers" }));
        }

        [Test]
        public void SaveWithoutWeaponsGetsTheStarterOnce()
        {
            var loop = Hero(HeroClass.Whisperer);
            var payload = JsonParser.Parse(SaveCodec.Encode(loop.State, Clock), "save").Fields["payload"];
            var withoutWeapons = JsonEdit.Update(payload, "state", state => JsonEdit.Remove(state, "weapons"));
            var state = GameStateJson.Read(new ContentNode(withoutWeapons, "save", "payload").Field("state"), TestWorld.Real.Enemies, TestWorld.Real.Gems, Catalog);

            Assert.That(state.Weapons.StarterGiven, Is.False);
            var reloaded = new GameLoop(TestWorld.Real, new FakeInputSource(), state, new StatSheet());
            Assert.That(reloaded.Weapons.Held!.Id, Is.EqualTo("rag_doll"));

            reloaded.Weapons.Unequip();
            var again = new GameLoop(TestWorld.Real, new FakeInputSource(), reloaded.State, new StatSheet());
            Assert.That(again.Weapons.Held, Is.Null);
        }
    }
}
