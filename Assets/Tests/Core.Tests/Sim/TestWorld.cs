using System;
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
            if (name == PassiveTree.FileName)
                return WithoutStartModifiers(json);
            if (name == WeaponCatalog.FileName)
                return Regex.Replace(json, "\"innate\": \\[.*\\]", "\"innate\": []");
            return json;
        }

        private static string WithoutStartModifiers(string tree) =>
            Regex.Replace(tree, "(\"type\": \"start\"[^\n]*?), \"modifiers\": \\[[^\n]*?\\](?=, \"class\")", "$1");

        public const string NoClass = "none";

        public static GameState Classless(GameState state)
        {
            state.Tree = new TreeAllocation(NoClass);
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
                loop.Weapons.Give(weapon);
                loop.Weapons.Equip(WeaponHand.Left, weapon);
                return;
            }
        }

        public static GameLoop Loop(IInputSource? input = null, GameState? state = null, StatSheet? hero = null) =>
            new GameLoop(Data, input ?? new FakeInputSource(), state ?? Classless(new GameState()), hero ?? new StatSheet());
    }
}
