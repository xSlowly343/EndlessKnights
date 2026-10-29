using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class RaidTests
    {
        private static RaidRules Rules => TestWorld.Data.Raid;

        private static (GameLoop Loop, FakeInputSource Input) Hero()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Stage = 20, HighestStage = 20, RecordStage = 20 });
            loop.Tick();
            loop.Raid.Take();
            return (loop, input);
        }

        [SetUp]
        public void SetUp() => Texts.UseLanguage("ru");

        [TearDown]
        public void TearDown() => Texts.UseLanguage("ru");

        [TestCase("ru")]
        [TestCase("en")]
        public void TheGameFileHasFourLayersAndEveryOneHasItsTexts(string language)
        {
            Texts.UseLanguage(language);
            var rules = RaidRules.Load(TestData.ReadGameDataFile(RaidRules.FileName));

            Assert.That(rules.Layers.Count, Is.EqualTo(RaidRules.InputKinds));
            Assert.That(Texts.Has("raid.boss." + rules.Boss), Is.True);
            foreach (var layer in rules.Layers)
            {
                Assert.That(Texts.Has("raid.layer." + layer.Id), Is.True, layer.Id);
                Assert.That(Texts.Has("raid.input." + layer.Id), Is.True, layer.Id);
            }
            Assert.That(rules.Layers[rules.LayerOf(RaidInput.Keys)].Id, Is.EqualTo("flesh"));
        }

        [TestCase(@"""input"": ""idle""", @"""input"": ""keys""", "already takes down")]
        [TestCase(@"""input"": ""idle""", @"""input"": ""wheel""", "unknown input")]
        [TestCase(@"""pushSeconds"": 15", @"""pushSeconds"": 1", "pushSeconds")]
        public void BadRulesAreRejected(string from, string to, string message)
        {
            var error = Assert.Throws<ContentException>(() => RaidRules.Load(TestData.ReadGameDataFile(RaidRules.FileName).Replace(from, to)));

            Assert.That(error!.Message, Does.Contain(message));
        }

        [Test]
        public void KeysGoToTheFleshAndMouseToTheArmor()
        {
            var (loop, input) = Hero();
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            var before = loop.State.TotalDamage;

            input.PressKeys(6);
            loop.Tick();

            var keys = loop.Raid.Share(RaidInput.Keys);
            Assert.That(keys, Is.EqualTo(((loop.State.TotalDamage - before) / hp).ToDouble()).Within(1e-9));
            Assert.That(loop.Raid.Share(RaidInput.Mouse), Is.Zero);
            input.MoveMouse(2000);
            loop.Tick();
            Assert.That(loop.Raid.Share(RaidInput.Mouse), Is.GreaterThan(0));
            Assert.That(loop.Raid.Share(RaidInput.Idle), Is.Zero);
        }

        [Test]
        public void SkillHitsGoToTheShieldAndAnyHitDuringAPauseGoesToTheCurse()
        {
            var (loop, _) = Hero();
            var events = new EventQueue();
            var tally = new RaidTally(Rules, loop.State, loop.Stages, loop.Ghost, events);
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            events.Add(GameEvent.Damage(0, hp * 0.5, DamageSource.Skill, HitFlags.None));
            loop.State.IdleTicks = 0;

            tally.Tick(0);
            loop.State.IdleTicks = (long)(Rules.IdleSeconds * GameLoop.TicksPerSecond);
            events.Add(GameEvent.Damage(1, hp * 0.25, DamageSource.Keyboard, HitFlags.None));
            tally.Tick(1);

            Assert.That(tally.Share(RaidInput.Skill), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(tally.Share(RaidInput.Keys), Is.Zero);
            Assert.That(tally.Share(RaidInput.Idle), Is.EqualTo(0.25).Within(1e-12), "DoT, minions and anything else while idle take down the curse");
        }

        [Test]
        public void AFriendsGhostDoesNotCountForYourRaid()
        {
            var (loop, _) = Hero();
            loop.Ghost.Use(new GhostSnapshot("friend", "Аня", HeroClass.Voodoo, 0.5, 0, 1));

            for (var i = 0; i < 50; i++)
                loop.Tick();

            Assert.That(loop.Ghost.Damage.Sign, Is.GreaterThan(0));
            Assert.That(loop.Raid.Take().Total, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void TakingThePushEmptiesTheTallyAndReportsTheSeconds()
        {
            var (loop, input) = Hero();
            for (var i = 0; i < 30; i++)
            {
                input.PressKeys(4);
                loop.Tick();
            }

            var push = loop.Raid.Take();

            Assert.That(push.Seconds, Is.EqualTo(3).Within(1e-9));
            Assert.That(push.Counts.KeyPresses, Is.EqualTo(120), "the clan challenges count the keys pressed");
            Assert.That(push.Keys, Is.GreaterThan(0));
            Assert.That(push.Of(RaidInput.Keys), Is.EqualTo(push.Keys));
            Assert.That(loop.Raid.Take().Total, Is.Zero);
            Assert.That(loop.Raid.Seconds, Is.Zero);
        }

        [Test]
        public void TallyingDoesNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var (loop, _) = Hero();
            var events = new EventQueue();
            var tally = new RaidTally(Rules, loop.State, loop.Stages, loop.Ghost, events);
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            for (var i = 0; i < 64; i++)
                events.Add(GameEvent.Damage(i, hp * 0.01, (DamageSource)(1 + i % 3), HitFlags.None));
            tally.Tick(0);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
                tally.Tick(0);
            tally.Take();

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void TheClanPacketCountsKeysClicksMouseAndBossesSinceTheLastOne()
        {
            var (loop, input) = Hero();
            input.PressKeys(7);
            input.ClickLeft(2);
            input.MoveMouse(96 / 0.0254f);
            loop.Tick();
            loop.State.BossKills += 3;

            var counts = loop.Raid.Take().Counts;

            Assert.That(counts.KeyPresses, Is.EqualTo(7));
            Assert.That(counts.Clicks, Is.EqualTo(2));
            Assert.That(counts.MouseMeters, Is.EqualTo(1).Within(1e-3));
            Assert.That(counts.Bosses, Is.EqualTo(3));
            Assert.That(loop.Raid.Take().Counts.KeyPresses, Is.Zero);
        }

        [TestCase("ru")]
        [TestCase("en")]
        public void EveryClanChallengeHasItsTexts(string language)
        {
            Texts.UseLanguage(language);
            var rules = ClanChallengeRules.Load(TestData.ReadGameDataFile(ClanChallengeRules.FileName));

            Assert.That(rules.Challenges.Count, Is.GreaterThanOrEqualTo(rules.PerDay));
            foreach (var challenge in rules.Challenges)
                Assert.That(Texts.Has("challenge.title." + challenge.Counter), Is.True, challenge.Id);
        }

        [Test]
        public void ChallengesReadLikeTheMockup()
        {
            Assert.That(ClanChallengeText.Title("keys", 100_000), Is.EqualTo("Клан набирает 100K нажатий"));
            Assert.That(ClanChallengeText.Title("mouseMeters", 10_000), Is.EqualTo("Вместе 10 км мыши"));
            Assert.That(ClanChallengeText.Progress("keys", 82_345, 100_000, false), Is.EqualTo("82.3K из 100K"));
            Assert.That(ClanChallengeText.Progress("mouseMeters", 6_400, 10_000, false), Is.EqualTo("6.4 км из 10 км"));
            Assert.That(ClanChallengeText.Progress("bosses", 150, 150, true), Is.EqualTo("выполнено"));
            Assert.That(ClanChallengeText.Yours("keys", 3_100, 10), Is.EqualTo("ваши 3.1K · Печати +10"));
            Assert.That(ClanChallengeText.Yours("clicks", 0, 10), Is.EqualTo("помогите — Печати +10"));
        }

        [Test]
        public void TheRaidCardReadsPlainly()
        {
            Assert.That(RaidText.Boss("devourer"), Is.EqualTo("Пожиратель Миров"));
            Assert.That(RaidText.Layer("shield", 0, 1000), Is.EqualTo("Щит · снят"));
            Assert.That(RaidText.Layer("armor", 340, 1000), Is.EqualTo("Броня · 34%"));
            Assert.That(RaidText.Hp(0.58), Is.EqualTo("HP 58%"));
            Assert.That(RaidText.Own(0.021, 9, 27), Is.EqualTo("Ваш вклад 2.1% · место 9 из 27"));
            Assert.That(RaidText.EndsIn(1_000_000 + (3 * 24 + 7) * 3_600_000L + 1000, 1_000_000), Is.EqualTo("3 д 7 ч"));
            Assert.That(RaidText.EndsIn(1_000_000 + 40 * 60_000L, 1_000_000), Is.EqualTo("40 мин"));
            Assert.That(RaidText.Last(true, 9, 27, 41), Is.EqualTo("Прошлый рейд: босс повержен · место 9 из 27 · Печати +41"));
        }
    }
}
