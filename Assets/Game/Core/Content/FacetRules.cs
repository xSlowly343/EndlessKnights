using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class FacetOption
    {
        public FacetOption(string id, string nameRu, string nameEn, IReadOnlyList<Modifier> lines)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Lines = lines;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Lines { get; }
    }

    public sealed class FacetRules
    {
        public IReadOnlyList<int> Tiers { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<int> Costs { get; private set; } = Array.Empty<int>();
        public IReadOnlyList<IReadOnlyList<FacetOption>> Slots { get; private set; } = Array.Empty<IReadOnlyList<FacetOption>>();
        public int GuardianCutters { get; private set; }
        public long GuardianEvery { get; private set; } = 500;
        public int DailyCutters { get; private set; }

        public FacetOption? Option(int slot, string id)
        {
            if (slot < 0 || slot >= Slots.Count)
                return null;
            var options = Slots[slot];
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Id == id)
                    return options[i];
            }
            return null;
        }

        internal static FacetRules Read(ContentNode node)
        {
            var tiers = new List<int>();
            foreach (var tier in node.Field("tiers").Items())
                tiers.Add(tier.AsInt(1, 100));
            var costs = new List<int>();
            foreach (var cost in node.Field("costs").Items())
                costs.Add(cost.AsInt(0, 1000));
            if (costs.Count != tiers.Count)
                throw node.Field("costs").Error($"needs one cost per tier ({tiers.Count})");
            var slots = new List<IReadOnlyList<FacetOption>>();
            foreach (var slot in node.Field("slots").Items())
            {
                var options = new List<FacetOption>();
                foreach (var option in slot.Field("options").Items())
                {
                    var name = option.Field("name");
                    options.Add(new FacetOption(option.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), ModifierReader.ReadList(option.Field("lines"))));
                }
                slots.Add(options);
            }
            if (slots.Count != MountRules.Slots)
                throw node.Field("slots").Error($"needs {MountRules.Slots} slots");
            var sources = node.Field("cutters");
            return new FacetRules
            {
                Tiers = tiers,
                Costs = costs,
                Slots = slots,
                GuardianCutters = sources.Field("guardian").AsInt(0, 100),
                GuardianEvery = sources.Field("guardianEvery").AsLong(1),
                DailyCutters = sources.Field("daily").AsInt(0, 100)
            };
        }
    }
}
