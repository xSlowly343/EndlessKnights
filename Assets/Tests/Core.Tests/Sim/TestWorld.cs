using System;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;

namespace Game.Core.Tests.Sim
{
    public static class TestWorld
    {
        private static readonly Lazy<GameData> RealData = new Lazy<GameData>(() => GameData.Load(TestData.ReadGameDataFile));

        public static GameData Data => RealData.Value;

        public static GameLoop Loop(IInputSource? input = null, GameState? state = null, StatSheet? hero = null) =>
            new GameLoop(Data, input ?? new FakeInputSource(), state ?? new GameState(), hero ?? new StatSheet());
    }
}
