using System.Linq;
using Game.Core.Content.Gems;
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
            Assert.That(state.Ascendancy.TraitLevel(TestWorld.Real.Ascendancy), Is.GreaterThan(0));
            Assert.That(state.Skills, Is.Not.Empty);
            Assert.That(state.StrikeLevel, Is.GreaterThan(0));
        }

        [TestCase(HeroClass.Duelist)]
        [TestCase(HeroClass.Windshot)]
        [TestCase(HeroClass.Shardcaller)]
        [TestCase(HeroClass.Sandwalker)]
        [TestCase(HeroClass.Minstrel)]
        [TestCase(HeroClass.Dreamcatcher)]
        [TestCase(HeroClass.Lanternbearer)]
        [TestCase(HeroClass.Whisperer)]
        [TestCase(HeroClass.Clockwright)]
        [TestCase(HeroClass.Alchemist)]
        [TestCase(HeroClass.Dowser)]
        [TestCase(HeroClass.Soulbinder)]
        [TestCase(HeroClass.Ghostcaptain)]
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
        public void StuckBotPrestigesAndBuysArtifacts()
        {
            var days = Typist().Run(3, 2);

            Assert.That(days.Last().Prestiges, Is.GreaterThan(0));
            Assert.That(days.Last().Artifacts, Is.GreaterThan(0));
            Assert.That(days.Last().RecordStage, Is.GreaterThanOrEqualTo(days.First().RecordStage));
        }

        [Test]
        public void BotPlaysTheSkillsOfItsWeaponAndKeepsAurasInAuraSlots()
        {
            var simulation = new PlayerSimulation(TestWorld.Real, new BotProfile(6, 1), HeroClass.Minstrel);

            simulation.Run(2, 2);
            var state = simulation.LastState!;

            Assert.That(state.Skills.Count(slot => slot.Def.Weapon != null), Is.GreaterThanOrEqualTo(5));
            Assert.That(state.Skills.Count(slot => slot.Role == SkillSlotRole.Aura), Is.GreaterThan(0));
            Assert.That(state.Skills.Where(slot => slot.Role == SkillSlotRole.Aura).All(slot => SkillLoadout.IsAura(slot.Skill)), Is.True);
        }

        [Test]
        public void BotChoosesABranchOnEveryTierItsSkillsReached()
        {
            var simulation = new PlayerSimulation(TestWorld.Real, new BotProfile(6, 1), HeroClass.Runedancer);

            simulation.Run(2, 2);
            var state = simulation.LastState!;

            var reached = 0;
            foreach (var slot in state.Skills)
            {
                var progress = state.GemProgress[slot.Skill.Id];
                for (var tier = 0; tier < slot.Skill.Branches.Count; tier++)
                {
                    if (progress.Level < slot.Skill.Branches[tier].Level)
                        continue;
                    reached++;
                    Assert.That(progress.Branches[tier], Is.Not.EqualTo(SkillBranches.NoChoice), slot.Skill.Id + " tier " + tier);
                }
            }
            Assert.That(reached, Is.GreaterThan(5));
        }

        [Test]
        public void WithoutATrialTheBotPrefersTheBranchWithMoreDamagePerSecond()
        {
            var faster = new SkillBranch("faster", "", "", 1, 0.7, 1, 1, null);
            var harder = new SkillBranch("harder", "", "", 1.3, 1, 1, 1, null);

            Assert.That(BranchScore.Best(new SkillBranchTier(5, new[] { harder, faster })), Is.EqualTo(1));
            Assert.That(BranchScore.Best(new SkillBranchTier(5, new[] { faster, harder })), Is.EqualTo(0));
        }

        [Test]
        public void ScoreIgnoresInputTheClassDoesNotUse()
        {
            var typist = new NodeScore(StatTags.Strike, Game.Core.Content.Tree.ClassAttributeDef.None);
            var mouseOnly = new[] { new Modifier(CombatStats.Damage, ModifierKind.More, -1, StatTags.Mouse) };
            var lessDamage = new[] { new Modifier(CombatStats.Damage, ModifierKind.More, -0.7) };
            var keyDamage = new[] { new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.1, StatTags.Strike) };

            Assert.That(typist.Of(mouseOnly), Is.EqualTo(0));
            Assert.That(typist.Of(lessDamage), Is.LessThan(0));
            Assert.That(typist.Of(keyDamage), Is.GreaterThan(0));
        }
    }
}
