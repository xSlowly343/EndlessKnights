using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum FacetResult
    {
        Done,
        BadSlot,
        BadTier,
        Locked,
        UnknownOption,
        Repeat,
        NoCutters,
        NoCrystals
    }

    public sealed class FacetSystem
    {
        private readonly GameState _state;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private FacetRules _rules;

        public FacetSystem(FacetRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public FacetRules Rules => _rules;
        private MountState Mount => _state.Mount;

        public void UseRules(FacetRules rules)
        {
            _rules = rules;
            Rebuild();
        }

        public string Chosen(int slot, int tier)
        {
            var index = Index(slot, tier);
            return index < 0 ? string.Empty : Mount.Facets[index];
        }

        public bool Open(int slot, int tier) => tier >= 0 && tier < _rules.Tiers.Count && slot >= 0 && slot < MountRules.Slots && Mount.Levels[slot] >= _rules.Tiers[tier];

        public int CostOf(int tier) => tier >= 0 && tier < _rules.Costs.Count ? _rules.Costs[tier] : 0;

        public FacetResult Choose(int slot, int tier, string option)
        {
            if (slot < 0 || slot >= MountRules.Slots)
                return FacetResult.BadSlot;
            if (tier < 0 || tier >= _rules.Tiers.Count || Index(slot, tier) < 0)
                return FacetResult.BadTier;
            if (!Open(slot, tier))
                return FacetResult.Locked;
            if (_rules.Option(slot, option) == null)
                return FacetResult.UnknownOption;
            var index = Index(slot, tier);
            var current = Mount.Facets[index];
            if (current == option)
                return FacetResult.Repeat;
            if (current.Length > 0)
            {
                if (Mount.Cutters < CostOf(tier))
                    return FacetResult.NoCutters;
                Mount.Cutters -= CostOf(tier);
            }
            Mount.Facets[index] = option;
            Rebuild();
            return FacetResult.Done;
        }

        public void NoteClassChange()
        {
            if (_state.ClassChanges == 1)
                Mount.FreeRebuilds++;
        }

        public FacetResult RebuildAll()
        {
            if (Mount.FreeRebuilds <= 0)
                return FacetResult.NoCrystals;
            Mount.FreeRebuilds--;
            for (var i = 0; i < Mount.Facets.Length; i++)
                Mount.Facets[i] = string.Empty;
            Rebuild();
            return FacetResult.Done;
        }

        public void NoteGuardian(long stage, bool first)
        {
            if (first && _rules.GuardianEvery > 0 && stage % _rules.GuardianEvery == 0)
                Mount.Cutters += _rules.GuardianCutters;
        }

        public void NoteDailyClaim() => Mount.Cutters += _rules.DailyCutters;

        public void Rebuild()
        {
            _modifiers.Clear();
            for (var slot = 0; slot < MountRules.Slots; slot++)
            {
                for (var tier = 0; tier < _rules.Tiers.Count; tier++)
                {
                    var option = _rules.Option(slot, Chosen(slot, tier));
                    if (option != null && Open(slot, tier))
                        _modifiers.AddRange(option.Lines);
                }
            }
            _layer.Set(_modifiers);
        }

        private int Index(int slot, int tier)
        {
            var index = slot * MountState.Tiers + tier;
            return slot < 0 || slot >= MountRules.Slots || tier < 0 || tier >= MountState.Tiers ? -1 : index;
        }
    }
}
