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

namespace Game.Core.Tests.Sim
{
    public class UniqueWeaponTests
    {
        private static WeaponCatalog Weapons => TestWorld.Real.Weapons;

        private static UniqueWeaponDef Def(string id)
        {
            Assert.That(Weapons.TryGetUnique(id, out var unique), Is.True, id);
            return unique;
        }

        private static GameLoop Real(HeroClass heroClass = HeroClass.Viking) =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState(6) { Class = heroClass }, new StatSheet());

        [Test]
        public void EightUniqueWeaponsFiveOfThemDoubleTyped()
        {
            Assert.That(Weapons.Uniques.Count, Is.EqualTo(8));
            Assert.That(Weapons.Uniques.Count(u => u.SecondType != null), Is.EqualTo(5));
            Assert.That(Def("corsair_axe").SecondType!.Id, Is.EqualTo("pistol"));
            Assert.That(Def("skull_shovel").SecondType, Is.Null);
        }

        [Test]
        public void DoubleTypedWeaponRollsSkillsOfBothTypes()
        {
            var unique = Def("corsair_axe");
            var weapon = Weapons.RollUnique(unique, new Rng(5));
            var types = weapon.Skills.Select(id => TestWorld.Real.Gems[id].Skill!.Weapon).Distinct().ToList();

            Assert.That(weapon.Skills.Count, Is.LessThanOrEqualTo(Weapons.SkillRules.Places));
            Assert.That(weapon.Skills.Distinct().Count(), Is.EqualTo(weapon.Skills.Count));
            Assert.That(types, Is.EquivalentTo(new[] { "axe", "pistol" }));
            Assert.That(weapon.Counts("axe") && weapon.Counts("pistol"), Is.True);
            Assert.That(weapon.Counts("katana"), Is.False);
        }

        [Test]
        public void HoldingADoubleTypedWeaponCountsAsHoldingBothTypesAndAddsBothInnates()
        {
            var loop = Real();
            var plainCount = loop.Weapons.Modifiers.Count;
            var weapon = loop.Weapons.Give(Weapons.RollUnique(Def("corsair_axe"), new Rng(2)));
            loop.Weapons.Equip(WeaponHand.Left, weapon);

            Assert.That(loop.State.Weapons.Holds("pistol"), Is.True);
            Assert.That(loop.State.Weapons.Holds("axe"), Is.True);
            Assert.That(Weapons.TryGetType("pistol", out var pistol), Is.True);
            foreach (var innate in pistol.Innate)
                Assert.That(loop.Weapons.Modifiers, Does.Contain(innate));
            Assert.That(loop.Weapons.Modifiers.Count, Is.GreaterThan(plainCount));
        }

        [Test]
        public void UniqueWeaponSurvivesSaveAndLoad()
        {
            var loop = Real();
            var weapon = loop.Weapons.Give(Weapons.RollUnique(Def("twin_moon_katana"), new Rng(3)));
            loop.Weapons.Equip(WeaponHand.Left, weapon);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Real).State;

            Assert.That(loaded.Weapons.Left!.Unique!.Id, Is.EqualTo("twin_moon_katana"));
            Assert.That(loaded.Weapons.Left.Skills, Is.EqualTo(weapon.Skills));
            Assert.That(loaded.Weapons.Holds("pendulum"), Is.True);
        }

        [Test]
        public void GuardiansDropUniqueWeaponsAndAutoSalvageKeepsThem()
        {
            var json = TestData.ReadGameDataFile(DropRules.FileName).Replace("\"uniques\": { \"guardian\": 0.03 }", "\"uniques\": { \"guardian\": 1 }");
            var loop = Real();
            loop.Drops.UseData(DropRules.Load(json), TestWorld.Real.Stages, TestWorld.Real.Gems, TestWorld.Real.Items);
            loop.State.Drops.SalvageUpTo = ItemRarity.Legendary;
            var stage = 50000L;

            var found = 0;
            for (var i = 0; i < 40; i++)
            {
                loop.Events.Clear();
                loop.Drops.OnBossDefeated(stage, EnemyType.Guardian, stage, raisesRecord: true);
                loop.Drops.Flush();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    var drop = loop.Events[e].Drop;
                    if (loop.Events[e].Kind != GameEventKind.ItemDropped || drop!.Weapon?.Unique == null)
                        continue;
                    found++;
                    Assert.That(drop.Salvaged, Is.False);
                }
                loop.State.Weapons.Bag.Clear();
                loop.State.Drops.Gear.Clear();
            }

            Assert.That(found, Is.GreaterThan(0));
        }

        [Test]
        public void TwinMoonKatanaCutsCooldownByResourceUpToItsCap()
        {
            var plain = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            var katana = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            katana.Loop.Hero.Add(new Modifier(CombatStats.CooldownPerResource, ModifierKind.Flat, 0.01));
            katana.Loop.Hero.Add(new Modifier(CombatStats.CooldownPerResourceMax, ModifierKind.Flat, 0.3));

            Assert.That(plain.Loop.Skills.TryCast(SkillSystem.LeftButtonSlot), Is.EqualTo(CastResult.Cast));
            Assert.That(katana.Loop.Skills.TryCast(SkillSystem.LeftButtonSlot), Is.EqualTo(CastResult.Cast));

            var full = plain.State.Skills[SkillSystem.LeftButtonSlot].CooldownTicks;
            var cut = katana.State.Skills[SkillSystem.LeftButtonSlot].CooldownTicks;
            Assert.That(full, Is.GreaterThan(0));
            Assert.That(cut, Is.EqualTo(full * 0.7).Within(1));
        }

        [Test]
        public void BannerGuitarAurasCostOnceInsteadOfReserving()
        {
            var plain = Aura(withGuitar: false);
            var guitar = Aura(withGuitar: true);

            Assert.That(plain.ReservedResource, Is.EqualTo(15));
            Assert.That(plain.Resource, Is.EqualTo(85).Within(1e-9));
            Assert.That(guitar.ReservedResource, Is.EqualTo(0));
            Assert.That(guitar.Resource, Is.EqualTo(85).Within(1e-9));
        }

        private static GameState Aura(bool withGuitar)
        {
            var hero = new StatSheet();
            if (withGuitar)
                hero.Add(new Modifier(CombatStats.AuraReserveOnce, ModifierKind.Flat, 1));
            var loop = TestWorld.Loop(state: TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = HeroClass.Ronin }), hero: hero);
            loop.State.Resource = 100;
            loop.Skills.Equip(TestWorld.Data.Gems["tailwind"]);
            return loop.State;
        }

        [Test]
        public void SkullShovelMinionsExplodeForHalfTheirDamage()
        {
            var plain = Summoned(0);
            var exploding = Summoned(0.5);

            Assert.That(plain, Is.GreaterThan(0));
            Assert.That(exploding, Is.EqualTo(plain * 1.5).Within(plain * 1e-6));
        }

        private static double Summoned(double explode)
        {
            var hero = SkillTestHero.Of(HeroClass.Gravedigger);
            if (explode > 0)
                hero.Loop.Hero.Add(new Modifier(CombatStats.MinionExplode, ModifierKind.Flat, explode));
            var minions = hero.Loop.Skills.Minions;
            minions.Place(new SummonEffect(MinionKind.Skeleton, 30, 2, 1), StatTags.None);
            Assert.That(minions.Standing(MinionKind.Skeleton), Is.EqualTo(1));
            hero.Ticks(10);
            minions.Consume(MinionKind.Skeleton, respawn: false);
            return hero.DamageFrom(DamageSource.Skill);
        }
    }
}
