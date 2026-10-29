using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class GhostTests
    {
        private static PartyGhostRules Rules => TestWorld.Data.Party.Ghost;

        private static (GameLoop Loop, FakeInputSource Input) Hero()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Stage = 20, HighestStage = 20, RecordStage = 20 });
            loop.Tick();
            return (loop, input);
        }

        private static GhostSnapshot Friend(double shareRate, double activity, int marks = 0) =>
            new GhostSnapshot("friend", "Аня", HeroClass.Voodoo, shareRate, marks, activity);

        [Test]
        public void TheGhostHitsAShareOfYourOwnEnemyEverySecond()
        {
            var (loop, _) = Hero();
            loop.Ghost.Use(Friend(0.4, 0.5));
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            var before = loop.State.EnemyHp;

            loop.Tick();

            var expected = hp * (Rules.Power * 0.5 * 0.4 * GameLoop.TickSeconds);
            Assert.That((before - loop.State.EnemyHp).ToDouble(), Is.EqualTo(expected.ToDouble()).Within(expected.ToDouble() * 1e-6));
            Assert.That(loop.Ghost.ShareRate, Is.EqualTo(Rules.Power * 0.5 * 0.4).Within(1e-12));
            Assert.That(loop.Ghost.Damage.ToDouble(), Is.EqualTo(expected.ToDouble()).Within(expected.ToDouble() * 1e-6));
        }

        [Test]
        public void TheGhostKillsAloneAtItsOwnPaceAndCountsTheBossesItHelped()
        {
            var (loop, _) = Hero();
            loop.Ghost.Use(Friend(2, 1));
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            var ticks = (int)System.Math.Ceiling((loop.State.EnemyMaxHp / hp).ToDouble() / (Rules.Power * 2) * GameLoop.TicksPerSecond);

            for (var i = 0; i < ticks - 2; i++)
                loop.Tick();
            Assert.That(loop.State.Kills, Is.Zero, "the ghost is a helper, not an instant kill");
            for (var i = 0; i < 3; i++)
                loop.Tick();

            Assert.That(loop.State.Kills, Is.EqualTo(1), "a ghost alone still kills, at its own pace");
            for (var i = 0; i < 2000 && loop.Ghost.BossesHelped == 0; i++)
                loop.Tick();
            Assert.That(loop.Ghost.BossesHelped, Is.EqualTo(1));
            Assert.That(loop.Ghost.TakeThanks(), Is.EqualTo(1), "the owner is thanked once for each boss");
            Assert.That(loop.Ghost.TakeThanks(), Is.Zero);
        }

        [Test]
        public void InAPartyTheGhostStaysSilentBecauseTheFriendIsThereForReal()
        {
            var (loop, _) = Hero();
            loop.Ghost.Use(Friend(0.4, 1));
            loop.Stages.Link();

            Assert.That(loop.Ghost.Fighting, Is.False);
            Assert.That(loop.Ghost.ShareRate, Is.Zero);
            loop.Tick();
            Assert.That(loop.Ghost.Damage.Sign, Is.Zero);
        }

        [Test]
        public void TheGhostBringsItsMarksAndGoesAwayWhenCleared()
        {
            var (loop, _) = Hero();
            var curse = loop.Data.Synergy.MaskOf(new[] { "curse" });
            loop.Ghost.Use(Friend(0.1, 1, curse));

            loop.Tick();
            Assert.That(loop.Synergy.AllyMarks & curse, Is.EqualTo(curse), "in solo the ghost gives its marks");

            loop.Ghost.Clear();
            loop.Tick();
            Assert.That(loop.Synergy.AllyMarks & curse, Is.Zero);
            Assert.That(loop.Ghost.Present, Is.False);
        }

        [Test]
        public void MarksTravelAsNamesAndComeBackAsTheSameMask()
        {
            var synergy = TestWorld.Data.Synergy;
            var mask = synergy.MaskOf(new[] { "curse", "lightning" });

            Assert.That(synergy.IdsOf(mask), Is.EquivalentTo(new[] { "curse", "lightning" }));
            Assert.That(synergy.MaskOf(synergy.IdsOf(mask)), Is.EqualTo(mask));
        }

        [Test]
        public void AGhostWithoutPlayOrPowerDoesNotCome()
        {
            var (loop, _) = Hero();

            loop.Ghost.Use(Friend(0.4, 0));
            Assert.That(loop.Ghost.Present, Is.False);
            loop.Ghost.Use(Friend(0, 1));
            Assert.That(loop.Ghost.Present, Is.False);
        }

        [Test]
        public void YourOwnSnapshotCountsYourPaceAndYourActiveSeconds()
        {
            var (loop, input) = Hero();
            for (var i = 0; i < 25; i++)
            {
                input.PressKeys(2);
                loop.Tick();
            }
            for (var i = 0; i < 10; i++)
                loop.Tick();

            Assert.That(loop.Ghost.TakeActiveSeconds(), Is.EqualTo(2));
            Assert.That(loop.Ghost.TakeActiveSeconds(), Is.Zero, "the half second carries over");
            Assert.That(loop.Ghost.OwnShareRate, Is.GreaterThan(0));
            var hp = loop.Stages.Table.EnemyHp(loop.State.Stage);
            Assert.That(loop.Ghost.OwnShareRate, Is.EqualTo((loop.State.RecentDps / hp).ToDouble()).Within(1e-9));
        }
    }
}
