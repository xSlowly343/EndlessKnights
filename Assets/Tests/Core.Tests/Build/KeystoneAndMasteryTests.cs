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
        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params string[] path)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
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

        private static readonly string[] ToOneHanded = { "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_combo_small", "unstoppable", "one_handed" };
        private static readonly string[] ToPenMastery = { "t_pen_1", "t_pen_2", "fluent_writing", "t_pen_3", "t_pen_4" };
        private static readonly string[] ToOnlyMusic = { "b_ensemble_1", "b_ensemble_2", "b_ensemble_3", "b_ensemble_4", "b_ensemble_small", "clan_voice", "only_music" };
        private static readonly string[] ToMelodyMastery = { "b_attr_3=precision", "b_melody_1", "b_melody_2", "chorus", "b_melody_3" };

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
        public void OneHandedMouseGivesNothingAndKeysHitTwiceAsHard()
        {
            var (plain, plainInput) = Hero(HeroClass.Typist, "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_combo_small", "unstoppable");
            var (keystone, keystoneInput) = Hero(HeroClass.Typist, ToOneHanded);

            foreach (var (loop, input) in new[] { (plain, plainInput), (keystone, keystoneInput) })
            {
                input.PressKeys(1);
                input.MoveMouse(2000);
                loop.Tick();
            }

            Assert.That(Damage(keystone, DamageSource.Mouse), Is.EqualTo(0));
            Assert.That(Damage(plain, DamageSource.Mouse), Is.GreaterThan(0));
            Assert.That(keystone.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard) / plain.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(2).Within(1e-9));
            Assert.That(keystone.State.Energy, Is.LessThan(plain.State.Energy));
        }

        [Test]
        public void OnlyMusicCutsDamageAndDoublesAuras()
        {
            var (plain, _) = Hero(HeroClass.Bard, "b_ensemble_1", "b_ensemble_2", "b_ensemble_3", "b_ensemble_4", "b_ensemble_small", "clan_voice");
            var (keystone, _) = Hero(HeroClass.Bard, ToOnlyMusic);
            plain.Skills.Equip(TestWorld.Data.Gems["march"]);
            keystone.Skills.Equip(TestWorld.Data.Gems["march"]);
            plain.State.Notes = 4;
            keystone.State.Notes = 4;
            plain.Tick();
            keystone.Tick();

            var plainDamage = plain.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            var keystoneDamage = keystone.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);
            Assert.That(plainDamage, Is.EqualTo(100 * (1 + 0.25 * 1.03)).Within(1e-6));
            Assert.That(keystoneDamage, Is.EqualTo(100 * (1 + 0.25 * 1.03 * 2) * 0.3).Within(1e-6));
        }

        [Test]
        public void MasteryNeedsAnOptionAndCanBeChanged()
        {
            var (loop, _) = Hero(HeroClass.Typist, ToPenMastery);

            Assert.That(loop.AllocateNode("pen_mastery"), Is.EqualTo(AllocationResult.ChoiceRequired));
            Assert.That(loop.AllocateNode("pen_mastery", masteryOption: 3), Is.EqualTo(AllocationResult.BadChoice));
            Assert.That(loop.AllocateNode("pen_mastery", masteryOption: 0), Is.EqualTo(AllocationResult.Allocated));
            Assert.That(loop.Focus.Max, Is.EqualTo(3.5));

            Assert.That(loop.ChangeMastery("pen_mastery", 2), Is.True);
            Assert.That(loop.Focus.Max, Is.EqualTo(3));
            Assert.That(loop.ChangeMastery("pen_mastery", 7), Is.False);
            Assert.That(loop.ChangeMastery("melody_mastery", 0), Is.False);
        }

        [Test]
        public void EnterMasteryHitsEnterHarder()
        {
            var (enter, enterInput) = Hero(HeroClass.Typist, With(ToPenMastery, "pen_mastery:1"));
            var (plain, plainInput) = Hero(HeroClass.Typist, ToPenMastery);

            enterInput.PressEnter();
            enter.Tick();
            plainInput.PressEnter();
            plain.Tick();

            Assert.That(Damage(enter, DamageSource.Keyboard) / Damage(plain, DamageSource.Keyboard), Is.EqualTo(1.25).Within(1e-6));
        }

        [Test]
        public void EnergyMasteryGivesMoreEnergyFromKeys()
        {
            var (energy, energyInput) = Hero(HeroClass.Typist, With(ToPenMastery, "pen_mastery:2"));

            energyInput.PressKeys(1);
            energy.Tick();

            Assert.That(energy.State.Energy, Is.EqualTo(1.1).Within(1e-9));
        }

        [Test]
        public void MelodyMasteryPlaysOnThreeNotes()
        {
            var (loop, input) = Hero(HeroClass.Bard, With(ToMelodyMastery, "melody_mastery:0"));
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);

            input.ClickLeft(3);
            loop.Tick();
            loop.Tick();

            Assert.That(loop.Combat.Class.NotesPerMelody, Is.EqualTo(3));
            Assert.That(loop.State.Skills[0].Active, Is.True);
        }

        [Test]
        public void MelodyNodesStretchAuraDuration()
        {
            var (loop, _) = Hero(HeroClass.Bard, "b_attr_3=tempo", "b_melody_1", "b_melody_2");
            loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.State.Notes = 4;

            loop.Tick();

            var expected = TestWorld.Data.Gems["march"].Skill!.DurationSeconds * 1.16 * GameLoop.TicksPerSecond;
            Assert.That(loop.State.Skills[0].ActiveTicks, Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void EveryTenthKeyHitsFiveTimesHarder()
        {
            var (typist, typistInput) = Hero(HeroClass.Typist, "t_heavy_1", "t_heavy_2", "every_tenth", "t_heavy_3", "t_heavy_4", "t_heavy_small");

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
        public void UnstoppableKeepsFocusThroughFourSecondPause()
        {
            var (loop, input) = Hero(HeroClass.Typist, "t_combo_1", "t_combo_2", "t_combo_3", "t_combo_4", "t_combo_small", "unstoppable");
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
        public void ComboNodesGrowFocusFaster()
        {
            var (loop, input) = Hero(HeroClass.Typist, "t_combo_1", "t_combo_2");

            input.PressKeys(1);
            loop.Tick();

            Assert.That(loop.State.Focus, Is.EqualTo(1 + 0.02 * 1.08).Within(1e-9));
        }

        [Test]
        public void QuietStrengthStartsLullTenSecondsEarlier()
        {
            var (hermit, hermitInput) = Hero(HeroClass.Typist);
            hermit.State.Tree = new TreeAllocation("hermit") { PointsEarned = 10 };
            foreach (var id in new[] { "c_silence_1", "c_silence_2", "quiet_strength", "c_silence_3", "c_silence_4" })
                Assert.That(hermit.AllocateNode(id), Is.EqualTo(AllocationResult.Allocated), id);
            hermit.Skills.Equip(TestWorld.Data.Gems["lull"]);

            for (var i = 0; i < 25 * GameLoop.TicksPerSecond; i++)
                hermit.Tick();
            hermitInput.PressKeys(1);
            hermit.Tick();

            Assert.That(Damage(hermit, DamageSource.Skill), Is.GreaterThan(0));
        }
    }
}
