using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum EnchantResult
    {
        Done,
        Closed,
        Unknown,
        AlreadyEnchanted,
        NotEnoughEcho
    }

    public sealed class ArtifactSystem
    {
        private const double MinUnlockDiscount = 0.1;
        private static readonly int TypeCount = System.Enum.GetValues(typeof(ArtifactType)).Length;

        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private ArtifactCatalog _catalog;
        private RiftRules _rift;

        public ArtifactSystem(ArtifactCatalog catalog, RiftRules rift, GameState state, StatSheet hero)
        {
            _catalog = catalog;
            _rift = rift;
            _state = state;
            _hero = hero;
            _layer = new ModifierLayer(hero, ModifierPool.Artifact);
            Rebuild();
        }

        public void UseCatalog(ArtifactCatalog catalog, RiftRules rift)
        {
            _catalog = catalog;
            _rift = rift;
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public System.Action? Rebuilt { get; set; }

        public bool InRift => _state.Rift.Inside;
        public IReadOnlyDictionary<string, int> Owned => OwnedLevels;
        public BigNumber Currency => InRift ? _state.Rift.Shards : _state.Echo;

        public BigNumber UnlockCost => UnlockCurve.At(OwnedLevels.Count + 1) * UnlockDiscount;

        public bool AllUnlocked => OwnedLevels.Count >= _catalog.All.Count && Locked().Count == 0;

        public bool WaitsForGroup => !AllUnlocked && Locked().Count == 0;

        public int OpenGroup
        {
            get
            {
                var group = int.MaxValue;
                foreach (var artifact in _catalog.All)
                {
                    if (!OwnedLevels.ContainsKey(artifact.Id))
                        group = System.Math.Min(group, artifact.Group);
                }
                return group == int.MaxValue ? _catalog.Groups : group;
            }
        }

        public ArtifactAwakening Awakening => _catalog.Awakening;
        public AwakeningState AwakeningState => _state.Awakening;
        public bool EnchantOpen => Awakening.Open && !InRift && _state.Artifacts.Count >= _catalog.All.Count;

        public BigNumber EnchantCost => _catalog.UnlockCost.At(_state.Artifacts.Count + _state.Awakening.Enchanted.Count + 1) * UnlockDiscount;

        public bool AllEnchanted => _state.Awakening.Enchanted.Count >= _catalog.All.Count;

        public EnchantResult Enchant(string id)
        {
            if (!EnchantOpen)
                return EnchantResult.Closed;
            if (!_catalog.TryGet(id, out _) || !_state.Artifacts.ContainsKey(id))
                return EnchantResult.Unknown;
            var awakening = _state.Awakening;
            if (awakening.Enchanted.Contains(id))
                return EnchantResult.AlreadyEnchanted;
            var cost = EnchantCost;
            if (_state.Echo < cost)
                return EnchantResult.NotEnoughEcho;
            _state.Echo -= cost;
            awakening.Enchanted.Add(id);
            if (!awakening.Progress.ContainsKey(id))
                awakening.Progress[id] = 0;
            Rebuild();
            return EnchantResult.Done;
        }

        public bool IsEnchanted(string id) => _state.Awakening.Enchanted.Contains(id);

        public double AwakeningProgress(string id) => _state.Awakening.Progress.TryGetValue(id, out var progress) ? progress : 0;

        public int AwakeningLevel(string id) =>
            _catalog.TryGet(id, out var artifact) ? Awakening.LevelAt(artifact.Awaken, AwakeningProgress(id)) : 0;

        public void SkipAwakening()
        {
            var awakening = _state.Awakening;
            for (var i = 0; i < ArtifactAwakening.InputCount; i++)
                awakening.Last[i] = Counter((AwakeningInput)i);
            awakening.Primed = true;
        }

        public void TickAwakening()
        {
            var awakening = _state.Awakening;
            if (!awakening.Primed)
            {
                for (var i = 0; i < ArtifactAwakening.InputCount; i++)
                    awakening.Last[i] = Counter((AwakeningInput)i);
                awakening.Primed = true;
                return;
            }
            var changed = false;
            for (var i = 0; i < ArtifactAwakening.InputCount; i++)
            {
                var input = (AwakeningInput)i;
                var now = Counter(input);
                var delta = now - awakening.Last[i];
                awakening.Last[i] = now;
                if (delta > 0 && !InRift)
                    changed |= Feed(input, delta);
            }
            if (changed)
                Rebuild();
        }

        private bool Feed(AwakeningInput input, double amount)
        {
            var awakening = _state.Awakening;
            var ids = EnchantedBy(input);
            var changed = false;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var before = awakening.Progress.TryGetValue(id, out var progress) ? progress : 0;
                awakening.Progress[id] = before + amount;
                changed |= Awakening.LevelAt(input, before + amount) != Awakening.LevelAt(input, before);
            }
            return changed;
        }

        private readonly List<string>[] _enchantedByInput = NewInputLists();
        private int _indexedEnchants = -1;
        private ArtifactCatalog? _indexedCatalog;

        private static List<string>[] NewInputLists()
        {
            var lists = new List<string>[ArtifactAwakening.InputCount];
            for (var i = 0; i < lists.Length; i++)
                lists[i] = new List<string>();
            return lists;
        }

        private List<string> EnchantedBy(AwakeningInput input)
        {
            var enchanted = _state.Awakening.Enchanted;
            if (_indexedEnchants != enchanted.Count || _indexedCatalog != _catalog)
            {
                foreach (var list in _enchantedByInput)
                    list.Clear();
                foreach (var id in enchanted)
                {
                    if (_catalog.TryGet(id, out var artifact))
                        _enchantedByInput[(int)artifact.Awaken].Add(id);
                }
                _indexedEnchants = enchanted.Count;
                _indexedCatalog = _catalog;
            }
            return _enchantedByInput[(int)input];
        }

        private double Counter(AwakeningInput input) => input switch
        {
            AwakeningInput.Keys => _state.TotalInput.Keys,
            AwakeningInput.Clicks => _state.TotalInput.Clicks,
            AwakeningInput.MouseMeters => _state.TotalInput.MouseMeters,
            AwakeningInput.Kills => _state.Kills,
            AwakeningInput.Bosses => _state.BossKills,
            _ => 0
        };

        private double AwakenedScale(string id) => InRift || !IsEnchanted(id) ? 1 : Awakening.Scale(AwakeningLevel(id));

        public BigNumber LevelCost(string id) =>
            OwnedLevels.TryGetValue(id, out var level) ? LevelCurve.At(level) : BigNumber.Zero;

        public bool IsMaxed(string id) =>
            OwnedLevels.TryGetValue(id, out var level) && _catalog.TryGet(id, out var artifact) && artifact.Capped && level >= artifact.MaxLevel;

        private double UnlockDiscount => System.Math.Max(MinUnlockDiscount, _hero.Value(CombatStats.ArtifactCost, 1));

        private Dictionary<string, int> OwnedLevels => InRift ? _state.Rift.Artifacts : _state.Artifacts;
        private ExponentialCurve UnlockCurve => InRift ? _rift.UnlockCost : _catalog.UnlockCost;
        private ExponentialCurve LevelCurve => InRift ? _rift.LevelCost : _catalog.LevelCost;

        public IReadOnlyList<(ArtifactDef Artifact, double Chance)> Chances()
        {
            var locked = Locked();
            var chances = new List<(ArtifactDef, double)>(locked.Count);
            foreach (var artifact in locked)
                chances.Add((artifact, 1.0 / locked.Count));
            return chances;
        }

        public Unlocks? Unlocks { get; set; }

        public bool Open => InRift ? _state.HighestStage >= _rift.StageOf(RiftFeature.Artifacts) : Unlocks?.IsOpen(Feature.Artifacts) != false;

        public ArtifactDef? TryUnlock()
        {
            if (!Open)
                return null;
            var locked = Locked();
            var cost = UnlockCost;
            if (locked.Count == 0 || Currency < cost)
                return null;
            Spend(cost);
            var artifact = locked[_state.Rng.NextInt(0, locked.Count)];
            OwnedLevels[artifact.Id] = 1;
            Rebuild();
            return artifact;
        }

        public bool TryLevelUp(string id)
        {
            if (!Open || !OwnedLevels.TryGetValue(id, out var level) || IsMaxed(id))
                return false;
            var cost = LevelCurve.At(level);
            if (Currency < cost)
                return false;
            Spend(cost);
            OwnedLevels[id] = level + 1;
            Rebuild();
            return true;
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            var owned = OwnedLevels;
            var boosts = TypeBoosts(owned, out var allDamageBoost);
            foreach (var artifact in _catalog.All)
            {
                if (!owned.TryGetValue(artifact.Id, out var saved))
                    continue;
                var level = artifact.EffectiveLevel(saved);
                var awakened = AwakenedScale(artifact.Id);
                var scale = (artifact.Type == ArtifactType.Meta ? 1 : 1 + boosts[(int)artifact.Type]) * awakened;
                if (artifact.Class == null || _state.Is(artifact.Class.Value))
                {
                    foreach (var modifier in artifact.PerLevel)
                        modifiers.Add(modifier.Kind == ModifierKind.More ? Compounded(modifier, level, awakened) : modifier.WithValue(modifier.Value * level * scale));
                    foreach (var modifier in artifact.PerArtifact)
                        modifiers.Add(modifier.WithValue(modifier.Value * level * owned.Count * scale));
                }
                modifiers.Add(General(level, allDamageBoost));
            }
            _layer.Set(modifiers);
            Rebuilt?.Invoke();
        }

        public const double MaxCompoundedMultiplier = 1e100;

        private static Modifier Compounded(Modifier modifier, int level, double awakened) =>
            modifier.WithValue(System.Math.Min(MaxCompoundedMultiplier, System.Math.Pow(1 + modifier.Value * awakened, level)) - 1);

        private Modifier General(int level, double boost)
        {
            var perLevel = _catalog.AllDamagePerLevel * (1 + boost);
            return _catalog.AllDamageCompounds
                ? new Modifier(CombatStats.Damage, ModifierKind.More, System.Math.Pow(1 + perLevel, level) - 1)
                : new Modifier(CombatStats.Damage, ModifierKind.Increased, perLevel * level);
        }

        private double[] TypeBoosts(Dictionary<string, int> owned, out double allDamageBoost)
        {
            var boosts = new double[TypeCount];
            allDamageBoost = 0;
            foreach (var artifact in _catalog.All)
            {
                if (!owned.TryGetValue(artifact.Id, out var saved))
                    continue;
                var level = artifact.EffectiveLevel(saved);
                var awakened = level * AwakenedScale(artifact.Id);
                allDamageBoost += artifact.BoostAllDamage * awakened;
                foreach (var type in artifact.BoostTypes)
                    boosts[(int)type] += artifact.Boost * awakened;
            }
            return boosts;
        }

        private List<ArtifactDef> Locked()
        {
            var locked = new List<ArtifactDef>();
            var group = int.MaxValue;
            foreach (var artifact in _catalog.All)
            {
                if (!OwnedLevels.ContainsKey(artifact.Id))
                    group = System.Math.Min(group, artifact.Group);
            }
            if (Unlocks != null && group != int.MaxValue && !Unlocks.ArtifactGroupOpen(group))
                return locked;
            foreach (var artifact in _catalog.All)
            {
                if (artifact.Group == group && !OwnedLevels.ContainsKey(artifact.Id))
                    locked.Add(artifact);
            }
            return locked;
        }

        private void Spend(BigNumber cost)
        {
            if (InRift)
                _state.Rift.Shards -= cost;
            else
                _state.Echo -= cost;
        }
    }
}
