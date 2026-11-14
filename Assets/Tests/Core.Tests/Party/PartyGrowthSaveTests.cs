using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyGrowthSaveTests
    {
        private const long Hour = 3_600_000;
        private const long Now = 1_763_000_000_000;

        private static GameLoop Member()
        {
            var loop = TestWorld.Loop();
            loop.SetServerTime(Now);
            loop.SetPartyStanding(true, Now - 30 * 24 * Hour, 4, Now);
            loop.SetPartyTree(new Dictionary<int, int> { [1] = 3, [6] = 1 }, new[] { "party_m20_morning" });
            loop.SetPartyTrials(new[] { 4, 2 });
            loop.SetPartyBanner(new string[0]);
            return loop;
        }

        private static GameState Reloaded(GameState state) =>
            GameStateJson.Read(new ContentNode(GameStateJson.Write(state), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);

        [Test]
        public void ThePartyGrowthAnswersGoIntoTheSave()
        {
            var loop = Member();
            var saved = loop.State.PartyGrowth;
            Assert.That(saved.InParty, Is.True);
            Assert.That(saved.Members, Is.EqualTo(4));
            Assert.That(saved.TreeLevels, Is.EqualTo(new Dictionary<int, int> { [1] = 3, [6] = 1 }));
            Assert.That(saved.TreeMajors, Is.EqualTo(new[] { "party_m20_morning" }));
            Assert.That(saved.TrialBest, Is.EqualTo(new[] { 4, 2 }));
            Assert.That(saved.AnsweredUnixMs, Is.EqualTo(Now));

            var copy = Reloaded(loop.State).PartyGrowth;
            Assert.That(copy.InParty, Is.True);
            Assert.That(copy.JoinedUnixMs, Is.EqualTo(saved.JoinedUnixMs));
            Assert.That(copy.TreeLevels, Is.EqualTo(saved.TreeLevels));
            Assert.That(copy.TreeMajors, Is.EqualTo(saved.TreeMajors));
            Assert.That(copy.TrialBest, Is.EqualTo(saved.TrialBest));
            Assert.That(copy.AnsweredUnixMs, Is.EqualTo(Now));
            Assert.That(copy.SeenUnixMs, Is.EqualTo(Now));
        }

        [Test]
        public void AFreshSaveBringsThePartyPowerBackWithoutTheNetwork()
        {
            var loop = Member();
            var online = loop.PartyGrowth.DamageMultiplier;
            Assert.That(online, Is.GreaterThan(1));
            loop.SetServerTime(Now + 20 * Hour);

            var back = TestWorld.Loop(state: Reloaded(loop.State));
            Assert.That(back.PartyGrowth.InParty, Is.True);
            Assert.That(back.PartyGrowth.TreeLevels, Is.EqualTo(new Dictionary<int, int> { [1] = 3, [6] = 1 }));
            Assert.That(back.PartyGrowth.TrialBest, Is.EqualTo(new[] { 4, 2 }));
            Assert.That(back.PartyGrowth.DamageMultiplier, Is.EqualTo(online).Within(1e-12));

            back.SetServerTime(Now + 23 * Hour);
            Assert.That(back.PartyGrowth.InParty, Is.True, "a day without answers still counts");
            back.SetServerTime(Now + 25 * Hour);
            Assert.That(back.PartyGrowth.InParty, Is.False, "after a day without the server the party power is gone");
            Assert.That(back.PartyGrowth.DamageMultiplier, Is.EqualTo(1));
            Assert.That(back.State.PartyGrowth.InParty, Is.False);
        }

        [Test]
        public void AnAnswerKeepsThePowerAndLeavingClearsIt()
        {
            var loop = Member();
            loop.SetServerTime(Now + 20 * Hour);
            loop.SetPartyTrials(new[] { 5, 2 });
            loop.SetServerTime(Now + 80 * Hour);
            Assert.That(loop.PartyGrowth.InParty, Is.True, "while the game runs the answers it got stay");
            loop.SetPartyStanding(false, 0, 0, Now + 40 * Hour);
            var back = TestWorld.Loop(state: Reloaded(loop.State));
            Assert.That(back.PartyGrowth.InParty, Is.False);
        }

        [Test]
        public void AStaleSaveIsNotUsed()
        {
            var loop = Member();
            loop.State.PartyGrowth.SeenUnixMs = Now + 30 * Hour;
            var back = TestWorld.Loop(state: Reloaded(loop.State));
            Assert.That(back.PartyGrowth.InParty, Is.False);
            Assert.That(back.State.PartyGrowth.TreeLevels, Is.Empty);
        }

        [Test]
        public void ThreeDaysAwayDropThePowerOnTheFirstClockTick()
        {
            var back = TestWorld.Loop(state: Reloaded(Member().State));
            Assert.That(back.PartyGrowth.InParty, Is.True, "the save was fresh when the game closed");
            back.SetServerTime(Now + 72 * Hour);
            Assert.That(back.PartyGrowth.InParty, Is.False);
        }
    }
}
