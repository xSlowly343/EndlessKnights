using System.Linq;
using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class LinkBranchTests
    {
        private static readonly StatId Damage = new StatId("damage");
        private static readonly string[] InGame = { "viking", "ronin", "amazon", "stormweaver", "nomad", "metalhead", "oracle", "bannerbearer", "voodoo", "clockmaker", "plague_doctor", "moonshiner", "gravedigger", "captain" };

        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        private static GameLoop Hero(HeroClass heroClass, params Modifier[] modifiers)
        {
            var loop = TestWorld.Loop();
            loop.State.Class = heroClass;
            foreach (var modifier in modifiers)
                loop.Hero.Add(modifier);
            return loop;
        }

        private static Modifier AllyPoisonLine => new Modifier(Damage, ModifierKind.Increased, 0.05, StatTags.None, ModifierPool.General, ModifierCondition.AllyPoison);

        [Test]
        public void ALinkLineCountsOnlyForAnAllysMark()
        {
            var loop = Hero(HeroClass.Viking, AllyPoisonLine);
            loop.Tick();
            var plain = loop.Hero.Value(Damage, 1);

            loop.State.PoisonStacks = 10;
            loop.Tick();
            Assert.That(loop.Hero.Value(Damage, 1), Is.EqualTo(plain));

            loop.State.PoisonStacks = 0;
            loop.Synergy.SetAlly(1, true, HeroClass.PlagueDoctor, Mark("poison"));
            loop.Tick();
            Assert.That(loop.Hero.Value(Damage, 1) / plain, Is.EqualTo(1.05).Within(1e-9));
        }

        [Test]
        public void AnAllysMarkAlsoCountsAsTheMarkOnTheEnemy()
        {
            var loop = Hero(HeroClass.Viking);
            loop.Synergy.SetAlly(1, true, HeroClass.PlagueDoctor, Mark("poison"));
            loop.Tick();

            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.Poison), Is.Not.Zero);
            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.AllyPoison), Is.Not.Zero);
        }

        [Test]
        public void ATreeReactionWorksOnlyForTheHeroWhoTookIt()
        {
            var index = Rules.Reactions.ToList().FindIndex(reaction => reaction.Id == "sand_in_the_hourglass");
            Assert.That(Rules.Reactions[index].Unlock?.Name, Is.EqualTo("reactionSandInTheHourglass"));
            var without = Hero(HeroClass.Nomad);
            var with = Hero(HeroClass.Nomad, new Modifier(Rules.Reactions[index].Unlock!.Value, ModifierKind.Flat, 1));
            foreach (var loop in new[] { without, with })
            {
                loop.State.BlindTicks = 50;
                loop.Synergy.SetAlly(1, true, HeroClass.Clockmaker, Mark("time"));
                loop.Tick();
            }

            Assert.That(without.Synergy.ReactionActive(index), Is.False);
            Assert.That(with.Synergy.ReactionActive(index), Is.True);
            Assert.That(with.Hero.Value(new StatId("bossTimerSlow"), 1), Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void NewMarksAreKnown()
        {
            foreach (var mark in new[] { "blind", "cold", "bond", "blood" })
                Assert.That(Mark(mark), Is.Not.Zero, mark);
        }

        [Test]
        public void EveryClassInTheGameHasFourLinkNodesAndOneSecondResponse()
        {
            foreach (var heroClass in InGame)
            {
                var tree = TestWorld.Data.ClassTreeOf(heroClass)!;
                var links = tree.Nodes.Where(node => node.ModifiersAt(1).Any(modifier => ModifierConditions.IsAlly(modifier.When))).ToList();
                var second = tree.Nodes.Where(node => node.ModifiersAt(1).Any(modifier => modifier.Stat == Rules.SecondResponseStat)).ToList();

                Assert.That(links.Count, Is.EqualTo(4), heroClass);
                Assert.That(second.Count, Is.EqualTo(1), heroClass);
                Assert.That(links, Does.Contain(second[0]), heroClass);
            }
        }

        [Test]
        public void TheLinkNodeWakesTheSecondResponse()
        {
            var tree = TestWorld.Data.ClassTreeOf("viking")!;
            var node = tree.Nodes.Single(candidate => candidate.ModifiersAt(1).Any(modifier => modifier.Stat == Rules.SecondResponseStat));
            var loop = Hero(HeroClass.Viking);
            foreach (var modifier in node.ModifiersAt(1))
                loop.Hero.Add(modifier);
            loop.Synergy.SetAlly(0, true, HeroClass.Metalhead, Mark("aura"));

            loop.Tick();

            Assert.That(loop.Hero.Value(new StatId("crushRageThreshold"), 0), Is.EqualTo(-0.1).Within(1e-9));
        }
    }
}
