using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public sealed class BotMeta
    {
        private const int QualityRerollsPerAct = 3;
        private const int MountRaisesPerAct = 8;

        private readonly GameLoop _loop;
        private readonly NodeScore _score;

        public BotMeta(GameLoop loop, NodeScore score)
        {
            _loop = loop;
            _score = score;
        }

        public void Act()
        {
            RaiseMount();
            ChooseFacets();
            RerollQuality();
            ClaimQuests();
            CraftSets();
        }

        private void ClaimQuests()
        {
            var quests = _loop.Quests;
            for (var i = 0; i < quests.State.Daily.Count; i++)
                quests.Claim(false, i);
            for (var i = 0; i < quests.State.Weekly.Count; i++)
                quests.Claim(true, i);
        }

        private void CraftSets()
        {
            var drops = _loop.Drops;
            if (!_loop.Unlocks.IsOpen(Feature.SetCraft) || drops.BagFull)
                return;
            var set = SetToCraft();
            if (set != null)
                drops.CraftSetPiece(set.Id);
        }

        private SetDef? SetToCraft()
        {
            var ceiling = _loop.Drops.Rules.CeilingAt(System.Math.Max(1, _loop.State.RecordStage));
            SetDef? best = null;
            foreach (var set in _loop.Data.Items.Sets)
            {
                if (set.Event != null || set.Rarity > ceiling || _loop.Gear.Collected(set))
                    continue;
                if (best == null || set.Rarity > best.Rarity || (set.Rarity == best.Rarity && _loop.Gear.CollectedPieces(set) > _loop.Gear.CollectedPieces(best)))
                    best = set;
            }
            return best;
        }

        private void RaiseMount()
        {
            var mount = _loop.Mount;
            for (var i = 0; i < MountRaisesPerAct; i++)
            {
                var slot = LowestRaisable(mount);
                if (slot < 0 || mount.Raise(slot) != MountResult.Done)
                    return;
            }
        }

        private static int LowestRaisable(MountSystem mount)
        {
            var best = -1;
            for (var slot = 0; slot < MountRules.Slots; slot++)
            {
                if (mount.Check(slot) == MountResult.Done && (best < 0 || mount.Level(slot) < mount.Level(best)))
                    best = slot;
            }
            return best;
        }

        private void ChooseFacets()
        {
            var facets = _loop.Facets;
            var slots = facets.Rules.Slots;
            for (var slot = 0; slot < MountRules.Slots && slot < slots.Count; slot++)
            {
                for (var tier = 0; tier < facets.Rules.Tiers.Count; tier++)
                {
                    if (!facets.Open(slot, tier) || facets.Chosen(slot, tier).Length > 0)
                        continue;
                    var option = BestOption(slots[slot]);
                    if (option != null)
                        facets.Choose(slot, tier, option);
                }
            }
        }

        private string? BestOption(IReadOnlyList<FacetOption> options)
        {
            string? best = null;
            var bestScore = double.NegativeInfinity;
            foreach (var option in options)
            {
                var score = _score.Of(option.Lines);
                if (score > bestScore)
                {
                    best = option.Id;
                    bestScore = score;
                }
            }
            return best;
        }

        private void RerollQuality()
        {
            for (var i = 0; i < QualityRerollsPerAct; i++)
            {
                if (_loop.Forge.RerollQuality() != ForgeResult.Done)
                    return;
            }
        }

        public bool BuyCheapestEchoUpgrade(BigNumber budget)
        {
            if (_loop.Artifacts.InRift)
                return false;
            var kind = EchoSink.None;
            string? id = null;
            var cost = budget;
            FindResonance(ref kind, ref id, ref cost);
            FindSpirit(ref kind, ref id, ref cost);
            FindAtlas(ref kind, ref id, ref cost);
            if (kind == EchoSink.None || id == null || _loop.State.Echo < cost)
                return false;
            return kind switch
            {
                EchoSink.Resonance => _loop.Resonance.Raise(id) == ResonanceResult.Raised,
                EchoSink.Spirit => _loop.Spirits.Raise(id) is SpiritResult.Raised or SpiritResult.Awakened,
                EchoSink.Atlas => _loop.Atlas.Allocate(id) == AtlasResult.Allocated,
                _ => false
            };
        }

        private void FindResonance(ref EchoSink kind, ref string? id, ref BigNumber cost)
        {
            if (!_loop.Unlocks.IsOpen(Feature.Resonance))
                return;
            foreach (var branch in _loop.Resonance.Catalog.All)
            {
                if (_loop.Resonance.Level(branch.Id) >= branch.MaxLevel)
                    continue;
                var next = _loop.Resonance.Cost(branch.Id);
                if (next < cost)
                {
                    kind = EchoSink.Resonance;
                    id = branch.Id;
                    cost = next;
                }
            }
        }

        private void FindSpirit(ref EchoSink kind, ref string? id, ref BigNumber cost)
        {
            if (!_loop.Unlocks.IsOpen(Feature.Spirits))
                return;
            foreach (var spirit in _loop.Spirits.Catalog.All)
            {
                if (_loop.Spirits.Level(spirit.Id) >= spirit.MaxLevel)
                    continue;
                var next = _loop.Spirits.Cost(spirit);
                if (next < cost)
                {
                    kind = EchoSink.Spirit;
                    id = spirit.Id;
                    cost = next;
                }
            }
        }

        private void FindAtlas(ref EchoSink kind, ref string? id, ref BigNumber cost)
        {
            if (!_loop.Unlocks.IsOpen(Feature.Atlas))
                return;
            foreach (var node in _loop.Atlas.Catalog.All)
            {
                if (_loop.Atlas.Taken(node.Id))
                    continue;
                var next = BigNumber.FromDouble(node.Cost);
                if (next < cost && _loop.Atlas.CanAllocate(node.Id) == AtlasResult.Allocated)
                {
                    kind = EchoSink.Atlas;
                    id = node.Id;
                    cost = next;
                }
            }
        }

        private enum EchoSink
        {
            None,
            Resonance,
            Spirit,
            Atlas
        }
    }
}
