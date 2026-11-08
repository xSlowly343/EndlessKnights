using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct MountSlotRow
    {
        public readonly int Slot;
        public readonly string Name;
        public readonly int Level;
        public readonly int Unlocked;
        public readonly int Max;
        public readonly double Factor;
        public readonly long ScanCost;
        public readonly BigNumber GoldCost;
        public readonly MountResult Check;

        public MountSlotRow(int slot, string name, int level, int unlocked, int max, double factor, long scanCost, BigNumber goldCost, MountResult check)
        {
            Slot = slot;
            Name = name;
            Level = level;
            Unlocked = unlocked;
            Max = max;
            Factor = factor;
            ScanCost = scanCost;
            GoldCost = goldCost;
            Check = check;
        }

        public string Badge => Level > 0 ? "+" + Level.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public readonly struct FacetCell
    {
        public readonly int Tier;
        public readonly int Level;
        public readonly bool Open;
        public readonly string Chosen;
        public readonly int Cost;
        public readonly IReadOnlyList<FacetOption> Options;

        public FacetCell(int tier, int level, bool open, string chosen, int cost, IReadOnlyList<FacetOption> options)
        {
            Tier = tier;
            Level = level;
            Open = open;
            Chosen = chosen;
            Cost = cost;
            Options = options;
        }

        public bool Free => Chosen.Length == 0;
    }

    public static class MountView
    {
        private static readonly string[] SlotKeys =
        {
            "hero.slot.weapon", "hero.slot.helmet", "hero.slot.armor", "hero.slot.gloves", "hero.slot.boots", "hero.slot.amulet", "hero.slot.ring", "hero.slot.ring"
        };

        public static string SlotName(int slot) => slot >= 0 && slot < SlotKeys.Length ? Texts.Get(SlotKeys[slot]) : string.Empty;

        public static IReadOnlyList<MountSlotRow> Rows(GameLoop loop)
        {
            var mount = loop.Mount;
            var rows = new List<MountSlotRow>(MountRules.Slots);
            for (var slot = 0; slot < MountRules.Slots; slot++)
                rows.Add(new MountSlotRow(slot, SlotName(slot), mount.Level(slot), mount.Unlocked, mount.Rules.MaxLevel, mount.Factor(slot), mount.ScanCost(slot), mount.GoldCost(slot), mount.Check(slot)));
            return rows;
        }

        public static IReadOnlyList<FacetCell> Facets(GameLoop loop, int slot)
        {
            var facets = loop.Facets;
            var rules = facets.Rules;
            var options = slot >= 0 && slot < rules.Slots.Count ? rules.Slots[slot] : System.Array.Empty<FacetOption>();
            var cells = new List<FacetCell>(rules.Tiers.Count);
            for (var tier = 0; tier < rules.Tiers.Count; tier++)
                cells.Add(new FacetCell(tier, rules.Tiers[tier], facets.Open(slot, tier), facets.Chosen(slot, tier), facets.CostOf(tier), options));
            return cells;
        }

        public static string OptionName(FacetOption option) => Texts.Pick(option.NameRu, option.NameEn);

        public static string Summary(GameLoop loop)
        {
            var mount = loop.Mount;
            var rules = mount.Rules;
            var lowest = mount.Lowest;
            var full = rules.FullDamage(lowest);
            var next = NextFull(rules, lowest);
            var nextText = next == null ? Texts.Get("mount.fullMax") : Texts.Format("mount.fullNext", next.Value.Level, Percent(next.Value.Damage));
            return Texts.Format("mount.summary", Whole(mount.State.Scan), mount.State.Cutters, mount.Unlocked, Percent(full), nextText);
        }

        public static FullMount? NextFull(MountRules rules, int lowest)
        {
            FullMount? next = null;
            foreach (var step in rules.Full)
            {
                if (step.Level > lowest && (next == null || step.Level < next.Value.Level))
                    next = step;
            }
            return next;
        }

        private static string Percent(double share) => string.Format(CultureInfo.InvariantCulture, "+{0:0.#}%", share * 100);

        private static string Whole(double value) => ((long)System.Math.Floor(System.Math.Max(0, value))).ToString(CultureInfo.InvariantCulture);
    }
}
