using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Content.Synergy;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Sim.Rift
{
    public sealed class RiftSystem : IBossListener
    {
        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly ArtifactSystem _artifacts;
        private readonly SynergySystem _synergy;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(16);
        private RiftRules _rules;
        private StageTable _home;
        private EnemyCatalog _enemies;
        private StageTable _world;
        private SynergyRules _synergyRules;
        private IReadOnlyList<RiftModifier> _windowModifiers = System.Array.Empty<RiftModifier>();
        private int[] _unlessMasks = System.Array.Empty<int>();
        private int _favouredMask;
        private long _modifiersFor = -1;
        private int _marks = -1;

        public RiftSystem(GameData data, GameState state, StatSheet hero, StageSystem stage, ArtifactSystem artifacts, SynergySystem synergy)
        {
            _state = state;
            _stage = stage;
            _artifacts = artifacts;
            _synergy = synergy;
            _layer = new ModifierLayer(hero);
            _rules = data.Rift;
            _home = data.Stages;
            _enemies = data.Enemies;
            _synergyRules = data.Synergy;
            _world = BuildWorld();
            stage.BossListener = this;
            ApplyWorld();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public RiftRules Rules => _rules;
        public bool Inside => _state.Rift.Inside;
        public IReadOnlyList<RiftModifier> WindowModifiers => _windowModifiers;

        public bool ModifierActive(int index) => Inside && (_unlessMasks[index] & PartyMarks) == 0;

        public void UseData(GameData data)
        {
            _rules = data.Rift;
            _home = data.Stages;
            _enemies = data.Enemies;
            _synergyRules = data.Synergy;
            _world = BuildWorld();
            _modifiersFor = -1;
            ApplyWorld();
        }

        public RiftWindow? WindowAt(long nowUnixMs) => _rules.Schedule.WindowAt(nowUnixMs);

        public RiftEntry Enter(long nowUnixMs)
        {
            if (Inside)
                return RiftEntry.AlreadyInside;
            if (_state.Dungeons.Inside)
                return RiftEntry.InDungeon;
            if (_stage.Linked)
                return RiftEntry.InParty;
            var open = WindowAt(nowUnixMs);
            return open == null ? RiftEntry.Closed : EnterWindow(open.Value);
        }

        public RiftEntry EnterPartyWindow(long windowId)
        {
            if (Inside && _state.Rift.WindowId == windowId)
                return RiftEntry.AlreadyInside;
            if (_state.Dungeons.Inside)
                return RiftEntry.InDungeon;
            var open = WindowAt(windowId * 1000);
            if (open == null || open.Value.Id != windowId)
                return RiftEntry.Closed;
            Leave();
            return EnterWindow(open.Value);
        }

        private RiftEntry EnterWindow(RiftWindow window)
        {
            var rift = _state.Rift;
            if (rift.WindowId != window.Id)
                rift.StartWindow(window.Id, window.EndUnixMs);
            var riftProgress = rift.OtherWorld ?? new StageProgress();
            rift.OtherWorld = StageProgress.Capture(_state);
            rift.Inside = true;
            riftProgress.Restore(_state);
            SwitchWorld();
            return RiftEntry.Entered;
        }

        public bool Leave()
        {
            if (!Inside)
                return false;
            var rift = _state.Rift;
            var riftProgress = StageProgress.Capture(_state);
            (rift.OtherWorld ?? new StageProgress()).Restore(_state);
            rift.OtherWorld = riftProgress;
            rift.Inside = false;
            SwitchWorld();
            return true;
        }

        public bool CloseIfEnded(long nowUnixMs)
        {
            var rift = _state.Rift;
            if ((_stage.Linked && rift.Inside) || rift.WindowId == 0 || nowUnixMs < rift.WindowEndUnixMs || (rift.OtherWorld == null && !rift.Inside))
                return false;
            Leave();
            rift.EndWindow();
            _artifacts.Rebuild();
            return true;
        }

        public void OnBossDefeated(EnemyType type, long stageBefore)
        {
            if (!Inside)
                return;
            var shards = type == EnemyType.Guardian ? _rules.GuardianShards : _rules.BossShards;
            _state.Rift.Shards += BigNumber.FromDouble(shards);
            if (_state.Stage > _state.Rift.Deepest)
                _state.Rift.Deepest = _state.Stage;
        }

        public void Tick()
        {
            var marks = Inside ? PartyMarks : 0;
            if (marks == _marks)
                return;
            _marks = marks;
            Rebuild();
        }

        private int PartyMarks => _synergy.OwnMarks | _synergy.AllyMarks;

        private StageTable BuildWorld() =>
            _home.ForWorld(_rules.HpMultiplier, _rules.WallPeriod, _rules.WallZoneLength, _rules.WallRate, _rules.BossTimerSeconds);

        private void SwitchWorld()
        {
            ApplyWorld();
            _artifacts.Rebuild();
            _state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            _stage.EnsureEnemy();
        }

        private void ApplyWorld()
        {
            var inside = Inside;
            if (!_state.Dungeons.Inside)
            {
                _stage.UseData(inside ? _world : _home, _enemies);
                _stage.RaisesRecord = !inside;
            }
            LoadWindowModifiers();
            _marks = -1;
            Tick();
        }

        private void LoadWindowModifiers()
        {
            var windowId = _state.Rift.WindowId;
            if (windowId == _modifiersFor)
                return;
            _modifiersFor = windowId;
            _windowModifiers = windowId == 0 ? System.Array.Empty<RiftModifier>() : _rules.ModifiersFor(windowId);
            _unlessMasks = new int[_windowModifiers.Count];
            _favouredMask = 0;
            for (var i = 0; i < _windowModifiers.Count; i++)
            {
                _unlessMasks[i] = _synergyRules.MaskOf(_windowModifiers[i].UnlessMarks);
                _favouredMask |= _synergyRules.MaskOf(_windowModifiers[i].Favours);
            }
        }

        private void Rebuild()
        {
            _buffer.Clear();
            for (var i = 0; i < _windowModifiers.Count; i++)
            {
                if (ModifierActive(i))
                    _buffer.AddRange(_windowModifiers[i].Modifiers);
            }
            _layer.Set(_buffer);
            _synergy.SetFavoured(Inside ? _favouredMask : 0, _rules.MarkBonus);
        }
    }
}
