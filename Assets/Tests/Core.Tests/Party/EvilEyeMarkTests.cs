using System.Linq;
using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class EvilEyeMarkTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        [Test]
        public void TheEvilEyeSetsItsOwnMarkOnlyForTheWhisperer()
        {
            var whisperer = TestWorld.Loop();
            whisperer.State.Class = HeroClass.Voodoo;
            var other = TestWorld.Loop();
            other.State.Class = HeroClass.Viking;
            whisperer.Tick();
            whisperer.Tick();
            other.Tick();
            other.Tick();

            Assert.That(whisperer.Synergy.OwnMarks & Mark("evilEye"), Is.Not.Zero);
            Assert.That(whisperer.Synergy.OwnMarks & Mark("curse"), Is.Zero);
            Assert.That(other.Synergy.OwnMarks & Mark("evilEye"), Is.Zero);
        }

        [Test]
        public void AnAllysEvilEyeWakesCurseResponsesAtHalfButNotTreeLinesOrReactions()
        {
            var viking = TestWorld.Loop();
            viking.State.Class = HeroClass.Viking;
            viking.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, ModifierCondition.AllyCurse));
            viking.Synergy.SetAlly(1, true, HeroClass.Voodoo, Mark("evilEye"));
            viking.Tick();

            Assert.That(viking.Hero.Value(new StatId("resourceGain"), 1), Is.EqualTo(1.125).Within(1e-9));
            Assert.That(viking.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.AllyCurse), Is.Zero);
            var broken = Rules.Reactions.ToList().FindIndex(reaction => reaction.Id == "broken");
            viking.Synergy.SetAlly(2, true, HeroClass.Viking, Mark("stun"));
            viking.Tick();
            Assert.That(viking.Synergy.ReactionActive(broken), Is.False);
        }

        [Test]
        public void AFullCurseStillWakesTheWholeResponse()
        {
            var viking = TestWorld.Loop();
            viking.State.Class = HeroClass.Viking;
            viking.Synergy.SetAlly(1, true, HeroClass.Voodoo, Mark("evilEye") | Mark("curse"));
            viking.Tick();

            Assert.That(viking.Hero.Value(new StatId("resourceGain"), 1), Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void TheSecondResponseOfEveryClassListensToAMarkOfItsLinkBranch()
        {
            foreach (var response in Rules.Responses.Where(response => response.Second))
            {
                var name = HeroClassNames.Of(response.HeroClass);
                var tree = TestWorld.Data.ClassTreeOf(name)!;
                var holder = tree.Nodes.Single(node => node.ModifiersAt(1).Any(modifier => modifier.Stat == Rules.SecondResponseStat));
                var when = holder.ModifiersAt(1).First(modifier => ModifierConditions.IsAlly(modifier.When)).When;

                Assert.That(ModifierConditions.MarkName(when), Is.EqualTo(Rules.Marks[response.Mark]), name);
            }
        }

        [Test]
        public void TheSwordlessBearerKeepsTheKeyPenaltyAtEveryRank()
        {
            var node = TestWorld.Data.ClassTreeOf("bannerbearer")!.Nodes.Single(candidate => candidate.Id == "bannerbearer_bearer_without_sword");
            for (var rank = 1; rank <= 5; rank++)
            {
                var sheet = new StatSheet();
                foreach (var modifier in node.ModifiersAt(rank))
                    sheet.Add(modifier);
                Assert.That(sheet.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.EqualTo(0.6).Within(1e-9), $"rank {rank}");
            }
        }
    }
}
