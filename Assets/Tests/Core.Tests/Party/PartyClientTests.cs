using Game.Core.Input;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyClientTests
    {
        private const ulong HostId = 1;

        private sealed class Player
        {
            public Player(LoopbackNetwork network, ulong id, long stage)
            {
                Id = id;
                Input = new FakeInputSource();
                var hero = new StatSheet();
                var hp = TestWorld.Data.Stages.EnemyHp(stage).ToDouble();
                hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, hp / (10 * TestWorld.Data.Combat.KeyDamage) - 1));
                Loop = TestWorld.Loop(Input, new GameState { Stage = stage, HighestStage = stage, RecordStage = stage }, hero);
                Client = new PartyClient(Loop, network.Connect(id), HostId, TestWorld.Data.Party);
            }

            public ulong Id { get; }
            public FakeInputSource Input { get; }
            public GameLoop Loop { get; }
            public PartyClient Client { get; }
            public int SoloKills { get; private set; }
            public int AllKills { get; private set; }

            public void Tick(bool typing)
            {
                if (typing)
                    Input.PressKeys(1);
                var solo = Client.Status != PartyLink.Linked && Client.Status != PartyLink.NoSignal;
                Loop.Tick();
                var kills = Loop.Events.CountOf(GameEventKind.EnemyKilled);
                AllKills += kills;
                if (solo)
                    SoloKills += kills;
                Loop.Events.Clear();
            }
        }

        private sealed class Rig
        {
            public Rig(params long[] stages) : this(0, stages)
            {
            }

            public Rig(double loss, params long[] stages)
            {
                Network = new LoopbackNetwork(5, 20, 120, loss, 4096);
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 11), Network.Connect(HostId), TestWorld.Data.Party);
                Players = new Player[stages.Length];
                for (var i = 0; i < stages.Length; i++)
                {
                    Players[i] = new Player(Network, 10UL + (ulong)i, stages[i]);
                    Players[i].Client.Join();
                }
            }

            public LoopbackNetwork Network { get; }
            public PartyHost Host { get; private set; }

            public void ReplaceHost(ulong seed) =>
                Host = new PartyHost(new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, seed), Network.Connect(HostId), TestWorld.Data.Party);
            public Player[] Players { get; }

            public void Run(double seconds, bool typing = true) => RunEach(seconds, typing, typing);

            public void RunEach(double seconds, bool firstTyping, bool othersTyping)
            {
                var ticks = (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
                for (var tick = 0; tick < ticks; tick++)
                {
                    Network.Advance(100);
                    for (var i = 0; i < Players.Length; i++)
                        Players[i].Tick(i == 0 ? firstTyping : othersTyping);
                    Host.Tick();
                }
            }
        }

        [Test]
        public void TwoPlayersOnDifferentStagesFightOneEnemy()
        {
            var rig = new Rig(5, 40);

            rig.Run(60);

            Assert.That(rig.Host.Encounter.MemberCount, Is.EqualTo(2));
            Assert.That(rig.Host.Kills, Is.GreaterThan(15));
            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
                Assert.That(player.Client.PartyRewards, Is.InRange(rig.Host.Kills - 3, rig.Host.Kills));
                Assert.That(player.Loop.State.Gold.Sign, Is.GreaterThan(0));
            }
            Assert.That(rig.Players[0].Loop.State.Stage, Is.GreaterThan(5));
            Assert.That(rig.Players[1].Loop.State.Stage - 40, Is.EqualTo(rig.Players[0].Loop.State.Stage - 5));
        }

        [Test]
        public void FivePercentLossStillRewardsEveryKillOnce()
        {
            var rig = new Rig(0.05, 5, 40, 120);

            rig.Run(60);

            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
                Assert.That(player.Client.PartyRewards, Is.InRange(rig.Host.Kills - 3, rig.Host.Kills));
                Assert.That(player.AllKills, Is.EqualTo(player.Client.PartyRewards + player.SoloKills));
            }
            var cleared = rig.Players[0].Loop.State.Stage - 5;
            Assert.That(cleared, Is.GreaterThan(0));
            Assert.That(rig.Players[1].Loop.State.Stage - 40, Is.EqualTo(cleared));
            Assert.That(rig.Players[2].Loop.State.Stage - 120, Is.EqualTo(cleared));
        }

        [Test]
        public void BossKillKeepsEveryonesShare()
        {
            var rig = new Rig(5, 40);

            rig.Run(60);

            foreach (var player in rig.Players)
            {
                var kill = player.Client.LastBossKill;
                Assert.That(player.Client.BossKillsSeen, Is.GreaterThan(0));
                Assert.That(kill.EnemyKind, Is.EqualTo(PartyEnemyKind.Boss));
                Assert.That(kill.Contribution(0) + kill.Contribution(1) + kill.Contribution(2) + kill.Contribution(3), Is.EqualTo(1).Within(1e-9));
                Assert.That(kill.Contribution(player.Client.OwnSlot), Is.GreaterThan(0.2));
            }
        }

        [Test]
        public void LinkedTicksDoNotAllocate()
        {
            var probeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeBefore < probe.Length)
                Assert.Ignore("This runtime does not report allocated bytes per thread.");
            var rig = new Rig(0.05, 5, 40);
            rig.Run(60);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            rig.Run(60);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void OwnHitsLowerThePredictedBarAtOnce()
        {
            var rig = new Rig(5, 40);
            rig.Run(3, false);
            var player = rig.Players[0];
            Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
            var before = player.Client.PredictedBarLeft;

            player.Input.PressKeys(1);
            player.Loop.Tick();
            player.Loop.Tick();

            Assert.That(player.Client.PredictedBarLeft, Is.LessThan(before));
            Assert.That(player.Loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(TestWorld.Data.Stages.EnemyHp(5).ToDouble() * player.Loop.State.Enemy!.HpMultiplier * player.Client.BarFull).Within(1e-6));
        }

        [Test]
        public void ThirtySecondDisconnectGoesSoloAndReturnsWithoutDoubleRewards()
        {
            var rig = new Rig(5, 40);
            rig.Run(20);
            var b = rig.Players[1];
            var killsBefore = rig.Host.Kills;

            rig.Network.SetLinkDown(b.Id, true);
            rig.Run(10);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.NoSignal));
            var rewardsAtLoss = b.Client.PartyRewards;
            rig.Run(25);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Solo));
            Assert.That(b.Client.PartyRewards, Is.EqualTo(rewardsAtLoss));
            Assert.That(b.SoloKills, Is.GreaterThan(0));
            var killsWhileAway = rig.Host.Kills - killsBefore;

            rig.Network.SetLinkDown(b.Id, false);
            var killsAtReturn = rig.Host.Kills;
            rig.Run(20);

            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Linked));
            Assert.That(rig.Players[0].Client.Status, Is.EqualTo(PartyLink.Linked));
            Assert.That(killsWhileAway, Is.GreaterThan(0));
            Assert.That(b.Client.PartyRewards, Is.LessThanOrEqualTo(killsBefore + (rig.Host.Kills - killsAtReturn)));
            Assert.That(b.AllKills, Is.EqualTo(b.Client.PartyRewards + b.SoloKills));
        }

        [Test]
        public void LongAbsenceStaysAwayUntilJoinedAgain()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var b = rig.Players[1];

            rig.Network.SetLinkDown(b.Id, true);
            rig.Run(TestWorld.Data.Party.DisconnectGraceSeconds + TestWorld.Data.Party.RejoinWindowSeconds + 5);
            rig.Network.SetLinkDown(b.Id, false);
            rig.Run(10);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Away));

            b.Client.Join();
            rig.Run(5);
            Assert.That(b.Client.Status, Is.EqualTo(PartyLink.Linked));
        }

        [Test]
        public void LeavingGoesSoloAndIgnoresTheParty()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var a = rig.Players[0];

            a.Client.Leave();
            rig.Run(10);

            Assert.That(a.Client.Status, Is.EqualTo(PartyLink.Solo));
            Assert.That(a.Loop.Stages.Linked, Is.False);
            Assert.That(a.SoloKills, Is.GreaterThan(0));
        }

        [Test]
        public void AnIdlePartnerIsPaidFromTheRestBankAndThenNotAtAll()
        {
            var rig = new Rig(5, 5);
            rig.Run(20);
            var idle = rig.Players[1];
            Assert.That(idle.Client.InParty, Is.True);
            Assert.That(idle.Client.UnpaidKills, Is.EqualTo(0));

            rig.RunEach(100, true, false);
            Assert.That(idle.Client.Roster.RestBankSeconds(idle.Client.OwnSlot), Is.EqualTo(0u), "the short bank from 20 s of play and 30 s of grace is spent");
            var killsBefore = idle.AllKills;

            rig.RunEach(30, true, false);

            Assert.That(idle.Client.UnpaidKills, Is.GreaterThan(0));
            Assert.That(idle.Client.LastKillPaid, Is.False);
            Assert.That(idle.AllKills, Is.EqualTo(killsBefore), "an unpaid kill gives no kill, gold or drops");
            Assert.That(rig.Players[0].Client.LastKillPaid, Is.True);
        }

        [Test]
        public void EveryMemberSeesTheContributionOfEachSlot()
        {
            var rig = new Rig(5, 5);

            rig.Run(30);

            var client = rig.Players[0].Client;
            Assert.That(client.ContributionKills, Is.GreaterThan(5));
            Assert.That(client.Contribution(0), Is.GreaterThan(0.2));
            Assert.That(client.Contribution(1), Is.GreaterThan(0.2));
            Assert.That(client.Contribution(0) + client.Contribution(1), Is.EqualTo(1).Within(1e-9));
            Assert.That(rig.Players[1].Client.Contribution(0), Is.EqualTo(client.Contribution(0)).Within(1e-9));
        }

        [Test]
        public void AMacroRhythmDoesNotCountAsTyping()
        {
            var rig = new Rig(5, 5);
            rig.Run(10);
            var macro = rig.Players[1];
            macro.Input.RhythmCv = 0.01f;

            rig.Run(100);

            Assert.That(macro.Loop.Focus.MacroDetected, Is.True);
            Assert.That(rig.Host.Encounter.Typing(macro.Client.OwnSlot), Is.False);
            Assert.That(rig.Host.Encounter.Typing(rig.Players[0].Client.OwnSlot), Is.True);
        }

        [Test]
        public void AfterTheServerRestartsTheFightGoesOnWithTheNewHost()
        {
            var rig = new Rig(5, 5);
            rig.Run(30);
            var paidBefore = rig.Players[0].Client.PartyRewards;
            Assert.That(rig.Host.Encounter.EnemyId, Is.GreaterThan(5u));

            rig.ReplaceHost(77);
            foreach (var player in rig.Players)
                player.Client.Rejoin();
            rig.Run(30);

            Assert.That(rig.Host.Kills, Is.GreaterThan(3), "the new host starts from enemy 1 and the fight goes on");
            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.Status, Is.EqualTo(PartyLink.Linked));
                Assert.That(player.Client.EnemyId, Is.EqualTo(rig.Host.Encounter.EnemyId));
            }
            Assert.That(rig.Players[0].Client.PartyRewards, Is.GreaterThan(paidBefore + 3));
            Assert.That(rig.Players[0].Client.LastBossKill.EnemyId, Is.LessThanOrEqualTo(rig.Host.Encounter.EnemyId), "bosses of the old match do not hide the new ones");
        }

        [Test]
        public void APausingPlayerPassesFocusToTheOneWhoTypes()
        {
            var rig = new Rig(5, 5, 5);
            rig.Run(15);
            var resting = rig.Players[0];
            var dropped = resting.Loop.State.Focus - TestWorld.Data.Combat.Focus.Min;
            var relay = TestWorld.Data.Party.Relay;

            rig.RunEach(resting.Loop.Focus.ResetAfterIdleSeconds + 1, false, true);

            Assert.That(dropped, Is.GreaterThan(0));
            Assert.That(rig.Host.Relays, Is.EqualTo(1));
            var got = System.Array.FindAll(rig.Players, player => player.Loop.State.RelayFocus > 0);
            Assert.That(got, Has.Length.EqualTo(1), "one typing ally gets the focus");
            Assert.That(got[0], Is.Not.SameAs(resting));
            Assert.That(got[0].Loop.State.RelayFocus, Is.EqualTo(relay.BonusFor(dropped)).Within(1e-6));
            Assert.That(got[0].Loop.State.FocusMultiplier, Is.EqualTo(got[0].Loop.State.Focus + got[0].Loop.State.RelayFocus).Within(1e-9));
            foreach (var player in rig.Players)
            {
                Assert.That(player.Client.LastRelay.From, Is.EqualTo(resting.Client.OwnSlot), "everyone sees who passed the focus");
                Assert.That(player.Client.LastRelay.To, Is.EqualTo(got[0].Client.OwnSlot));
                Assert.That(player.Client.RelaySerial, Is.EqualTo(1));
            }

            Game.Core.Text.Texts.UseLanguage("ru");
            Assert.That(Game.Core.Ui.FocusRelayText.Notice(got[0].Client.OwnSlot, got[0].Client.LastRelay, "Аня", "Боря", relay.Seconds), Is.EqualTo("Аня передаёт вам Фокус: +1.0 на 30 с"));
            Assert.That(Game.Core.Ui.FocusRelayText.Notice(resting.Client.OwnSlot, resting.Client.LastRelay, "Аня", "Боря", relay.Seconds), Is.EqualTo("Ваш Фокус ушёл к Боря: +1.0"));
            Assert.That(Game.Core.Ui.FocusRelayText.Notice(9, resting.Client.LastRelay, "Аня", "Боря", relay.Seconds), Is.EqualTo("Аня передаёт Фокус Боря"));
            Assert.That(Game.Core.Ui.HeroStatsView.Lines(got[0].Loop, Game.Core.Math.NumberFormatSettings.Default), Has.Some.Matches<Game.Core.Ui.HeroStatLine>(line => line.Value.StartsWith("+1.0 ещё")));

            rig.RunEach(relay.Seconds + 1, false, true);

            Assert.That(got[0].Loop.State.RelayFocus, Is.Zero, "the bonus runs out");
            Assert.That(rig.Host.Relays, Is.EqualTo(1), "a long pause passes the focus once");
        }

        [Test]
        public void ACalledFriendHitsTheBossForHalfAMinuteAndKeepsTheirOwnStage()
        {
            var rig = new Rig(5, 5);
            var friend = new Player(rig.Network, 99, 40);
            friend.Loop.State.KillsOnStage = 3;
            var ownStage = friend.Loop.State.Stage;
            for (var tick = 0; tick < 3000 && !rig.Host.Encounter.FightingBoss; tick++)
                rig.Run(0.1);
            Assert.That(rig.Host.Encounter.FightingBoss, Is.True);
            var help = TestWorld.Data.Party.Help;

            Assert.That(rig.Host.InviteGuest(friend.Id, help.Seconds), Is.True);
            friend.Client.JoinAsGuest();
            var linkedTicks = 0;
            var guestHits = 0.0;
            var seenAsGuest = false;
            for (var tick = 0; tick < (help.Seconds + 5) * GameLoop.TicksPerSecond && !friend.Client.GuestFinished; tick++)
            {
                rig.Network.Advance(100);
                foreach (var player in rig.Players)
                    player.Tick(true);
                friend.Tick(true);
                rig.Host.Tick();
                var slot = rig.Host.Encounter.SlotOf(friend.Id);
                if (friend.Client.Status == PartyLink.Linked)
                    linkedTicks++;
                if (slot == PartyEncounter.NoSlot)
                    continue;
                Assert.That(rig.Host.Encounter.Member(slot).Guest, Is.True);
                Assert.That(rig.Host.Encounter.Fighters, Is.EqualTo(2), "a guest does not make the enemy bigger");
                guestHits = System.Math.Max(guestHits, rig.Host.Encounter.Member(slot).EnemyShare);
                seenAsGuest |= rig.Players[0].Client.Roster.Guest(slot);
            }

            Assert.That(linkedTicks, Is.GreaterThan((help.Seconds - 2) * GameLoop.TicksPerSecond));
            Assert.That(guestHits, Is.GreaterThan(0), "the friend's hits land on the party's enemy");
            Assert.That(seenAsGuest, Is.True, "the party sees a guest in the slot");
            Assert.That(friend.Client.GuestFinished, Is.True);
            Assert.That(friend.Client.Status, Is.EqualTo(PartyLink.Solo));
            Assert.That(friend.Client.IsGuest, Is.False);
            Assert.That(friend.Client.PartyRewards, Is.Zero, "the friend is not paid for the party's kills");
            Assert.That(friend.Loop.State.Stage, Is.EqualTo(ownStage));
            Assert.That(friend.Loop.State.KillsOnStage, Is.EqualTo(3), "the friend's own stage progress comes back");
            Assert.That(rig.Host.Encounter.SlotOf(friend.Id), Is.EqualTo(PartyEncounter.NoSlot));
        }

        [Test]
        public void CallsForHelpReadInRussian()
        {
            Game.Core.Text.Texts.UseLanguage("ru");

            Assert.That(Game.Core.Ui.HelpCallText.Invite("Аня", 61_000, 1_500), Is.EqualTo("Аня зовёт на помощь против босса · ещё 60 с"));
            Assert.That(Game.Core.Ui.HelpCallText.Helping(12.3), Is.EqualTo("Вы помогаете: ещё 13 с"));
            Assert.That(Game.Core.Ui.HelpCallText.Guest("Боря"), Is.EqualTo("Боря пришёл на помощь"));
            Assert.That(Game.Core.Ui.HelpCallText.Accept(PartyHelpRules.AcceptFriends), Is.EqualTo("Принимать зов на помощь: от друзей"));
            Assert.That(Game.Core.Ui.HelpCallText.Refused("no boss to help with"), Is.EqualTo("звать можно только на босса"));
            Assert.That(Game.Core.Ui.HelpCallText.Refused("party is full"), Is.EqualTo("в отряде нет мест"));
            Assert.That(Game.Core.Ui.HelpCallText.Refused("socket closed"), Is.EqualTo("нет связи с сервером"));
            foreach (var accept in PartyHelpRules.AcceptChoices)
                Assert.That(Game.Core.Ui.HelpCallText.Accept(accept), Does.StartWith("Принимать"));
        }

        [Test]
        public void OnlyABossFightCanCallAFriend()
        {
            var rig = new Rig(5);
            rig.Run(1);

            Assert.That(rig.Host.Encounter.FightingBoss, Is.False);
            Assert.That(rig.Host.InviteGuest(99, 30), Is.False);
            Assert.That(rig.Host.InviteGuest(rig.Players[0].Id, 30), Is.False);
        }

        [Test]
        public void TheOracleGainsOmensFromAllyHitsAndGivesResourceToAllies()
        {
            var rig = new Rig(5);
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "good_forecast");
            var oracleClient = new PartyClient(oracle.Loop, rig.Network.Connect(50), HostId, TestWorld.Data.Party);
            oracleClient.Join();
            var viking = rig.Players[0];
            void Step(bool typing)
            {
                rig.Network.Advance(100);
                viking.Tick(typing);
                oracle.Loop.Tick();
                oracle.Loop.Events.Clear();
                rig.Host.Tick();
            }
            for (var tick = 0; tick < 20; tick++)
                Step(true);
            oracle.State.Resource = 0;

            for (var tick = 0; tick < 50; tick++)
                Step(true);

            Assert.That(oracleClient.Status, Is.EqualTo(PartyLink.Linked));
            Assert.That(oracle.State.PartyAllies, Is.EqualTo(1));
            Assert.That(oracle.State.Resource, Is.InRange(3.5, 5.5), "about one omen for every ten hits of the ally");

            oracle.State.Resource = 100;
            viking.Loop.State.Resource = 0;
            oracle.Loop.RequestCast(0);
            for (var tick = 0; tick < 10; tick++)
                Step(false);

            Assert.That(oracle.State.Skills[0].Active, Is.True, "Good Forecast is on");

            Assert.That(viking.Loop.State.Resource, Is.EqualTo(20).Within(1e-6), "Good Forecast gives the ally 20 of their resource");
            Assert.That(viking.Loop.State.PartyAllies, Is.EqualTo(1));
        }

        private static void Step(Rig rig, SkillTestHero hero, bool typing)
        {
            rig.Network.Advance(100);
            rig.Players[0].Tick(typing);
            hero.Loop.Tick();
            hero.Loop.Events.Clear();
            rig.Host.Tick();
        }

        private static PartyClient Joined(Rig rig, SkillTestHero hero)
        {
            var client = new PartyClient(hero.Loop, rig.Network.Connect(50), HostId, TestWorld.Data.Party);
            client.Join();
            for (var tick = 0; tick < 20; tick++)
                Step(rig, hero, true);
            Assert.That(client.Status, Is.EqualTo(PartyLink.Linked));
            return client;
        }

        [Test]
        public void AMistakeInTheDetailsWeakensTheNextEnemyAtTheHostWithGoldForEveryone()
        {
            var rig = new Rig(5);
            var oracle = SkillTestHero.Of(HeroClass.Oracle, "mistake_in_the_details");
            Joined(rig, oracle);
            oracle.State.Resource = 100;
            oracle.Loop.RequestCast(0);
            for (var tick = 0; tick < 10 && rig.Host.Encounter.WeakenedSpawns == 0; tick++)
                Step(rig, oracle, false);
            Assert.That(rig.Host.Encounter.WeakenedSpawns, Is.EqualTo(1));
            Assert.That(rig.Host.Encounter.WeakenGold, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(oracle.State.WeakenedSpawns, Is.Zero, "the host keeps the weakened enemies, not the client");

            var viking = rig.Players[0].Loop.State;
            var seen = false;
            for (var tick = 0; tick < 100 && !seen; tick++)
            {
                Step(rig, oracle, true);
                seen = viking.EnemyWeakened;
            }

            Assert.That(seen, Is.True, "the ally knows the shared enemy is weakened");
            Assert.That(viking.EnemyGoldBonus, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(oracle.State.EnemyWeakened, Is.True);
            Assert.That(oracle.State.EnemyGoldBonus, Is.EqualTo(0.2).Within(1e-6));
        }

        [Test]
        public void TheHostDropsTheQuestionWhenTheInquisitorHasNoAuraLeft()
        {
            var rig = new Rig(5);
            var inquisitor = SkillTestHero.Borrowing(HeroClass.Viking, HeroClass.Bannerbearer, "artel_pay", "unanswered_question");
            Joined(rig, inquisitor);
            Assert.That(inquisitor.State.Skills[0].AuraApplied, Is.True);
            inquisitor.State.Resource = 100;
            inquisitor.Loop.RequestCast(1);
            for (var tick = 0; tick < 5; tick++)
                Step(rig, inquisitor, true);
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1.15).Within(1e-9));

            inquisitor.State.IdleTicks = 10 * GameLoop.TicksPerSecond;
            for (var tick = 0; tick < 5; tick++)
                Step(rig, inquisitor, true);

            Assert.That(inquisitor.State.Skills[1].Active, Is.False);
            Assert.That(rig.Host.Encounter.DamageTakenMultiplier, Is.EqualTo(1), "the host lifts the question");
        }

        [Test]
        public void TheShieldAnswerSlowsThePartyBossTimerAtTheHost()
        {
            var rig = new Rig(5);
            var shield = SkillTestHero.Of(HeroClass.Bannerbearer, "answer_from_the_shield");
            Joined(rig, shield);
            shield.State.Resource = 100;
            shield.Loop.RequestCast(0);
            for (var tick = 0; tick < 5; tick++)
                Step(rig, shield, false);

            var gem = TestWorld.Data.Gems["answer_from_the_shield"];
            var found = false;
            for (var i = 0; i < PartyEncounter.MaxEffects; i++)
            {
                var effect = rig.Host.Encounter.Effect(i);
                if (effect.Active && effect.Id == gem.NetId)
                {
                    found = true;
                    Assert.That(effect.TimerSlow, Is.EqualTo(0.33).Within(1e-9));
                    Assert.That(effect.DamageTaken, Is.Zero);
                }
            }
            Assert.That(found, Is.True);
            Assert.That(rig.Host.Encounter.BossTimerRate, Is.EqualTo(0.67).Within(1e-9));
        }

        [Test]
        public void FocusIsNotPassedWhenNobodyTypes()
        {
            var rig = new Rig(5, 5);
            rig.Run(15);

            rig.Run(rig.Players[0].Loop.Focus.ResetAfterIdleSeconds + 1, false);

            Assert.That(rig.Host.Relays, Is.Zero);
            Assert.That(rig.Players[1].Loop.State.RelayFocus, Is.Zero);
            Assert.That(rig.Players[0].Loop.State.FocusDropped, Is.Zero, "a dropped focus is spent even when nobody takes it");
        }

        [Test]
        public void AReactionReachesTheOthersAndFadesAfterItsTime()
        {
            var rig = new Rig(5, 40, 70);
            rig.Run(10);
            var sender = rig.Players[1].Client;
            var slot = sender.OwnSlot;

            Assert.That(sender.SendReaction(0), Is.True);
            rig.Run(1);

            Assert.That(sender.ReactionOf(slot), Is.EqualTo(0), "the sender sees its own reaction at once");
            Assert.That(rig.Players[0].Client.ReactionOf(slot), Is.EqualTo(0));
            Assert.That(rig.Players[2].Client.ReactionOf(slot), Is.EqualTo(0));
            Assert.That(rig.Players[0].Client.ReactionOf(rig.Players[0].Client.OwnSlot), Is.EqualTo(PartyClient.NoReaction));
            var allies = new Game.Core.Ui.StripAlly[Game.Core.Ui.StripAllies.MaxShown];
            Assert.That(Game.Core.Ui.StripAllies.Fill(rig.Players[0].Client, allies), Is.EqualTo(2));
            Assert.That(System.Array.Find(allies, ally => ally.Slot == slot).Reaction, Is.EqualTo(Game.Core.Ui.PartyReactions.Label(TestWorld.Data.Party.Reactions, 0)));

            rig.Run(TestWorld.Data.Party.Reactions.ShowSeconds + 0.5);

            Assert.That(rig.Players[0].Client.ReactionOf(slot), Is.EqualTo(PartyClient.NoReaction));
        }

        [Test]
        public void ReactionsWaitForTheCooldownAndUnknownOnesAreRefused()
        {
            var rig = new Rig(5, 40);
            rig.Run(10);
            var client = rig.Players[0].Client;

            Assert.That(client.SendReaction(TestWorld.Data.Party.Reactions.Ids.Count), Is.False);
            Assert.That(client.SendReaction(1), Is.True);
            Assert.That(client.CanReact, Is.False);
            Assert.That(client.SendReaction(2), Is.False);

            rig.Run(TestWorld.Data.Party.Reactions.CooldownSeconds);

            Assert.That(client.CanReact, Is.True);
            Assert.That(client.SendReaction(2), Is.True);
        }

        [Test]
        public void SoloPlayersCannotReact()
        {
            var rig = new Rig(5);

            Assert.That(rig.Players[0].Client.CanReact, Is.False);
            Assert.That(rig.Players[0].Client.SendReaction(0), Is.False);
            Assert.That(Game.Core.Ui.StripAllies.Fill(rig.Players[0].Client, new Game.Core.Ui.StripAlly[3]), Is.Zero);
        }

        [Test]
        public void TheDamageOfAlliesArrivesInYourOwnNumbers()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var watcher = rig.Players[0].Client;
            var ally = rig.Players[1].Client.OwnSlot;
            watcher.TakeAllyDamage(ally);

            rig.Run(5);

            var damage = watcher.TakeAllyDamage(ally);
            Assert.That(damage.Sign, Is.GreaterThan(0));
            Assert.That(watcher.TakeAllyDamage(ally).Sign, Is.Zero, "taking resets the sum");
            Assert.That(watcher.TakeAllyDamage(watcher.OwnSlot).Sign, Is.Zero, "your own damage is shown by your own hits");
            Assert.That(rig.Host.Encounter.MemberCount, Is.EqualTo(2));
        }

        [Test]
        public void AnEnemyEffectShowsAsABadgeInItsOwnersColour()
        {
            var rig = new Rig(5, 40);
            rig.Run(5);
            var gem = TestWorld.Data.Gems.All[0];
            var owner = rig.Players[1].Client.OwnSlot;
            rig.Host.Encounter.ApplyEnemyEffect(gem.NetId, 0.15, 8, owner);

            rig.Run(1);

            var badges = new Game.Core.Ui.EnemyBadge[Game.Core.Party.Wire.ShownEffect.MaxShown];
            Assert.That(Game.Core.Ui.PartyStripMarks.FillBadges(rig.Players[0].Client, TestWorld.Data.Gems, badges), Is.EqualTo(1));
            Assert.That(badges[0].ColorIndex, Is.EqualTo(1));
            Assert.That(badges[0].GemColor, Is.EqualTo(gem.Color));
            Assert.That(badges[0].SecondsLeft, Is.InRange(6, 8));
            Assert.That(badges[0].Name, Is.EqualTo(gem.NameRu));
            Assert.That(Game.Core.Ui.PartyStripMarks.FillBadges(rig.Players[1].Client, TestWorld.Data.Gems, badges), Is.EqualTo(1));
            Assert.That(badges[0].ColorIndex, Is.Zero, "the owner sees its own effect in its own colour");
        }
    }
}
