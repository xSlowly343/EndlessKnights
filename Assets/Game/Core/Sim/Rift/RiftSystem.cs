using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Items;
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
        private const ulong EnemySalt = 0xE7E_3135UL;

        private EnemyCatalog _enemies;
        private EnemyCatalog? _windowEnemies;
        private long _windowEnemiesFor = -1;
        private StageTable _world;
        private SynergyRules _synergyRules;
        private IReadOnlyList<RiftModifier> _windowModifiers = System.Array.Empty<RiftModifier>();
        private int[] _unlessMasks = System.Array.Empty<int>();
        private long _modifiersFor = -1;
        private int _roles = -1;

        public SlateSystem? Slates { get; set; }
        public GearSystem? Gear { get; set; }
        private RiftModifier? _keyed;

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
            _windowEnemies = null;
            _synergyRules = data.Synergy;
            _world = BuildWorld();
            stage.BossListener = this;
            ApplyWorld();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public RiftRules Rules => _rules;
        public bool Inside => _state.Rift.Inside;
        public IReadOnlyList<RiftModifier> WindowModifiers => _windowModifiers;

        public const int NotLifted = -1;
        public const int LiftedBySelf = PartyRules.MaxSlots;

        public bool ModifierActive(int index) => Inside && (_unlessMasks[index] & _synergy.PartyRoles) == 0;

        public int LiftingRoles(int index) => Inside ? _unlessMasks[index] & _synergy.PartyRoles : 0;

        public int LiftedBy(int index)
        {
            var unless = LiftingRoles(index);
            if (unless == 0)
                return NotLifted;
            if ((_synergy.OwnRole & unless) != 0)
                return LiftedBySelf;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if ((_synergy.AllyRoleOf(slot) & unless) != 0)
                    return slot;
            }
            return NotLifted;
        }

        public void UseData(GameData data)
        {
            _rules = data.Rift;
            _home = data.Stages;
            _enemies = data.Enemies;
            _windowEnemies = null;
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
            if (_state.Dungeons.Inside || _state.Events.FightInside)
                return RiftEntry.InDungeon;
            if (_stage.Linked)
                return RiftEntry.InParty;
            var rift = _state.Rift;
            if (rift.WindowId != 0 && nowUnixMs < rift.WindowEndUnixMs && WindowAt(rift.WindowId * 1000) != null)
                return EnterRift(rift.WindowId, rift.WindowEndUnixMs);
            var open = WindowAt(nowUnixMs);
            return open == null ? RiftEntry.Closed : EnterWindow(open.Value);
        }

        public RiftEntry EnterPartyRift(long riftId)
        {
            if (Inside && _state.Rift.WindowId == riftId)
                return RiftEntry.AlreadyInside;
            if (_state.Dungeons.Inside || _state.Events.FightInside)
                return RiftEntry.InDungeon;
            if (WindowAt(riftId * 1000) == null)
                return RiftEntry.Closed;
            Leave();
            return EnterRift(riftId, PartyRiftEnd(riftId));
        }

        public long PartyRiftEnd(long riftId) => riftId * 1000 + _rules.PartyDurationMs;

        private RiftEntry EnterWindow(RiftWindow window) => EnterRift(window.Id, window.EndUnixMs);

        private RiftEntry EnterRift(long riftId, long endUnixMs)
        {
            var rift = _state.Rift;
            if (rift.WindowId != riftId)
                rift.StartWindow(riftId, endUnixMs);
            _state.Peaceful.Mode = PeacefulMode.Battle;
            var riftProgress = rift.OtherWorld ?? new StageProgress();
            rift.OtherWorld = StageProgress.Capture(_state);
            rift.Inside = true;
            riftProgress.Restore(_state);
            var fresh = rift.OtherHero == null;
            rift.OtherHero ??= HeroKit.Fresh(_state, riftId);
            HeroKit.Swap(_state, rift.OtherHero);
            SwitchWorld();
            HeroSwapped?.Invoke(fresh);
            return RiftEntry.Entered;
        }

        public bool Leave()
        {
            if (!Inside)
                return false;
            if (_state.Dungeons.Inside)
                BeforeLeave?.Invoke();
            var rift = _state.Rift;
            var riftProgress = StageProgress.Capture(_state);
            (rift.OtherWorld ?? new StageProgress()).Restore(_state);
            rift.OtherWorld = riftProgress;
            rift.Inside = false;
            var swapped = rift.OtherHero != null;
            if (swapped)
                HeroKit.Swap(_state, rift.OtherHero!);
            SwitchWorld();
            if (swapped)
                HeroSwapped?.Invoke(false);
            return true;
        }

        public bool LeaveIfAway(double awaySeconds, double afterSeconds) => !_stage.Linked && awaySeconds >= afterSeconds && Leave();

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
            if (!Inside || _state.Dungeons.Inside)
                return;
            var shards = type == EnemyType.Guardian ? _rules.GuardianShards : _rules.BossShards;
            _state.Rift.Shards += BigNumber.FromDouble(shards);
            var forge = _rules.Forge;
            _state.Rift.Sparks += type == EnemyType.Guardian ? forge.GuardianSparks : forge.BossSparks;
            if (_state.Stage <= _state.Rift.Deepest)
                return;
            _state.Rift.Sparks += (forge.MilestonesUpTo(_state.Stage) - forge.MilestonesUpTo(_state.Rift.Deepest)) * forge.MilestoneSparks;
            _state.Rift.Deepest = _state.Stage;
            Slates?.OnRiftBoss();
        }

        public long Sparks => _state.Rift.Sparks;

        public bool SalvageSparks(ItemRarity rarity)
        {
            if (!Inside)
                return false;
            _state.Rift.Sparks += _rules.Forge.SalvageSparks(rarity);
            return true;
        }

        public long CreditShop(long riftId, long boughtSparks)
        {
            var rift = _state.Rift;
            if (riftId != rift.WindowId || riftId == 0 || boughtSparks <= rift.ShopCredited)
                return 0;
            var credit = boughtSparks - rift.ShopCredited;
            rift.ShopCredited = boughtSparks;
            rift.Sparks += credit;
            return credit;
        }

        public void Tick()
        {
            var roles = Inside ? _synergy.PartyRoles : 0;
            if (roles == _roles)
                return;
            _roles = roles;
            Rebuild();
        }

        public System.Action? BeforeLeave { get; set; }

        public System.Action<bool>? HeroSwapped { get; set; }

        public static StageTable BuildWorld(StageTable home, RiftRules rules) =>
            home.ForWorld(rules.HpMultiplier, rules.WallPeriod, rules.WallZoneLength, rules.WallRate, rules.BossTimerSeconds);

        private StageTable BuildWorld() => BuildWorld(_home, _rules);

        public EnemyCatalog WindowEnemies
        {
            get
            {
                var window = _state.Rift.WindowId;
                if (_windowEnemies == null || _windowEnemiesFor != window)
                {
                    _windowEnemies = _enemies.Shuffled((ulong)window ^ EnemySalt);
                    _windowEnemiesFor = window;
                }
                return _windowEnemies;
            }
        }

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
                _stage.UseData(inside ? _world : _home, inside ? WindowEnemies : _enemies);
                _stage.RaisesRecord = !inside;
            }
            LoadWindowModifiers();
            _roles = -1;
            Tick();
        }

        private void LoadWindowModifiers()
        {
            var riftId = _state.Rift.WindowId;
            if (riftId == _modifiersFor)
                return;
            _modifiersFor = riftId;
            var windowId = WindowAt(riftId * 1000)?.Id ?? riftId;
            _windowModifiers = riftId == 0 ? System.Array.Empty<RiftModifier>() : _rules.ModifiersFor(windowId);
            _keyed = riftId == 0 ? null : _rules.KeyedModifier(windowId);
            _unlessMasks = new int[_windowModifiers.Count];
            for (var i = 0; i < _windowModifiers.Count; i++)
                _unlessMasks[i] = _synergyRules.RoleMaskOf(_windowModifiers[i].UnlessRoles);
        }

        public RiftModifier? KeyedModifier => _keyed;

        public bool KeyHeld
        {
            get
            {
                if (!Inside || _keyed == null || Gear == null)
                    return false;
                foreach (var set in _rules.WindowSet.KeysOf(_keyed.Id))
                {
                    if (Gear.Collected(set))
                        return true;
                }
                return false;
            }
        }

        public void Refresh()
        {
            _roles = -1;
            Tick();
        }

        private void Rebuild()
        {
            _buffer.Clear();
            var softened = KeyHeld ? _keyed : null;
            var share = 1 - _rules.WindowSet.SoftenShare;
            for (var i = 0; i < _windowModifiers.Count; i++)
            {
                if (!ModifierActive(i))
                    continue;
                var modifiers = _windowModifiers[i].Modifiers;
                if (!ReferenceEquals(_windowModifiers[i], softened))
                {
                    _buffer.AddRange(modifiers);
                    continue;
                }
                foreach (var modifier in modifiers)
                    _buffer.Add(modifier.WithValue(modifier.Value * share));
            }
            _layer.Set(_buffer);
        }
    }
}
