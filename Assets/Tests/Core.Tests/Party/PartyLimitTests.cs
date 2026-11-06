using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyLimitTests
    {
        private static long Gate => TestWorld.Data.Stages.Gates[0].Stage;

        private static GameLoop AtTheLimit()
        {
            var loop = TestWorld.Loop();
            loop.State.Stage = Gate;
            loop.State.HighestStage = Gate;
            loop.State.RecordStage = Gate;
            loop.Stages.Link();
            return loop;
        }

        [Test]
        public void AnOrdinaryPartyBossDoesNotClearTheLimit()
        {
            var loop = AtTheLimit();
            loop.Stages.SpawnLinked(true, 1, 1, 0, false, 300);
            loop.Stages.ConfirmLinkedKill();
            Assert.That(loop.State.Stage, Is.EqualTo(Gate));
            Assert.That(loop.State.GatesCleared, Does.Not.Contain(Gate));
            Assert.That(loop.Events.CountOf(GameEventKind.GateNeedsLimitFight), Is.EqualTo(1));
        }

        [Test]
        public void TheLimitFoughtByThePartyClearsIt()
        {
            var loop = AtTheLimit();
            loop.Stages.LinkedGateBoss = true;
            loop.Stages.SpawnLinked(true, 1, 50, 0, false, 1200);
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Guardian));
            loop.Stages.ConfirmLinkedKill();
            Assert.That(loop.State.Stage, Is.EqualTo(Gate + 1));
            Assert.That(loop.State.GatesCleared, Does.Contain(Gate));
        }

        [Test]
        public void TheHostTurnsTheBossIntoTheLimitAndKeepsBossPierceOff()
        {
            var rules = TestWorld.WithPierce.Party;
            var encounter = new PartyEncounter(rules, TestWorld.WithPierce.Stages, 3);
            encounter.Join(1);
            encounter.Join(2);
            encounter.Advance();
            encounter.SetStage(0, Gate);
            encounter.SetAtGate(0, true);
            encounter.SetPierce(1, new PartyPierceStats(0, 0, 0, 0, 0.5, 0));
            for (var i = 0; i < 400 && !encounter.FightingBoss; i++)
            {
                encounter.ReportInput(0);
                encounter.ReportInput(1);
                encounter.ApplyDamage(1, encounter.EnemyId, 1000);
                encounter.Advance();
            }
            Assert.That(encounter.FightingBoss, Is.True);
            Assert.That(encounter.GateBoss, Is.True);
            Assert.That(encounter.BarFull, Is.EqualTo(rules.HpMultiplier(2) * TestWorld.WithPierce.Stages.Gates[0].HpMultiplier).Within(1e-9));
            Assert.That(encounter.BarLeft, Is.EqualTo(encounter.BarFull), "boss pierce does not touch the Limit");
            Assert.That(encounter.BossTicksLeft, Is.EqualTo((int)System.Math.Round(TestWorld.WithPierce.Stages.Gates[0].TimerSeconds * GameLoop.TicksPerSecond)));
        }
    }
}
