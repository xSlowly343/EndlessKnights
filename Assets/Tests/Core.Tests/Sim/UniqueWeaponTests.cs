using System.Collections.Generic;
using Game.Core.Build;
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

        private static GameLoop Real(HeroClass heroClass = HeroClass.Runedancer) =>
            new GameLoop(TestWorld.Real, new FakeInputSource(), new GameState(6) { Class = heroClass }, new StatSheet());

        [Test]
        public void EightUniqueWeaponsFiveOfThemOpenSkillsOfAnotherClass()
        {
            Assert.That(Weapons.Uniques.Count, Is.EqualTo(8));
            Assert.That(Weapons.Uniques.Count(u => u.OpensSkills.Count > 0), Is.EqualTo(5));
            Assert.That(Def("corsair_daggers").OpensSkills, Is.EqualTo(new[] { "ghostcaptain_hit_1", "ghostcaptain_auto_1" }));
            Assert.That(Def("skull_shovel").OpensSkills, Is.Empty);
        }

        [Test]
        public void HoldingTheUniqueOpensItsForeignSkillsWithoutTheNativeTrait()
        {
            var loop = Real();
            loop.Book.Unequip(0);
            var corsair = loop.Weapons.Give(Weapons.RollUnique(Def("corsair_daggers")));
            var shot = TestWorld.Real.Gems["ghostcaptain_hit_1"];

            Assert.That(loop.Book.Equip(shot), Is.EqualTo(BookResult.WrongClass));
            Assert.That(loop.Weapons.Equip(corsair), Is.True);
            var open = new List<GemDef>();
            loop.Book.OpenClassSkills(open);
            Assert.That(open, Does.Contain(shot));
            Assert.That(open, Does.Not.Contain(TestWorld.Real.Gems["ghostcaptain_queue_1"]));
            Assert.That(loop.Book.Equip(shot), Is.EqualTo(BookResult.Done));
            var row = loop.Book.RowOf(shot);
            Assert.That(loop.Skills.IsNative(row), Is.False);
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(row), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void TakingTheUniqueOffTakesItsForeignSkillsOutOfTheBook()
        {
            var loop = Real();
            loop.Book.Equip(TestWorld.Real.Gems["runedancer_hit_1"]);
            loop.Weapons.Equip(loop.Weapons.Give(Weapons.RollUnique(Def("corsair_daggers"))));
            var shot = TestWorld.Real.Gems["ghostcaptain_hit_1"];
            Assert.That(loop.Book.Equip(shot), Is.EqualTo(BookResult.Done));

            loop.Weapons.Equip(loop.Weapons.Bag.First(weapon => weapon.Unique == null));

            Assert.That(loop.Book.RowOf(shot), Is.EqualTo(SkillLoadout.NoSlot));
            Assert.That(loop.State.Skills, Is.Not.Empty, "own skills stay");
        }

        [Test]
        public void AUniqueCannotOpenSkillsOfItsOwnClassOrMoreThanTwo()
        {
            var json = TestData.ReadGameDataFile(WeaponCatalog.FileName);
            var own = WeaponCatalog.Load(json.Replace("\"opensSkills\": [\"clockwright_hit_1\"]", "\"opensSkills\": [\"duelist_hit_1\"]"));
            var many = WeaponCatalog.Load(json.Replace("\"opensSkills\": [\"clockwright_hit_1\"]", "\"opensSkills\": [\"clockwright_hit_1\", \"clockwright_hit_2\", \"clockwright_auto_1\"]"));

            Assert.That(Assert.Throws<ContentException>(() => own.BindGems(TestWorld.Real.Gems))!.Message, Does.Contain("own class"));
            Assert.That(Assert.Throws<ContentException>(() => many.BindGems(TestWorld.Real.Gems))!.Message, Does.Contain("at most 2"));
        }

        [Test]
        public void UniqueWeaponSurvivesSaveAndLoad()
        {
            var loop = Real(HeroClass.Duelist);
            var weapon = loop.Weapons.Give(Weapons.RollUnique(Def("twin_moon_rapier")));
            loop.Weapons.Equip(weapon);

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Real).State;

            Assert.That(loaded.Weapons.Held!.Unique!.Id, Is.EqualTo("twin_moon_rapier"));
            Assert.That(loaded.Weapons.Opens("clockwright_hit_1"), Is.True);
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
            var plain = SkillTestHero.Of(HeroClass.Runedancer, "runedancer_hit_1");
            var katana = SkillTestHero.Of(HeroClass.Runedancer, "runedancer_hit_1");
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
            var loop = TestWorld.Loop(state: TestWorld.WithAllSkillsOpen(new GameState { Class = HeroClass.Duelist }), hero: hero);
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
            var hero = SkillTestHero.Of(HeroClass.Soulbinder);
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
