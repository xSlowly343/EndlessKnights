using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class ShowQueueTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GemDef Auto => Gem("runedancer_auto_1");

        private static GemDef Heavy => Gem("runedancer_strong_1");

        [Test]
        public void ASkillCastByHandPlaysInFullAtOnceEvenOverAnother()
        {
            var queue = new ShowQueue(ShowRules.OwnSkills);
            queue.Offer(Auto, 0, false, 0);
            Assert.That(queue.TryTake(0, out var first), Is.True);
            Assert.That(first.Size, Is.EqualTo(ShowSize.Full));

            queue.Offer(Heavy, 0, true, 0.1);

            Assert.That(queue.TryTake(0.1, out var heavy), Is.True);
            Assert.That(heavy.Gem, Is.SameAs(Heavy));
            Assert.That(heavy.Size, Is.EqualTo(ShowSize.Full));
            Assert.That(queue.UrgentBusy(0.1 + ShowRules.OwnSkills.UrgentSeconds - 0.01), Is.True);
            Assert.That(queue.UrgentBusy(0.1 + ShowRules.OwnSkills.UrgentSeconds), Is.False);
        }

        [Test]
        public void AnAutoCastWaitsForAFreeMomentAndThenPlaysInFull()
        {
            var rules = ShowRules.OwnSkills;
            var queue = new ShowQueue(rules);
            queue.Offer(Auto, 0, false, 0);
            queue.TryTake(0, out _);
            var late = rules.FullSeconds - rules.WaitSeconds + 0.05;
            queue.Offer(Heavy, 0, false, late);

            Assert.That(queue.TryTake(late + 0.01, out _), Is.False);
            Assert.That(queue.Waiting, Is.EqualTo(1));

            Assert.That(queue.TryTake(rules.FullSeconds, out var cue), Is.True);
            Assert.That(cue.Size, Is.EqualTo(ShowSize.Full));
        }

        [Test]
        public void AnAutoCastThatWaitedTooLongPlaysSmall()
        {
            var rules = ShowRules.OwnSkills;
            var queue = new ShowQueue(rules);
            queue.Offer(Heavy, 0, true, 0);
            queue.TryTake(0, out _);
            queue.Offer(Auto, 0, false, 0.05);

            Assert.That(queue.TryTake(0.05 + rules.WaitSeconds, out _), Is.False);
            Assert.That(queue.TryTake(0.06 + rules.WaitSeconds, out var cue), Is.True);

            Assert.That(cue.Size, Is.EqualTo(ShowSize.Small));
            Assert.That(queue.Waiting, Is.Zero);
        }

        [Test]
        public void PartySkillsPlayOneByOneWithAGapAndTheOverflowPlaysSmall()
        {
            var rules = ShowRules.PartySkills;
            var queue = new ShowQueue(rules);
            queue.Offer(Auto, 1, false, 0);
            Assert.That(queue.TryTake(0, out var first), Is.True);
            Assert.That(first.Source, Is.EqualTo(1));
            queue.Offer(Heavy, 2, false, 0.01);
            queue.Offer(Auto, 3, false, 0.02);
            queue.Offer(Heavy, 1, false, 0.03);

            Assert.That(queue.TryTake(0.04, out var pushedOut), Is.True);
            Assert.That(pushedOut.Size, Is.EqualTo(ShowSize.Small));
            Assert.That(pushedOut.Source, Is.EqualTo(2));
            Assert.That(queue.TryTake(0.04, out _), Is.False);

            var free = rules.FullSeconds + rules.GapSeconds;
            Assert.That(queue.TryTake(free - 0.01, out _), Is.False);
            Assert.That(queue.TryTake(free, out var second), Is.True);
            Assert.That(second.Size, Is.EqualTo(ShowSize.Full));
            Assert.That(second.Source, Is.EqualTo(3));
            Assert.That(queue.TryTake(free + 0.01, out _), Is.False);
        }

        [Test]
        public void TheSlashGrowsWithTheHitsItHadToSkip()
        {
            var gate = new SlashGate();
            Assert.That(gate.Take(), Is.Zero);

            gate.Hit();
            Assert.That(gate.Take(), Is.EqualTo(1));

            gate.Hit();
            gate.Hit();
            Assert.That(gate.Take(), Is.EqualTo(2));

            gate.Hit(9);
            Assert.That(gate.Pending, Is.EqualTo(SlashGate.HitsForThirdTier));
            Assert.That(gate.Take(), Is.EqualTo(SlashGate.MaxTier));
            Assert.That(gate.Pending, Is.Zero);
        }

        [Test]
        public void AlliesSlashInTurnWithAGapBetweenThem()
        {
            var turns = new AllySlashTurns(3);
            var ready = new[] { true, true, true };
            turns.Hit(0, 5);
            turns.Hit(2, 1);

            Assert.That(turns.TryTake(0, ready, out var first, out var firstTier), Is.True);
            Assert.That(first, Is.EqualTo(0));
            Assert.That(firstTier, Is.EqualTo(SlashGate.MaxTier));
            turns.Hit(0, 1);

            Assert.That(turns.TryTake(AllySlashTurns.DefaultGapSeconds - 0.01, ready, out _, out _), Is.False);
            Assert.That(turns.TryTake(AllySlashTurns.DefaultGapSeconds, ready, out var second, out var secondTier), Is.True);
            Assert.That(second, Is.EqualTo(2));
            Assert.That(secondTier, Is.EqualTo(1));

            Assert.That(turns.TryTake(1, ready, out var third, out _), Is.True);
            Assert.That(third, Is.EqualTo(0));
            Assert.That(turns.TryTake(2, ready, out _, out _), Is.False);
        }

        [Test]
        public void AnAllyWhoIsStillSwingingKeepsItsHitsForLater()
        {
            var turns = new AllySlashTurns(2);
            turns.Hit(0, 2);

            Assert.That(turns.TryTake(0, new[] { false, true }, out _, out _), Is.False);
            Assert.That(turns.Pending(0), Is.EqualTo(2));

            Assert.That(turns.TryTake(0, new[] { true, true }, out var ally, out var tier), Is.True);
            Assert.That(ally, Is.Zero);
            Assert.That(tier, Is.EqualTo(2));
        }

        [Test]
        public void ASkillIsCastByHandWhenItStandsOnAMouseButton()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 30 });
            loop.Skills.Equip(Auto, role: SkillSlotRole.Auto);
            loop.Skills.Equip(Heavy, role: SkillSlotRole.Right);

            Assert.That(SkillShowKind.CastByHand(loop.State.Skills, Heavy), Is.True);
            Assert.That(SkillShowKind.CastByHand(loop.State.Skills, Auto), Is.False);
            Assert.That(SkillShowKind.CastByHand(loop.State.Skills, Gem("runedancer_hit_1")), Is.False);
        }
    }
}
