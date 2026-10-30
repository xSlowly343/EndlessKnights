using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class ClassChoiceTests
    {
        private static readonly ClockStamp Clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

        private static GameLoop Novice(FakeInputSource? input = null, HeroClass placeholder = HeroClass.Viking)
        {
            var loop = TestWorld.Loop(input ?? new FakeInputSource(), new GameState { Class = placeholder, ClassChosen = false });
            loop.Book.GrantStartingGems();
            return loop;
        }

        [Test]
        public void NoviceHasNoClassWeaponNoClassSkillsAndCannotTakeNodes()
        {
            var loop = Novice();

            Assert.That(loop.State.ClassName, Is.EqualTo(HeroClassNames.Novice));
            Assert.That(loop.State.Weapons.Right, Is.Null);
            Assert.That(loop.State.Weapons.Left, Is.Null);
            Assert.That(loop.State.Skills.Select(slot => slot.Skill.Id), Is.EquivalentTo(new[] { "through_stride", "lull" }));
            Assert.That(loop.State.Skills.All(slot => slot.Def.Weapon == null), Is.True);
            Assert.That(loop.ClassChoiceReady, Is.False, "the choice opens on the second level");
            var node = TestWorld.Data.Tree.Nodes.First(n => n.Type == TreeNodeType.Small);
            Assert.That(loop.AllocateNode(node.Id), Is.EqualTo(AllocationResult.ChooseClassFirst));
            Assert.That(loop.ChooseClass(HeroClass.Ronin), Is.EqualTo(ClassChoiceResult.NotYet));
        }

        [Test]
        public void NoviceKeysDoNotRunTheMechanicsOfThePlaceholderClass()
        {
            var input = new FakeInputSource();
            var loop = Novice(input, HeroClass.Ronin);

            for (var tick = 0; tick < 20; tick++)
            {
                input.PressKeys(3);
                loop.Tick();
            }

            Assert.That(loop.State.Combo, Is.Zero, "the series belongs to Wandering Blade, not to a novice");
            Assert.That(loop.Resource.Rules, Is.SameAs(TestWorld.Data.Combat.Resources.Fallback));
        }

        [Test]
        public void ChoosingTheClassOnTheSecondLevelGivesItsWeaponSkillsResourceAndTree()
        {
            var loop = Novice();
            loop.State.HeroLevel = TestWorld.Data.TreePoints.ClassChoiceLevel;

            Assert.That(loop.ClassChoiceReady, Is.True);
            Assert.That(loop.ChooseClass(HeroClass.Ronin), Is.EqualTo(ClassChoiceResult.Chosen));

            var state = loop.State;
            Assert.That(state.ClassChosen, Is.True);
            Assert.That(state.Class, Is.EqualTo(HeroClass.Ronin));
            Assert.That(state.Tree.StartClass, Is.EqualTo("ronin"));
            Assert.That(state.Tree.PointsLeft, Is.GreaterThan(0), "points earned as a novice are kept");
            Assert.That(state.Weapons.Right?.Type.Id ?? state.Weapons.Left?.Type.Id, Is.EqualTo("katana"));
            Assert.That(state.Skills[0].Def.Weapon, Is.EqualTo("katana"));
            Assert.That(state.Skills[1].Def.Weapon, Is.EqualTo("katana"));
            Assert.That(state.OwnedGems.Select(gem => gem.Id), Does.Contain("through_stride").And.Contain("lull"), "general skills stay with the hero");
            Assert.That(loop.Resource.Rules, Is.SameAs(TestWorld.Data.Combat.Resources.For("ronin")));
            Assert.That(loop.ClassChoiceReady, Is.False);
            Assert.That(loop.ChooseClass(HeroClass.Viking), Is.EqualTo(ClassChoiceResult.AlreadyChosen));

            var step = state.Tree.Allocatable(TestWorld.Data.Tree).First(node => node.Type == TreeNodeType.Small);
            Assert.That(loop.AllocateNode(step.Id), Is.EqualTo(AllocationResult.Allocated));
        }

        [Test]
        public void TheSaveRemembersANoviceAndOldSavesHaveAClass()
        {
            var loop = Novice();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, Clock), TestWorld.Data).State;

            Assert.That(loaded.ClassChosen, Is.False);
            Assert.That(loaded.Tree.StartClass, Is.EqualTo(HeroClassNames.Novice));
            Assert.That(new GameState().ClassChosen, Is.True, "a state without the flag is a chosen class, as in saves before the novice");
        }

        [Test]
        public void TheBotChoosesItsClassWhenTheChoiceOpens()
        {
            var loop = Novice(placeholder: HeroClass.Captain);
            var bot = new PlayerBot(TestWorld.Data, loop, 20);

            bot.Act(1);
            Assert.That(loop.State.ClassChosen, Is.False);

            loop.State.HeroLevel = TestWorld.Data.TreePoints.ClassChoiceLevel;
            bot.Act(1);
            Assert.That(loop.State.ClassChosen, Is.True);
            Assert.That(loop.State.Class, Is.EqualTo(HeroClass.Captain));
            Assert.That(loop.State.Tree.PointsSpent, Is.GreaterThan(0));
        }
    }
}
