using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StripSceneTests
    {
        private static int SwitchTicks => (int)System.Math.Round(TestWorld.Data.Panel.Scene.SwitchSeconds * GameLoop.TicksPerSecond);

        private static GameLoop Hero(long stage = 300) => Hero(new FakeInputSource(), stage);

        private static GameLoop Hero(FakeInputSource input, long stage = 300)
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            var state = new GameState(6) { Class = HeroClass.Runedancer, Stage = stage, HighestStage = stage, RecordStage = stage };
            var loop = TestWorld.Loop(input, state, hero);
            loop.Tick();
            loop.Events.Clear();
            return loop;
        }

        [Test]
        public void TheGameStartsInTheSavedPlaceWithoutASwitch()
        {
            var state = new GameState(1);
            state.Peaceful.Mode = PeacefulMode.Camp;
            state.Peaceful.Place = PeacefulPlace.Town;
            var loop = TestWorld.Loop(new FakeInputSource(), state);
            loop.Tick();

            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Town));
            Assert.That(loop.Scene.Switching, Is.False);
            Assert.That(loop.Scene.SwitchProgress, Is.EqualTo(1));
            Assert.That(loop.Events.CountOf(GameEventKind.StripPlaceChanged), Is.Zero);
        }

        [Test]
        public void EnteringEveryPeacefulPlaceSwapsTheStripAndLeavingBringsTheBattleBack()
        {
            var places = new[]
            {
                (PeacefulPlace.Town, StripPlace.Town),
                (PeacefulPlace.Hideout, StripPlace.Hideout),
                (PeacefulPlace.ClanCamp, StripPlace.ClanCamp),
                (PeacefulPlace.Refuge, StripPlace.Refuge)
            };
            var loop = Hero();
            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.Fight, Is.EqualTo(StripFight.Enemy));

            foreach (var (place, shown) in places)
            {
                loop.Events.Clear();
                Assert.That(loop.Peaceful.Enter(place), Is.EqualTo(PeacefulResult.Done));
                Assert.That(loop.Peaceful.Enter(place), Is.EqualTo(PeacefulResult.Same));
                loop.Tick();
                Assert.That(loop.Scene.Place, Is.EqualTo(shown));
                Assert.That(loop.Scene.Peaceful, Is.True);
                Assert.That(loop.Scene.Fight, Is.EqualTo(StripFight.None));
                Assert.That(loop.Scene.Activity, Is.EqualTo(loop.Peaceful.Activity));
                Assert.That(loop.Events.CountOf(GameEventKind.StripPlaceChanged), Is.EqualTo(1));
            }

            loop.Events.Clear();
            Assert.That(loop.Peaceful.Leave(), Is.EqualTo(PeacefulResult.Done));
            loop.Tick();
            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.PreviousPlace, Is.EqualTo(StripPlace.Refuge));
            Assert.That(loop.Scene.Fight, Is.EqualTo(StripFight.Enemy));
            Assert.That(loop.Scene.Activity, Is.Empty);
            Assert.That(loop.Events.CountOf(GameEventKind.StripPlaceChanged), Is.EqualTo(1));
        }

        [Test]
        public void EveryPeacefulPlacePausesTheFight()
        {
            foreach (var place in new[] { PeacefulPlace.Town, PeacefulPlace.ClanCamp, PeacefulPlace.Refuge, PeacefulPlace.Hideout })
            {
                var input = new FakeInputSource();
                var loop = Hero(input);
                loop.Peaceful.Enter(place);
                var kills = loop.State.Kills;
                var damage = loop.State.TotalDamage;
                for (var i = 0; i < 20; i++)
                {
                    input.PressKeys(3);
                    loop.Tick();
                }
                Assert.That(loop.State.Kills, Is.EqualTo(kills), place.ToString());
                Assert.That(loop.State.TotalDamage, Is.EqualTo(damage), place.ToString());
                Assert.That(loop.Scene.Fight, Is.EqualTo(StripFight.None), place.ToString());
            }
        }

        [Test]
        public void TheSwitchRunsForTheTimeFromPanelJson()
        {
            Assume.That(SwitchTicks, Is.GreaterThan(1));
            var loop = Hero();
            loop.Peaceful.Enter(PeacefulPlace.Town);
            loop.Tick();
            Assert.That(loop.Scene.PreviousPlace, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.SwitchTicksLeft, Is.EqualTo(SwitchTicks));
            Assert.That(loop.Scene.SwitchProgress, Is.Zero);

            var before = loop.Scene.SwitchProgress;
            for (var i = 1; i < SwitchTicks; i++)
            {
                loop.Tick();
                Assert.That(loop.Scene.Switching, Is.True);
                Assert.That(loop.Scene.SwitchProgress, Is.GreaterThan(before));
                before = loop.Scene.SwitchProgress;
            }
            loop.Tick();
            Assert.That(loop.Scene.Switching, Is.False);
            Assert.That(loop.Scene.SwitchProgress, Is.EqualTo(1));
        }

        [Test]
        public void ADungeonTakesTheStripAndGivesItBack()
        {
            var loop = Hero(5000);
            var key = new DungeonKey(4, System.Array.Empty<string>(), 4321, TestWorld.Data.Dungeons.KeyAttempts);
            loop.State.Dungeons.Keys.Add(key);

            Assert.That(loop.Dungeons.Enter(key), Is.EqualTo(DungeonResult.Entered));
            loop.Tick();
            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Dungeon));
            Assert.That(StripSceneText.Title(loop), Is.EqualTo(Texts.Format("strip.place.dungeonTier", 4)));
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Town), Is.EqualTo(PeacefulResult.Busy), "no town visits from inside a dungeon");

            Assert.That(loop.Dungeons.Abandon(), Is.True);
            loop.Tick();
            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.PreviousPlace, Is.EqualTo(StripPlace.Dungeon));
        }

        [Test]
        public void TheRiftAndTheEventBossHaveTheirOwnScenesAndTheDeeperOneWins()
        {
            var state = new GameState(1);
            Assert.That(StripScene.PlaceOf(state), Is.EqualTo(StripPlace.Home));
            state.Rift.Inside = true;
            Assert.That(StripScene.PlaceOf(state), Is.EqualTo(StripPlace.Rift));
            state.Dungeons.Active = new DungeonKey(1, System.Array.Empty<string>(), 0, 3);
            Assert.That(StripScene.PlaceOf(state), Is.EqualTo(StripPlace.Dungeon), "a rift dungeon is entered from the rift");
            state.Events.FightInside = true;
            Assert.That(StripScene.PlaceOf(state), Is.EqualTo(StripPlace.EventFight));
            state.Peaceful.Mode = PeacefulMode.Camp;
            Assert.That(StripScene.PlaceOf(state), Is.EqualTo(StripPlace.Refuge));
        }

        [Test]
        public void TheBossIsAnnouncedWhenItComesAndWhenItGoes()
        {
            var input = new FakeInputSource();
            var loop = Hero(input, 4);
            var seen = 0;
            for (var i = 0; i < 3000 && seen < 2; i++)
            {
                loop.Events.Clear();
                var before = loop.Scene.Fight;
                input.PressKeys(3);
                loop.Tick();
                if (loop.Scene.Fight == before)
                    continue;
                seen++;
                Assert.That(loop.Events.CountOf(GameEventKind.StripFightChanged), Is.EqualTo(1));
                Assert.That(loop.Scene.Fight, Is.EqualTo(StripScene.FightOf(loop.State, StripPlace.Home)));
            }
            Assert.That(seen, Is.EqualTo(2), "a boss came and was beaten");
            Assert.That(loop.Scene.Place, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.Switching, Is.False, "a boss does not swap the place");
        }

        [Test]
        public void TheTitleNamesThePlaceAndTheActivity()
        {
            var loop = Hero();
            Assert.That(StripSceneText.Title(loop), Is.EqualTo(Texts.Get("strip.place.home")));
            loop.Peaceful.Enter(PeacefulPlace.Town);
            loop.Peaceful.SetActivity("fishing");
            loop.Tick();
            Assert.That(StripSceneText.Title(loop), Does.StartWith(Texts.Get("strip.place.town")));
            Assert.That(StripSceneText.Title(loop), Does.Contain(PeacefulView.Activities(loop.Peaceful).Find(line => line.On)!.Name));
        }

        [Test]
        public void TheSaveKeepsEveryPlace()
        {
            foreach (var place in new[] { PeacefulPlace.Refuge, PeacefulPlace.Hideout, PeacefulPlace.Town, PeacefulPlace.ClanCamp })
            {
                var state = new GameState(1);
                state.Peaceful.Mode = PeacefulMode.Camp;
                state.Peaceful.Place = place;
                var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(state), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
                Assert.That(copy.Peaceful.Place, Is.EqualTo(place));
                Assert.That(StripScene.PlaceOf(copy), Is.EqualTo(StripScene.PlaceOf(state)));
            }
        }

        [Test]
        public void WatchingTheSceneDoesNotAllocate()
        {
            var loop = Hero();
            var scene = new StripScene(TestWorld.Data.Panel.Scene, loop.State, loop.Peaceful, new EventQueue(4096));
            scene.Tick();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                loop.State.Peaceful.Mode = i % 2 == 0 ? PeacefulMode.Camp : PeacefulMode.Battle;
                scene.Tick();
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            loop.State.Peaceful.Mode = PeacefulMode.Battle;
        }
    }
}
