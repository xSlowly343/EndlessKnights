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

        public static GameData Data => DataWithoutClassPassives.Value;
        public static GameData Real => RealData.Value;

        private static string NeutralFile(string name)
        {
            var json = TestData.ReadGameDataFile(name);
            if (name == UnlockRules.FileName)
                return "{ \"version\": 1, \"features\": {}, \"artifactGroups\": [] }";
            if (name == WeaponCatalog.FileName)
                return Regex.Replace(json, "\"innate\": \\[.*\\]", "\"innate\": []");
            return json;
        }

        public static GameState WithAllWeaponPlacesOpen(GameState state)
        {
            var levels = Data.Weapons.SkillRules.UnlockLevels;
            state.HeroLevel = levels[levels.Count - 1];
            return state;
        }

        public static void ArmFor(GameLoop loop, GemDef gem)
        {
            var type = gem.Skill?.Weapon;
            if (type == null || loop.State.Weapons.Holds(type))
                return;
            foreach (var weapon in loop.Data.Weapons.Bases)
            {
                if (weapon.Type.Id != type)
                    continue;
                loop.Weapons.Equip(WeaponHand.Left, loop.Weapons.Give(weapon));
                return;
            }
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
