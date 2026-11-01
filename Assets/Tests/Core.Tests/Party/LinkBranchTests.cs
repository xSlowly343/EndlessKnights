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
        private static readonly string[] InGame = { "runedancer", "duelist", "windshot", "shardcaller", "sandwalker", "minstrel", "dreamcatcher", "lanternbearer", "whisperer", "clockwright", "alchemist", "dowser", "soulbinder", "ghostcaptain" };

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
            var loop = Hero(HeroClass.Runedancer, AllyPoisonLine);
            loop.Tick();
            var plain = loop.Hero.Value(Damage, 1);

            loop.State.PoisonStacks = 10;
            loop.Tick();
            Assert.That(loop.Hero.Value(Damage, 1), Is.EqualTo(plain));

            loop.State.PoisonStacks = 0;
            loop.Synergy.SetAlly(1, true, HeroClass.Alchemist, Mark("poison"));
            loop.Tick();
            Assert.That(loop.Hero.Value(Damage, 1) / plain, Is.EqualTo(1.05).Within(1e-9));
        }

        [Test]
        public void AnAllysMarkAlsoCountsAsTheMarkOnTheEnemy()
        {
            var loop = Hero(HeroClass.Runedancer);
            loop.Synergy.SetAlly(1, true, HeroClass.Alchemist, Mark("poison"));
            loop.Tick();

            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.Poison), Is.Not.Zero);
            Assert.That(loop.Hero.Conditions & ModifierConditions.Bit(ModifierCondition.AllyPoison), Is.Not.Zero);
        }

        [Test]
        public void ATreeReactionWorksOnlyForTheHeroWhoTookIt()
        {
            var index = Rules.Reactions.ToList().FindIndex(reaction => reaction.Id == "sand_in_the_hourglass");
            Assert.That(Rules.Reactions[index].Unlock?.Name, Is.EqualTo("reactionSandInTheHourglass"));
            var without = Hero(HeroClass.Sandwalker);
            var with = Hero(HeroClass.Sandwalker, new Modifier(Rules.Reactions[index].Unlock!.Value, ModifierKind.Flat, 1));
            foreach (var loop in new[] { without, with })
            {
                loop.State.BlindTicks = 50;
                loop.Synergy.SetAlly(1, true, HeroClass.Clockwright, Mark("time"));
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
        public void AnAllySettingTheMarkGivesResourceOnceWhenItAppears()
        {
            var loop = Hero(HeroClass.Sandwalker, new Modifier(CombatStats.ResourceOnAllyMark, ModifierKind.Flat, 2, StatTags.None, ModifierPool.General, ModifierCondition.AllyFire));
            loop.Tick();
            loop.State.PendingResource = 0;

            loop.Synergy.SetAlly(1, true, HeroClass.Dowser, Mark("fire"));
            loop.Synergy.Tick();
            loop.Conditions.Tick();
            Assert.That(loop.State.PendingResource, Is.EqualTo(2).Within(1e-9));

            loop.Conditions.Tick();
            Assert.That(loop.State.PendingResource, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void ResourceOnAMarkCountsOnlyTheMarkThatAppeared()
        {
            var sheet = new StatSheet();
            sheet.Add(new Modifier(CombatStats.ResourceOnAllyMark, ModifierKind.Flat, 2, StatTags.None, ModifierPool.General, ModifierCondition.AllyFire));
            sheet.Add(new Modifier(CombatStats.ResourceOnAllyMark, ModifierKind.Flat, 3, StatTags.None, ModifierPool.General, ModifierCondition.AllyStun));

            Assert.That(sheet.FlatWhen(CombatStats.ResourceOnAllyMark, ModifierConditions.Bit(ModifierCondition.AllyStun)), Is.EqualTo(3));
        }
    }
}
