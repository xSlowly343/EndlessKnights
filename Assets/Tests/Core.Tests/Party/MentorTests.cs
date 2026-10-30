using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class MentorTests
    {
        private static MentorRules Rules => TestWorld.Data.Mentor;

        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [TestCase("ru")]
        [TestCase("en")]
        public void TheGameFileLoadsAndEveryPerkHasItsText(string language)
        {
            Texts.UseLanguage(language);
            var rules = MentorRules.Load(TestData.ReadGameDataFile(MentorRules.FileName));

            Assert.That(rules.StudentDays, Is.EqualTo(7), "the first week, by the decision of 23.10.2026");
            Assert.That(rules.GoldBonus, Is.EqualTo(0.5));
            Assert.That(rules.XpBonus, Is.EqualTo(0.5));
            foreach (var step in rules.Steps)
            {
                foreach (var perk in new[] { step.MentorPerk, step.StudentPerk })
                {
                    if (perk.Length > 0)
                        Assert.That(Texts.Has("mentor.perk." + perk), Is.True, perk);
                }
            }
        }

        [TestCase(@"""hours"": 5,", @"""hours"": 1,", "hours must grow")]
        [TestCase(@"""mentorMinDays"": 30", @"""mentorMinDays"": 7", "studentDays")]
        public void BadRulesAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => MentorRules.Load(TestData.ReadGameDataFile(MentorRules.FileName).Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void BesideTheMentorTheStudentEarnsHalfAgainGoldAndExperience()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var gold = loop.Hero.Value(CombatStats.Gold, 1);
            var xpBefore = loop.State.HeroXp;
            var levelBefore = loop.State.HeroLevel;

            loop.Mentor.Set(true);
            loop.Levels.OnKill(EnemyType.Normal);

            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(gold * (1 + Rules.GoldBonus)).Within(1e-9));
            Assert.That(loop.State.HeroLevel, Is.EqualTo(levelBefore));
            Assert.That(loop.State.HeroXp - xpBefore, Is.EqualTo(TestWorld.Data.Combat.Hero.XpPerKill * (1 + Rules.XpBonus)).Within(1e-9));
            loop.Mentor.Set(false);
            Assert.That(loop.Hero.Value(CombatStats.Gold, 1), Is.EqualTo(gold).Within(1e-9));
            Assert.That(loop.Levels.XpMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void TheBonusNeedsBothFightingInTheSameLinkedParty()
        {
            var roster = new RosterMessage { Player0 = 111, Player1 = 222, Presence0 = (byte)PartyPresence.Fighting, Presence1 = (byte)PartyPresence.Fighting };

            Assert.That(MentorBonus.FightsBeside(PartyLink.Linked, roster, 0, 222), Is.True);
            Assert.That(MentorBonus.FightsBeside(PartyLink.NoSignal, roster, 0, 222), Is.False);
            Assert.That(MentorBonus.FightsBeside(PartyLink.Linked, roster, 0, 333), Is.False, "the mentor is not in this party");
            roster.Presence1 = (byte)PartyPresence.Resting;
            Assert.That(MentorBonus.FightsBeside(PartyLink.Linked, roster, 0, 222), Is.False, "the mentor is resting");
        }

        [Test]
        public void TheEarlyBookSlotOpensTheNextSlotOfTheBookNow()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var book = TestWorld.Data.SkillBook;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(book.BaseSlots));
            var later = loop.Book.UnlockStageOf(book.BaseSlots + 1);

            loop.Mentor.UsePerks(new[] { "graduate_frame", MentorRules.EarlyBookSlotPerk }, 0);

            Assert.That(loop.State.EarlyBookSlot, Is.True);
            Assert.That(loop.Book.SlotCount, Is.EqualTo(book.BaseSlots + 1));
            Assert.That(loop.Book.UnlockStageOf(book.BaseSlots), Is.Zero, "the early slot is open");
            Assert.That(loop.Book.UnlockStageOf(book.BaseSlots + 1), Is.EqualTo(TestWorld.Data.SkillBook.UnlockStageOf(book.BaseSlots)), "the next slot opens one milestone earlier");
            Assert.That(later, Is.GreaterThan(loop.Book.UnlockStageOf(book.BaseSlots + 1)));
            loop.State.RecordStage = 100_000;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(book.MaxSlots), "the perk never gives more than all the slots");
        }

        [Test]
        public void MentorCoopPointsGoToTheTreeAsCoopHelpPoints()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var before = loop.State.Tree.PointsEarned;

            loop.Mentor.UsePerks(System.Array.Empty<string>(), 2);
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(before + 2));

            loop.Mentor.UsePerks(System.Array.Empty<string>(), 1);
            Assert.That(loop.State.MentorCoopPoints, Is.EqualTo(2), "an older answer of the server takes nothing back");
            loop.Mentor.UsePerks(System.Array.Empty<string>(), 9);
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(before + 5), "coop help gives five points at most");
        }

        [Test]
        public void CoopHelpPointsOfTheServerGoToTheTreeAndNeverTakeMentorPointsAway()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var before = loop.State.Tree.PointsEarned;
            loop.Mentor.UsePerks(System.Array.Empty<string>(), 2);

            loop.Mentor.UseCoopHelp(1);
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(before + 2), "an old save with mentor points keeps them");

            loop.Mentor.UseCoopHelp(4);
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(before + 4));
            loop.Mentor.UseCoopHelp(3);
            Assert.That(loop.State.CoopHelpPoints, Is.EqualTo(4), "an older answer of the server takes nothing back");
        }

        [Test]
        public void TheMentorMarksAreKeptUpToTheLimit()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var marks = new string[Rules.MaxMarks + 3];
            for (var i = 0; i < marks.Length; i++)
                marks[i] = "node_" + i;

            loop.Mentor.UseMarks(marks);

            Assert.That(loop.Mentor.Marks.Count, Is.EqualTo(Rules.MaxMarks));
            Assert.That(loop.Mentor.IsMarked("node_0"), Is.True);
            Assert.That(loop.Mentor.IsMarked("node_" + Rules.MaxMarks), Is.False);
        }

        [Test]
        public void TheMentorCardReadsLikeTheMockup()
        {
            const long now = 1_800_000_000_000;
            const long day = 86_400_000;

            Assert.That(MentorText.WithStudent("Лёша", now + 4 * day + 1000, now, 7), Is.EqualTo("Наставник · ученик Лёша, день 3 из 7"));
            Assert.That(MentorText.Together(3 * 3600 + 40 * 60, Rules, 1), Is.EqualTo("Вместе в бою 3 ч 40 мин из 5 ч до следующей награды"));
            Assert.That(MentorText.Step(Rules.Steps[0], true, true), Is.EqualTo("1 ч ✓"));
            Assert.That(MentorText.Step(Rules.Steps[2], false, true), Is.EqualTo("10 ч · плащ"));
            Assert.That(MentorText.Step(Rules.Steps[3], false, true), Is.EqualTo("20 ч · очко дерева"));
            Assert.That(MentorText.Step(Rules.Steps[2], false, false), Is.EqualTo("10 ч · рамка «Выпускник»"));
            Assert.That(MentorText.Marks(4, 10), Is.EqualTo("Метки на дереве 4/10"));
            Assert.That(MentorText.Bonus(Rules.GoldBonus), Is.EqualTo("+50%"));
            var letter = new Game.Core.Mail.MailLetter("mentor-1", Game.Core.Mail.MailLetter.MentorKind, now, 0, 0, 1, "", new Game.Core.Content.Rift.RiftReward(0, 3, 0), false);
            Assert.That(MailText.Title(letter), Is.EqualTo("Подарок наставника"));
            Assert.That(MailText.Body(letter), Is.EqualTo("1 ч в бою вместе с наставником"));
            Assert.That(MailText.Reward(letter), Does.Contain("3"));
        }
    }
}
