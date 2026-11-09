using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public readonly struct DamageChange
    {
        public readonly bool Possible;
        public readonly DamageBreakdown Before;
        public readonly DamageBreakdown After;

        public DamageChange(bool possible, DamageBreakdown before, DamageBreakdown after)
        {
            Possible = possible;
            Before = before;
            After = after;
        }

        public static DamageChange None => new DamageChange(false, default, default);

        public double Hit => Ratio(Before.Hit, After.Hit);

        public double BossHit => Ratio(Before.BossHit, After.BossHit);

        public static double Ratio(double before, double after) => before > 0 ? after / before : 1;
    }

    public static class DamagePreview
    {
        public static DamageChange Node(GameLoop loop, string nodeId) =>
            After(loop, copy => RaiseWithExtraPoint(copy, nodeId));

        public static DamageChange Gear(GameLoop loop, GearItem item, GearSlot slot)
        {
            var index = IndexOf(loop.State.Drops.Gear, item);
            return index < 0 ? DamageChange.None : After(loop, copy => copy.Gear.Equip(slot, copy.State.Drops.Gear[index]));
        }

        public static DamageChange Gear(GameLoop loop, GearItem item)
        {
            var index = IndexOf(loop.State.Drops.Gear, item);
            return index < 0 ? DamageChange.None : After(loop, copy => copy.Gear.Equip(copy.State.Drops.Gear[index]));
        }

        public static DamageChange Weapon(GameLoop loop, WeaponItem weapon)
        {
            var index = IndexOf(loop.State.Weapons.Bag, weapon);
            return index < 0 ? DamageChange.None : After(loop, copy => copy.Weapons.Equip(copy.State.Weapons.Bag[index]));
        }

        public static DamageChange Facet(GameLoop loop, int slot, int tier, string option) =>
            After(loop, copy =>
            {
                copy.State.Mount.Cutters = int.MaxValue;
                return copy.Facets.Choose(slot, tier, option) == FacetResult.Done;
            });

        public static DamageChange MountLevel(GameLoop loop, int slot) =>
            After(loop, copy =>
            {
                if (slot < 0 || slot >= MountRules.Slots || copy.Mount.Level(slot) >= copy.Mount.Rules.MaxLevel)
                    return false;
                copy.State.Mount.Levels[slot]++;
                copy.Mount.Refresh();
                copy.Facets.Rebuild();
                return true;
            });

        public static DamageChange After(GameLoop loop, System.Func<GameLoop, bool> change)
        {
            var before = Copy(loop);
            var after = Copy(loop);
            if (!change(after))
                return DamageChange.None;
            after.RefreshClassAttribute();
            return new DamageChange(true, DamageBreakdown.Of(before), DamageBreakdown.Of(after));
        }

        public static GameLoop Copy(GameLoop loop)
        {
            var data = loop.Data;
            var json = GameStateJson.Write(loop.State);
            var state = GameStateJson.Read(new ContentNode(json, "preview", string.Empty), data.Enemies, data.Gems, data.Weapons, data.Items, data.Slates);
            var copy = new GameLoop(data, new FakeInputSource(), state, new StatSheet());
            copy.RefreshClassAttribute();
            return copy;
        }

        private static bool RaiseWithExtraPoint(GameLoop copy, string nodeId)
        {
            if (copy.TreeOfNode(nodeId) is not { } found)
                return false;
            var allocation = found.Allocation;
            var before = allocation.Modifiers(found.Tree);
            allocation.PointsEarned++;
            if (allocation.Raise(found.Tree, nodeId) != RankResult.Done)
                return false;
            var after = allocation.Modifiers(found.Tree);
            foreach (var modifier in before)
                copy.Hero.Remove(modifier.InPool(ModifierPool.Tree));
            foreach (var modifier in after)
                copy.Hero.Add(modifier.InPool(ModifierPool.Tree));
            return true;
        }

        private static int IndexOf<T>(System.Collections.Generic.IReadOnlyList<T> list, T item) where T : class
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return i;
            }
            return -1;
        }
    }
}
