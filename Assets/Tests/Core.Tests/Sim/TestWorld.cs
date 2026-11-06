using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;

namespace Game.Core.Tests.Sim
{
    public static class TestWorld
    {
        private static readonly Lazy<GameData> RealData = new Lazy<GameData>(() => GameData.Load(TestData.ReadGameDataFile));
        private static readonly Lazy<GameData> DataWithoutClassPassives = new Lazy<GameData>(() => GameData.Load(NeutralFile));

        private static readonly Lazy<GameData> DataWithoutRotation = new Lazy<GameData>(() => GameData.Load(name =>
            name == StageTable.FileName ? NeutralFile(name).Replace(",\n  \"rotation\": { \"minutes\": 5 }", string.Empty) : NeutralFile(name)));

        public static GameData Data => DataWithoutClassPassives.Value;
        public static GameData BiomesByStage => DataWithoutRotation.Value;
        public static GameData Real => RealData.Value;
        public static GameData WithPierce => DataWithPierce.Value;

        private static readonly Lazy<GameData> DataWithPierce = new Lazy<GameData>(() => GameData.Load(name =>
            name == CombatRules.FileName ? TestData.ReadGameDataFile(name) : NeutralFile(name)));

        private static string NeutralFile(string name)
        {
            var json = TestData.ReadGameDataFile(name);
            if (name == UnlockRules.FileName)
                return "{ \"version\": 1, \"features\": {}, \"artifactGroups\": [] }";
            if (name == WeaponCatalog.FileName)
                return Regex.Replace(json, "\"innate\": \\[.*\\]", "\"innate\": []");
            if (name == CombatRules.FileName)
                return System.Text.RegularExpressions.Regex.Replace(json, "\"pierce\": \\{ \"baseShare\": [0-9.]+ \\}", "\"pierce\": { \"baseShare\": 0 }");
            return json;
        }

        public static GameState WithAllSkillsOpen(GameState state)
        {
            state.HeroLevel = Data.SkillBook.SkillUnlockLevel(SkillTier.Mythic);
            return state;
        }

        public static void RaiseClassTree(GameLoop loop, int points)
        {
            var tree = loop.ClassTreeDef!;
            for (var i = 0; i < points; i++)
            {
                var node = tree.Nodes.First(n => loop.State.ClassTree.CheckRaise(tree, n.Id) == RankResult.Done);
                if (loop.RaiseNode(node.Id) != RankResult.Done)
                    throw new InvalidOperationException(node.Id);
            }
        }

        public static GameLoop Loop(IInputSource? input = null, GameState? state = null, StatSheet? hero = null) =>
            new GameLoop(Data, input ?? new FakeInputSource(), state ?? new GameState(), hero ?? new StatSheet());
    }
}
