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
        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params string[] path) => Hero(heroClass, new Modifier[0], path);

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, Modifier[] extra, params string[] path)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            foreach (var modifier in extra)
                hero.Add(modifier);
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.Tree.PointsEarned = 46;
            foreach (var step in path)
            {
                var mastery = step.Split(':');
                var attribute = step.Split('=');
                var result = mastery.Length == 2 ? loop.AllocateNode(mastery[0], masteryOption: int.Parse(mastery[1]))
                    : attribute.Length == 2 ? loop.AllocateNode(attribute[0], attribute: attribute[1])
                    : loop.AllocateNode(step);
                Assert.That(result, Is.EqualTo(AllocationResult.Allocated), step);
            }
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 30));
            loop.Events.Clear();
            return (loop, input);
        }

        private static readonly string[] ToSeriesNotable = { "swordsman_ring_5", "swordsman_to_w1_1", "swordsman_to_w1_2", "swordsman_w1_4", "swordsman_w1_3", "swordsman_w1_notable" };
        private static readonly string[] ToMelodyNotable = { "bard_ring_5", "bard_to_w1_1", "bard_to_w1_2", "bard_w1_4", "bard_w1_3", "bard_w1_notable" };
        private static readonly string[] ToSongNotable = { "bard_ring_5", "bard_to_w1_1", "bard_to_w1_2", "bard_w1_4", "bard_w1_3", "bard_w1_notable", "bard_w1_w3_1", "bard_w1_w3_2", "bard_w3_4", "bard_w3_3", "bard_w3_notable", "bard_to_k1_1" };

        private static string[] With(string[] path, string last) => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(path, last));

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
        public void OnlyMusicCutsDamageAndDoublesSongs()
        {
            var (plain, _) = Hero(HeroClass.Bard, ToSongNotable);
            var (keystone, _) = Hero(HeroClass.Bard, With(ToSongNotable, "bard_keystone_1"));
            foreach (var loop in new[] { plain, keystone })
            {
                loop.Skills.Equip(TestWorld.Data.Gems["march"]);
                loop.State.Notes = 4;
                loop.Tick();
            }

            var march = 0.25 * (1 + 0.05);
            var pathDamage = 5 * 0.06;
            Assert.That(plain.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100 * (1 + pathDamage + march)).Within(1e-6));
            Assert.That(keystone.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100 * (1 + pathDamage + march * 2) * 0.3).Within(1e-6));
        }

        [Test]
        public void MasteryNeedsAnOptionAndCanBeChanged()
        {
            var (loop, _) = Hero(HeroClass.Swordsman, ToSeriesNotable);

            Assert.That(loop.AllocateNode("swordsman_w1_mastery"), Is.EqualTo(AllocationResult.ChoiceRequired));
            Assert.That(loop.AllocateNode("swordsman_w1_mastery", masteryOption: 3), Is.EqualTo(AllocationResult.BadChoice));
            Assert.That(loop.AllocateNode("swordsman_w1_mastery", masteryOption: 2), Is.EqualTo(AllocationResult.Allocated));
            var withDamage = loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

            Assert.That(loop.ChangeMastery("swordsman_w1_mastery", 0), Is.True);
            Assert.That(withDamage - loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(15).Within(1e-9));
            Assert.That(loop.ChangeMastery("swordsman_w1_mastery", 7), Is.False);
            Assert.That(loop.ChangeMastery("bard_w1_mastery", 0), Is.False);
        }

        [Test]
        public void SeriesNotableLetsTheComboSurviveALongerPause()
        {
            var (plain, plainInput) = Hero(HeroClass.Swordsman, ToSeriesNotable[0], ToSeriesNotable[1]);
            var (series, seriesInput) = Hero(HeroClass.Swordsman, ToSeriesNotable);
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
        public void MelodyMasteryPlaysOnThreeNotes()
        {
            var (loop, input) = Hero(HeroClass.Bard, With(ToMelodyNotable, "bard_w1_mastery:0"));
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);

            input.ClickLeft(3);
            loop.Tick();
            loop.Tick();

            Assert.That(loop.Combat.Class.NotesPerMelody, Is.EqualTo(3));
            Assert.That(loop.State.Skills[0].Active, Is.True);
        }

        [Test]
        public void MelodyNodesStretchSongDuration()
        {
            var (loop, _) = Hero(HeroClass.Bard, "bard_ring_5", "bard_to_w1_1", "bard_to_w1_2");
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.State.Notes = 4;

            loop.Tick();

            var expected = TestWorld.Data.Gems["march"].Skill!.DurationSeconds * 1.16 * GameLoop.TicksPerSecond;
            Assert.That(loop.State.Skills[0].ActiveTicks, Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void NoMouseDamageModifierSilencesTheMouse()
        {
            var (plain, plainInput) = Hero(HeroClass.Swordsman);
            var (silent, silentInput) = Hero(HeroClass.Swordsman, new[] { new Modifier(CombatStats.Damage, ModifierKind.More, -1, StatTags.Mouse) });

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
            var (typist, typistInput) = Hero(HeroClass.Swordsman, new[]
            {
                new Modifier(CombatStats.KeyStrikeEvery, ModifierKind.Flat, 10),
                new Modifier(CombatStats.KeyStrikeMultiplier, ModifierKind.Flat, 5)
            });

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
            var (loop, input) = Hero(HeroClass.Swordsman, new[] { new Modifier(CombatStats.FocusResetSeconds, ModifierKind.Flat, 2) });
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
            var (loop, input) = Hero(HeroClass.Swordsman, new[] { new Modifier(CombatStats.FocusGain, ModifierKind.Increased, 0.08) });

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Focus, Is.EqualTo(1 + 0.02 * 1.08).Within(1e-9));
        }

        [Test]
        public void EnterDamageHitsEnterHarder()
        {
            var (enter, enterInput) = Hero(HeroClass.Swordsman, new[] { new Modifier(CombatStats.EnterDamage, ModifierKind.Increased, 0.25) });
            var (plain, plainInput) = Hero(HeroClass.Swordsman);

            enterInput.PressEnter();
            enter.Tick();
            plainInput.PressEnter();
            plain.Tick();

            Assert.That(Damage(enter, DamageSource.Keyboard) / Damage(plain, DamageSource.Keyboard), Is.EqualTo(1.25).Within(1e-6));
        }

        [Test]
        public void EnergyGainGivesMoreEnergyFromKeys()
        {
            var (energy, energyInput) = Hero(HeroClass.Swordsman, new[] { new Modifier(CombatStats.EnergyGain, ModifierKind.Increased, 0.15, StatTags.Keyboard) });

            energyInput.PressKeys(1);
            energy.Tick();

            Assert.That(energy.State.Energy, Is.EqualTo(1.15).Within(1e-9));
        }

        [Test]
        public void IdleStartSecondsBringsLullTenSecondsEarlier()
        {
            var (hermit, hermitInput) = Hero(HeroClass.Wanderer, new[] { new Modifier(CombatStats.IdleStartSeconds, ModifierKind.Flat, -10) });
            hermit.Skills.Equip(TestWorld.Data.Gems["lull"]);

            for (var i = 0; i < 25 * GameLoop.TicksPerSecond; i++)
                hermit.Tick();
            hermitInput.PressKeys(1);
            hermit.Tick();

            Assert.That(Damage(hermit, DamageSource.Skill), Is.GreaterThan(0));
        }
    }
}
