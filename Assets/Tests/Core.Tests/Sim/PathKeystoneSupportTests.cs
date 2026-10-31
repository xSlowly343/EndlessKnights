using System.Linq;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PathKeystoneSupportTests
    {
        private static double KeyDamage => TestWorld.Data.Combat.KeyDamage;
        private static GemDef Gem(string id) => TestWorld.Real.Gems[id];

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = heroClass }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.State.HitsOnEnemy = TestWorld.Data.Combat.Conditions.FirstHits;
            loop.Events.Clear();
            return (loop, input);
        }

        private static int Equip(GameLoop loop, GemDef gem)
        {
            TestWorld.ArmFor(loop, gem);
            return loop.Skills.Equip(gem);
        }

        private static double Damage(GameLoop loop, DamageSource source)
        {
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == source)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        private static void Wait(GameLoop loop, double seconds)
        {
            for (var i = 0; i < (int)System.Math.Round(seconds * GameLoop.TicksPerSecond); i++)
                loop.Tick();
        }

        [Test]
        public void GoldGrowsWithTheResourceBar()
        {
            var (plain, _) = Hero(HeroClass.Metalhead);
            var (loud, _) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.GoldPerResource, ModifierKind.Flat, 1));
            loud.State.ResourceFill = 1;

            var ratio = (loud.Loot.GoldFor(1, EnemyType.Normal, null) / plain.Loot.GoldFor(1, EnemyType.Normal, null)).ToDouble();

            Assert.That(ratio, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void ClicksHitAgainWithTheKeystone()
        {
            var (loop, input) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.ClickHitMultiplier, ModifierKind.Flat, 1.5));
            var (plain, plainInput) = Hero(HeroClass.Metalhead);

            input.ClickLeft();
            plainInput.ClickLeft();
            loop.Tick();
            plain.Tick();

            Assert.That(loop.Paths.ClickHits, Is.EqualTo(1));
            Assert.That(plain.Paths.ClickHits, Is.EqualTo(0));
            Assert.That(Damage(loop, DamageSource.Skill), Is.GreaterThan(0));
        }

        [Test]
        public void TypingKeepsSongsUpToTheirFullLength()
        {
            var (loop, input) = Hero(HeroClass.Viking, new Modifier(CombatStats.AuraSecondsPerKey, ModifierKind.Flat, 0.5, StatTags.Aura));
            var slot = Equip(loop, Gem("hearth_song"));
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            var song = loop.State.Skills[slot];
            var full = song.ActiveTotalTicks;

            Wait(loop, 3);
            Assert.That(song.ActiveTicks, Is.EqualTo(full - 30));

            input.PressKeys(1);
            loop.Tick();
            Assert.That(song.ActiveTicks, Is.EqualTo(full - 31 + 5));

            for (var i = 0; i < 10; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }
            Assert.That(song.ActiveTicks, Is.EqualTo(full - 1));
        }

        [Test]
        public void HeadbangExtendsTheSongUpToTheCap()
        {
            var rules = TestWorld.Data.Combat.Classes;
            var (loop, input) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.SongMouseSeconds, ModifierKind.Flat, 10, StatTags.Aura));
            var slot = Equip(loop, Gem("hearth_song"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var song = loop.State.Skills[slot];
            var full = song.ActiveTicks;

            input.MoveMouse((float)rules.HeadbangPxPerSecond);
            loop.Tick();
            Assert.That(song.ActiveTicks, Is.EqualTo(full - 1 + GameLoop.TicksPerSecond));

            for (var i = 0; i < 200; i++)
            {
                input.MoveMouse((float)rules.HeadbangPxPerSecond);
                loop.Tick();
            }
            Assert.That(song.MouseExtendedTicks, Is.EqualTo((int)((rules.HeadbangMaxSeconds + 10) * GameLoop.TicksPerSecond)));
        }

        [Test]
        public void OnlyTheMetalheadHeadbangs()
        {
            var (loop, input) = Hero(HeroClass.Viking);
            var slot = Equip(loop, Gem("hearth_song"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(slot);
            var full = loop.State.Skills[slot].ActiveTicks;

            input.MoveMouse(100000);
            loop.Tick();

            Assert.That(loop.State.Skills[slot].ActiveTicks, Is.EqualTo(full - 1));
        }

        [Test]
        public void GainedOmensBurnTheEnemy()
        {
            var (loop, input) = Hero(HeroClass.Oracle, new Modifier(CombatStats.ResourceGainDamage, ModifierKind.Flat, 0.5));
            var pxPerOmen = TestWorld.Data.Combat.Resources.For("oracle").MousePxPerPoint;

            input.MoveMouse((float)pxPerOmen * 2);
            loop.Tick();

            Assert.That(loop.Resource.GainedThisTick, Is.EqualTo(2).Within(1e-9));
            var expected = loop.Combat.SkillDamage(KeyDamage, 1.0, StatTags.Skill).ToDouble();
            Assert.That(Damage(loop, DamageSource.Skill), Is.EqualTo(expected).Within(expected * 1e-6));
        }

        [Test]
        public void SkillCostCanGrow()
        {
            var (loop, _) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.SkillCost, ModifierKind.More, 0.5));
            var gem = Gem("hearth_song");
            var slot = Equip(loop, gem);
            loop.State.Resource = 100;

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Resource, Is.EqualTo(100 - gem.Skill!.ResourceCost * 1.5).Within(1e-9));
        }

        [Test]
        public void RegularEnemiesArriveWeakened()
        {
            var strong = new Modifier(CombatStats.Damage, ModifierKind.More, 1e60);
            var (plain, plainInput) = Hero(HeroClass.Oracle, strong);
            var (cut, cutInput) = Hero(HeroClass.Oracle, strong, new Modifier(CombatStats.EnemyHpCut, ModifierKind.Flat, 0.25));

            plainInput.PressKeys(1);
            cutInput.PressKeys(1);
            plain.Tick();
            cut.Tick();

            Assert.That(cut.State.EnemyType, Is.EqualTo(EnemyType.Normal));
            Assert.That((cut.State.EnemyMaxHp / plain.State.EnemyMaxHp).ToDouble(), Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void ActiveAurasAddDamage()
        {
            var (loop, _) = Hero(HeroClass.Bannerbearer, new Modifier(CombatStats.DamagePerAura, ModifierKind.Flat, 0.15));
            var slot = Equip(loop, Gem("artel_pay"));
            loop.State.Resource = 100;
            loop.Tick();

            Assert.That(loop.State.ActiveAuras, Is.EqualTo(1));
            Assert.That(loop.Combat.ConditionMultiplier(StatTags.Keyboard), Is.EqualTo(1.15).Within(1e-9));
            Assert.That(slot, Is.EqualTo(0));
        }

        [Test]
        public void ReservationCanShrink()
        {
            var (plain, _) = Hero(HeroClass.Bannerbearer);
            var (light, _) = Hero(HeroClass.Bannerbearer, new Modifier(CombatStats.AuraReserve, ModifierKind.Flat, -5));
            Equip(plain, Gem("artel_pay"));
            Equip(light, Gem("artel_pay"));
            plain.Tick();
            light.Tick();

            Assert.That(light.State.ReservedResource, Is.EqualTo(plain.State.ReservedResource - 5).Within(1e-9));
        }

        [Test]
        public void CursesStrikeByThemselves()
        {
            var (loop, _) = Hero(HeroClass.Voodoo, new Modifier(CombatStats.CurseHitsPerSecond, ModifierKind.Flat, 1));
            var slot = Equip(loop, Gem("pin"));
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(slot), Is.EqualTo(CastResult.Cast));
            loop.Events.Clear();

            Wait(loop, 2);

            Assert.That(loop.Skills.ActiveCurses, Is.EqualTo(1));
            Assert.That(Damage(loop, DamageSource.Skill), Is.GreaterThan(0));
        }

        [Test]
        public void EchoRepeatsAShareOfTheLastSeconds()
        {
            var (loop, input) = Hero(HeroClass.Clockmaker,
                new Modifier(CombatStats.EchoEverySeconds, ModifierKind.Flat, 1),
                new Modifier(CombatStats.EchoShare, ModifierKind.Flat, 0.5));
            var dealt = 0.0;

            for (var i = 0; i < GameLoop.TicksPerSecond; i++)
            {
                if (i % 2 == 0)
                    input.PressKeys(1);
                loop.Events.Clear();
                loop.Tick();
                dealt += Damage(loop, DamageSource.Keyboard);
            }

            Assert.That(loop.Paths.Echoes, Is.EqualTo(1));
            Assert.That(Damage(loop, DamageSource.Skill), Is.EqualTo(dealt * 0.5).Within(dealt * 1e-6));
        }

        [Test]
        public void DoomHoldsDamageBackAndLandsItLater()
        {
            var seconds = TestWorld.Data.Combat.Paths.DoomSeconds;
            var (plain, plainInput) = Hero(HeroClass.Clockmaker);
            var (loop, input) = Hero(HeroClass.Clockmaker,
                new Modifier(CombatStats.DoomShare, ModifierKind.Flat, 0.6),
                new Modifier(CombatStats.DoomMultiplier, ModifierKind.Flat, 0.5));

            plainInput.PressKeys(1);
            input.PressKeys(1);
            plain.Tick();
            loop.Tick();
            var full = Damage(plain, DamageSource.Keyboard);

            Assert.That(Damage(loop, DamageSource.Keyboard), Is.EqualTo(full * 0.4).Within(full * 1e-6));
            Assert.That(loop.State.Doom.ToDouble(), Is.EqualTo(full * 0.6).Within(full * 1e-6));

            loop.Events.Clear();
            Wait(loop, seconds);
            Assert.That(loop.Paths.DoomReleases, Is.EqualTo(1));
            Assert.That(Damage(loop, DamageSource.Skill), Is.EqualTo(full * 0.6 * 1.5).Within(full * 1e-6));
            Assert.That(loop.State.Doom.Sign, Is.EqualTo(0));
        }

        [Test]
        public void DoomIsLostWithTheEnemy()
        {
            var (loop, _) = Hero(HeroClass.Clockmaker);
            loop.State.Doom = BigNumber.FromDouble(100);
            loop.State.DoomTicks = 5;

            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(10));

            Assert.That(loop.State.Doom.Sign, Is.EqualTo(0));
            Assert.That(loop.State.DoomTicks, Is.EqualTo(0));
        }

        [Test]
        public void DoomedEnemiesAreCritMoreOften()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.DoomedCritChance, ModifierKind.Increased, 0.4));
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Clockmaker }, hero);
            var before = loop.Combat.CritChance(StatTags.Keyboard);

            loop.State.Doom = BigNumber.FromDouble(1);

            Assert.That(loop.Combat.CritChance(StatTags.Keyboard), Is.EqualTo(before * 1.4).Within(1e-9));
        }

        [Test]
        public void ReadingGivesMoreOmens()
        {
            var rules = TestWorld.Data.Combat.Resources.For("oracle");
            var (plain, plainInput) = Hero(HeroClass.Oracle);
            var (reader, readerInput) = Hero(HeroClass.Oracle, new Modifier(CombatStats.ReadingGain, ModifierKind.Increased, 0.5));
            foreach (var loop in new[] { plain, reader })
                loop.State.KeylessTicks = 1000;

            plainInput.MoveMouse(1);
            readerInput.MoveMouse(1);
            plain.Tick();
            reader.Tick();

            var mouse = 1 / rules.MousePxPerPoint;
            Assert.That(reader.State.Resource - mouse, Is.EqualTo((plain.State.Resource - mouse) * 1.5).Within(1e-9));
        }
    }
}
