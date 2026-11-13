using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Sim.Rift
{
    public static class HeroKit
    {
        public const ulong SeedSalt = 0x41F7_C0DE_5EEDUL;

        public static GameState Fresh(GameState home, long windowId) =>
            new GameState((ulong)windowId ^ SeedSalt) { Class = home.Class, ClassChosen = true };

        public static void Swap(GameState a, GameState b)
        {
            SwapLevel(a, b);
            SwapWallet(a, b);
            SwapBuild(a, b);
            SwapGems(a, b);
            SwapArms(a, b);
            SwapMount(a.Mount, b.Mount);
            SwapSlates(a.Slates, b.Slates);
            SwapMap(a.Resonance, b.Resonance);
            SwapMap(a.Spirits, b.Spirits);
            SwapList(a.Atlas, b.Atlas);
        }

        private static void SwapLevel(GameState a, GameState b)
        {
            (a.HeroLevel, b.HeroLevel) = (b.HeroLevel, a.HeroLevel);
            (a.StrikeLevel, b.StrikeLevel) = (b.StrikeLevel, a.StrikeLevel);
            (a.HeroXp, b.HeroXp) = (b.HeroXp, a.HeroXp);
            (a.Resource, b.Resource) = (b.Resource, a.Resource);
        }

        private static void SwapWallet(GameState a, GameState b)
        {
            (a.Gold, b.Gold) = (b.Gold, a.Gold);
            (a.RunGoldEarned, b.RunGoldEarned) = (b.RunGoldEarned, a.RunGoldEarned);
            (a.Drops.Dust, b.Drops.Dust) = (b.Drops.Dust, a.Drops.Dust);
        }

        private static void SwapBuild(GameState a, GameState b)
        {
            (a.TreePointsEarned, b.TreePointsEarned) = (b.TreePointsEarned, a.TreePointsEarned);
            (a.ClassTree, b.ClassTree) = (b.ClassTree, a.ClassTree);
            (a.CommonTrees, b.CommonTrees) = (b.CommonTrees, a.CommonTrees);
            (a.Ascendancy, b.Ascendancy) = (b.Ascendancy, a.Ascendancy);
            (a.ActivePreset, b.ActivePreset) = (b.ActivePreset, a.ActivePreset);
            (a.OtherPreset, b.OtherPreset) = (b.OtherPreset, a.OtherPreset);
        }

        private static void SwapGems(GameState a, GameState b)
        {
            SwapList(a.Skills, b.Skills);
            SwapList(a.OwnedGems, b.OwnedGems);
            SwapMap(a.GemProgress, b.GemProgress);
            (a.LegacyGems, b.LegacyGems) = (b.LegacyGems, a.LegacyGems);
        }

        private static void SwapArms(GameState a, GameState b)
        {
            (a.Weapons.Held, b.Weapons.Held) = (b.Weapons.Held, a.Weapons.Held);
            (a.Weapons.StarterGiven, b.Weapons.StarterGiven) = (b.Weapons.StarterGiven, a.Weapons.StarterGiven);
            SwapList(a.Weapons.Bag, b.Weapons.Bag);
            SwapArray(a.Gear.Worn, b.Gear.Worn);
            SwapList(a.Drops.Gear, b.Drops.Gear);
        }

        private static void SwapMount(MountState a, MountState b)
        {
            SwapArray(a.Levels, b.Levels);
            SwapArray(a.Facets, b.Facets);
            (a.Scan, b.Scan) = (b.Scan, a.Scan);
            (a.Cutters, b.Cutters) = (b.Cutters, a.Cutters);
            (a.FreeRebuilds, b.FreeRebuilds) = (b.FreeRebuilds, a.FreeRebuilds);
        }

        private static void SwapSlates(SlateState a, SlateState b)
        {
            SwapList(a.Bag, b.Bag);
            SwapList(a.Board, b.Board);
            (a.Found, b.Found) = (b.Found, a.Found);
        }

        private static void SwapList<T>(List<T> a, List<T> b)
        {
            var held = new List<T>(a);
            a.Clear();
            a.AddRange(b);
            b.Clear();
            b.AddRange(held);
        }

        private static void SwapMap<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b) where TKey : notnull
        {
            var held = new Dictionary<TKey, TValue>(a, a.Comparer);
            a.Clear();
            foreach (var pair in b)
                a[pair.Key] = pair.Value;
            b.Clear();
            foreach (var pair in held)
                b[pair.Key] = pair.Value;
        }

        private static void SwapArray<T>(T[] a, T[] b)
        {
            for (var i = 0; i < a.Length && i < b.Length; i++)
                (a[i], b[i]) = (b[i], a[i]);
        }
    }
}
