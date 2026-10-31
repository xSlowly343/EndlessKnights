using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class KeystoneAndMasteryTests
    {
        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params Modifier[] extra)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            foreach (var modifier in extra)
                hero.Add(modifier);
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 30));
            loop.Events.Clear();
            return (loop, input);
        }

        private static readonly Modifier[] OneSong =
        {
            new Modifier(CombatStats.AuraEffect, ModifierKind.More, 1.0, StatTags.Aura),
            new Modifier(new StatId("concurrentAuras"), ModifierKind.Flat, -10)
        };

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

        [Test]
        public void OneSongDoublesTheSong()
        {
            var (plain, _) = Hero(HeroClass.Metalhead);
            var (keystone, _) = Hero(HeroClass.Metalhead, OneSong);
            var silent = plain.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            foreach (var loop in new[] { plain, keystone })
            {
                loop.Skills.Equip(TestWorld.Data.Gems["march"]);
                loop.State.Resource = 100;
                loop.Skills.TryCast(0);
            }

            var march = plain.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard) - silent;
            Assert.That(march, Is.GreaterThan(0));
            Assert.That(keystone.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(silent + march * 2).Within(1e-6));
        }

        [Test]
        public void NinjaPathLetsTheComboSurviveALongerPause()
        {
            var (plain, plainInput) = Hero(HeroClass.Ronin);
            var (series, seriesInput) = Hero(HeroClass.Ronin);
            series.CompleteActivity("garden");
            Assert.That(series.ChooseSubclass("ninja"), Is.EqualTo(SubclassResult.Chosen));
            var pauseTicks = (int)((TestWorld.Data.Combat.Classes.ComboTimeoutSeconds + 0.3) * GameLoop.TicksPerSecond);

            foreach (var (loop, input) in new[] { (plain, plainInput), (series, seriesInput) })
            {
                input.PressKeys(1);
                loop.Tick();
                for (var i = 0; i < pauseTicks; i++)
                    loop.Tick();
                input.PressKeys(1);
                loop.Tick();
            }

            Assert.That(plain.State.Combo, Is.EqualTo(1));
            Assert.That(series.State.Combo, Is.EqualTo(2));
        }

        [Test]
        public void GuitarNodeStretchesSongDuration()
        {
            var (loop, _) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.AuraDuration, ModifierKind.Increased, 0.06));
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.State.Resource = 100;

            loop.Skills.TryCast(0);

            var expected = TestWorld.Data.Gems["march"].Skill!.DurationSeconds * 1.06 * GameLoop.TicksPerSecond;
            Assert.That(loop.State.Skills[0].ActiveTicks, Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void NoMouseDamageModifierSilencesTheMouse()
        {
            var (plain, plainInput) = Hero(HeroClass.Ronin);
            var (silent, silentInput) = Hero(HeroClass.Ronin, new Modifier(CombatStats.Damage, ModifierKind.More, -1, StatTags.Mouse));

            foreach (var (loop, input) in new[] { (plain, plainInput), (silent, silentInput) })
            {
                input.MoveMouse(2000);
                loop.Tick();
            }

            Assert.That(Damage(silent, DamageSource.Mouse), Is.EqualTo(0));
            Assert.That(Damage(plain, DamageSource.Mouse), Is.GreaterThan(0));
        }

        [Test]
        public void EveryTenthKeyHitsHarderWithTheHeavyStrikeStats()
        {
            var (typist, typistInput) = Hero(HeroClass.Ronin,
                new Modifier(CombatStats.KeyStrikeEvery, ModifierKind.Flat, 10),
                new Modifier(CombatStats.KeyStrikeMultiplier, ModifierKind.Flat, 5));

            for (var i = 0; i < 10; i++)
            {
                typistInput.PressKeys(1);
                typist.Tick();
                typist.Tick();
            }

            var heavy = 0;
            for (var i = 0; i < typist.Events.Count; i++)
            {
                if ((typist.Events[i].Flags & HitFlags.HeavyStrike) != 0)
                    heavy++;
            }
            Assert.That(heavy, Is.EqualTo(1));
            Assert.That(typist.State.KeyHits, Is.EqualTo(10));
        }

        [Test]
        public void FocusResetDelayKeepsFocusThroughFourSecondPause()
        {
            var (loop, input) = Hero(HeroClass.Ronin, new Modifier(CombatStats.FocusResetSeconds, ModifierKind.Flat, 2));
            for (var i = 0; i < 50; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            var focus = loop.State.Focus;

            for (var i = 0; i < 45; i++)
                loop.Tick();

            Assert.That(loop.Focus.ResetAfterIdleSeconds, Is.EqualTo(5));
            Assert.That(loop.State.Focus, Is.EqualTo(focus));
        }

        [Test]
        public void FocusGainGrowsFocusFaster()
        {
            var (loop, input) = Hero(HeroClass.Ronin, new Modifier(CombatStats.FocusGain, ModifierKind.Increased, 0.08));

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Focus, Is.EqualTo(1 + 0.02 * 1.08).Within(1e-9));
        }

        [Test]
        public void EnterHitsLikeAnyOtherKey()
        {
            var (enter, enterInput) = Hero(HeroClass.Ronin);
            var (plain, plainInput) = Hero(HeroClass.Ronin);

            enterInput.PressEnter();
            enter.Tick();
            plainInput.PressKeys(1);
            plain.Tick();

            Assert.That(Damage(enter, DamageSource.Keyboard), Is.EqualTo(Damage(plain, DamageSource.Keyboard)).Within(1e-9));
        }

        [Test]
        public void ResourceGainGivesMoreResourceFromKeys()
        {
            var (energy, energyInput) = Hero(HeroClass.Metalhead, new Modifier(CombatStats.ResourceGain, ModifierKind.Increased, 0.15, StatTags.Keyboard));

            energyInput.PressKeys(1);
            energy.Tick();

            Assert.That(energy.State.Resource, Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void IdleStartSecondsBringsLullTenSecondsEarlier()
        {
            var (hermit, hermitInput) = Hero(HeroClass.Nomad, new Modifier(CombatStats.IdleStartSeconds, ModifierKind.Flat, -10));
            hermit.Skills.Equip(TestWorld.Data.Gems["lull"]);

            for (var i = 0; i < 25 * GameLoop.TicksPerSecond; i++)
                hermit.Tick();
            hermitInput.PressKeys(1);
            hermit.Tick();

            Assert.That(Damage(hermit, DamageSource.Skill), Is.GreaterThan(0));
        }
    }
}
