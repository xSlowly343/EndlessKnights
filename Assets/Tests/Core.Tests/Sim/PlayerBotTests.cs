using System.Linq;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PlayerBotTests
    {
        private static PlayerSimulation Typist() => new PlayerSimulation(TestWorld.Real, new BotProfile(4));

        [Test]
        public void ReportHasOneRowPerDayAndRepeatsWithTheSameSeed()
        {
            var first = Typist().Run(2, 1).Select(d => d.ToCsv()).ToArray();
            var second = Typist().Run(2, 1).Select(d => d.ToCsv()).ToArray();

            Assert.That(first.Length, Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void BotSpendsEveryTreePointAndTakesASubclass()
        {
            var simulation = Typist();

            var day = simulation.Run(2, 2).Last();
            var state = simulation.LastState!;

            Assert.That(state.ClassTreeActive, Is.True, "the typist plays Huscarl on the new tree");
            Assert.That(state.TreePointsSpent, Is.GreaterThan(10));
            Assert.That(state.TreePointsLeft, Is.EqualTo(0));
            Assert.That(state.ClassTree.Majors, Is.Not.Empty, "the bot picks the first major talent");
            Assert.That(day.RecordStage, Is.GreaterThan(TestWorld.Real.Stages.BiomeLength));
            Assert.That(state.Ascendancy.Subclass, Is.Not.Null);
            Assert.That(state.Ascendancy.PointsLeft(TestWorld.Real.Ascendancy), Is.EqualTo(0));
            Assert.That(state.Skills, Is.Not.Empty);
            Assert.That(state.StrikeLevel, Is.GreaterThan(0));
        }

        [TestCase(HeroClass.Ronin)]
        [TestCase(HeroClass.Amazon)]
        [TestCase(HeroClass.Stormweaver)]
        [TestCase(HeroClass.Nomad)]
        [TestCase(HeroClass.Metalhead)]
        [TestCase(HeroClass.Oracle)]
        [TestCase(HeroClass.Bannerbearer)]
        [TestCase(HeroClass.Voodoo)]
        [TestCase(HeroClass.Clockmaker)]
        [TestCase(HeroClass.PlagueDoctor)]
        [TestCase(HeroClass.Moonshiner)]
        public void BotSpendsEveryPointOnTheTreeOfItsClass(HeroClass heroClass)
        {
            var simulation = new PlayerSimulation(TestWorld.Real, new BotProfile(4), heroClass);

            var day = simulation.Run(2, 2).Last();
            var state = simulation.LastState!;

            Assert.That(state.ClassTreeActive, Is.True);
            Assert.That(state.ClassTree.TreeId, Is.EqualTo(HeroClassNames.Of(heroClass)));
            Assert.That(state.TreePointsSpent, Is.GreaterThan(10));
            Assert.That(state.TreePointsLeft, Is.EqualTo(0));
            Assert.That(state.ClassTree.Majors, Is.Not.Empty);
            Assert.That(day.RecordStage, Is.GreaterThan(TestWorld.Real.Stages.BiomeLength));
        }

        [Test]
        public void TypistAvoidsKeystonesThatHurtItsDamage()
        {
            var simulation = Typist();

            simulation.Run(3, 2);

            Assert.That(simulation.LastState!.Tree.Allocated, Does.Not.Contain("only_music"));
        }

        [Test]
        public void StuckBotPrestigesAndBuysArtifacts()
        {
            var days = Typist().Run(3, 2);

            Assert.That(days.Last().Prestiges, Is.GreaterThan(0));
            Assert.That(days.Last().Artifacts, Is.GreaterThan(0));
            Assert.That(days.Last().RecordStage, Is.GreaterThanOrEqualTo(days.First().RecordStage));
        }

        [Test]
        public void ScoreIgnoresInputTheClassDoesNotUse()
        {
            var typist = new NodeScore(StatTags.Keyboard, new[] { "tempo" });
            var mouseOnly = new[] { new Modifier(CombatStats.Damage, ModifierKind.More, -1, StatTags.Mouse) };
            var lessDamage = new[] { new Modifier(CombatStats.Damage, ModifierKind.More, -0.7) };
            var keyDamage = new[] { new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.1, StatTags.Keyboard) };

            Assert.That(typist.Of(mouseOnly), Is.EqualTo(0));
            Assert.That(typist.Of(lessDamage), Is.LessThan(0));
            Assert.That(typist.Of(keyDamage), Is.GreaterThan(0));
        }
    }
}
