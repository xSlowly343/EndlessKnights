using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Settings;
using Game.Core.Sim.Rift;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GameLoop
    {
        public const int TicksPerSecond = 10;
        public const double TickSeconds = 1.0 / TicksPerSecond;
        public const int MaxCatchUpTicks = 50;
        public const int MaxRequestedSlots = 64;

        private const double TimeEpsilon = 1e-9;

        private readonly IInputSource _input;
        private readonly InputRules _inputRules;
        private readonly FocusSystem _focus;
        private readonly ResourceSystem _resource;
        private readonly CombatSystem _combat;
        private readonly StageSystem _stage;
        private readonly LootSystem _loot;
        private readonly LevelSystem _levels;
        private readonly DpsMeter _dps;
        private readonly SkillSystem _skills;
        private readonly TriggerSystem _triggers;
        private readonly ModifierLayer _treeLayer;
        private readonly SkillBook _book;
        private readonly PrestigeSystem _prestige;
        private readonly StrikeUpgradeSystem _upgrades;
        private readonly ArtifactSystem _artifacts;
        private readonly TutorialSystem _tutorial;
        private readonly PartyBuffs _partyBuffs;
        private readonly SynergySystem _synergy;
        private readonly RiftSystem _rift;
        private readonly WeaponSystem _weapons;
        private GameData _data;
        private double _pendingSeconds;
        private ulong _castRequests;

        public GameLoop(GameData data, IInputSource input, GameState state, StatSheet hero)
        {
            _input = input;
            _data = data;
            State = state;
            Hero = hero;
            _inputRules = new InputRules(data.InputCaps);
            _focus = new FocusSystem(data.Combat.Focus, state, hero);
            _resource = new ResourceSystem(data.Combat.Resources, state, hero);
            _combat = new CombatSystem(data.Combat, hero, state, Events);
            _loot = new LootSystem(data.Stages, hero, state, Events);
            _levels = new LevelSystem(data.Combat.Hero, data.Stages, data.TreePoints, state, Events);
            _stage = new StageSystem(data.Stages, data.Enemies, hero, state, Events, _loot, _levels);
            _combat.OnKill(_stage);
            _skills = new SkillSystem(state, Events, _resource, _combat, hero, data.Weapons);
            _combat.OnKeyHit(_skills);
            _triggers = new TriggerSystem(state, _skills, _resource);
            _weapons = new WeaponSystem(data.Weapons, state, hero, _skills);
            _artifacts = new ArtifactSystem(data.Artifacts, data.Rift, state, hero);
            _upgrades = new StrikeUpgradeSystem(data.Combat.StrikeUpgrade, state, hero);
            _prestige = new PrestigeSystem(data.Prestige, state, Events, _stage, _upgrades);
            _book = new SkillBook(data.SkillBook, state, _skills, hero);
            _treeLayer = new ModifierLayer(hero);
            _partyBuffs = new PartyBuffs(data.Party.Buffs, state, hero);
            _synergy = new SynergySystem(data.Synergy, state, hero);
            _rift = new RiftSystem(data, state, hero, _stage, _artifacts, _synergy);
            RebuildTree();
            _dps = new DpsMeter(data.Combat.Offline, data.Combat.Focus, state);
            _tutorial = new TutorialSystem(data.Tutorial, state, _upgrades);
            _stage.EnsureEnemy();
        }

        public StatSheet Hero { get; }
        public CombatSystem Combat => _combat;
        public StageSystem Stages => _stage;
        public FocusSystem Focus => _focus;
        public ResourceSystem Resource => _resource;
        public LootSystem Loot => _loot;
        public SkillSystem Skills => _skills;
        public LevelSystem Levels => _levels;
        public SkillBook Book => _book;
        public PrestigeSystem Prestige => _prestige;
        public StrikeUpgradeSystem Upgrades => _upgrades;
        public ArtifactSystem Artifacts => _artifacts;
        public TutorialSystem Tutorial => _tutorial;
        public PartyBuffs PartyBuffs => _partyBuffs;
        public SynergySystem Synergy => _synergy;
        public RiftSystem Rift => _rift;
        public WeaponSystem Weapons => _weapons;
        public GameData Data => _data;
        public BossHint? BossHint => BossHint.For(State, _data.Stages.BossHint, _prestige, _upgrades);

        public GameState State { get; }
        public EventQueue Events { get; } = new EventQueue();
        public InputSnapshot LastInput { get; private set; }
        public InputStats SessionInput { get; } = new InputStats();
        public double MetersPerPixel { get; set; } = InputStats.DefaultMetersPerPixel;
        public bool Paused { get; set; }
        public InputPermissions Permissions { get; set; } = InputPermissions.All;
        public ITickHook? Hook { get; set; }

        public int Advance(double seconds)
        {
            if (Paused || seconds <= 0)
                return 0;

            _pendingSeconds += seconds;
            var ticks = 0;
            while (_pendingSeconds + TimeEpsilon >= TickSeconds)
            {
                _pendingSeconds -= TickSeconds;
                if (ticks == MaxCatchUpTicks)
                {
                    _pendingSeconds = 0;
                    break;
                }
                Tick();
                ticks++;
            }
            if (_pendingSeconds < 0)
                _pendingSeconds = 0;
            return ticks;
        }

        public AllocationResult AllocateNode(string nodeId, string? attribute = null, int? masteryOption = null)
        {
            var result = State.Tree.Allocate(_data.Tree, nodeId, attribute, masteryOption);
            if (result == AllocationResult.Allocated)
                RebuildTree();
            return result;
        }

        public BigNumber? RefundCost(string nodeId) => State.Tree.RefundCost(_data.Tree, nodeId, State.HeroLevel);

        public RefundResult RefundNode(string nodeId)
        {
            var cost = RefundCost(nodeId);
            if (cost == null)
                return RefundResult.NotAllocated;
            if (!State.Tree.CanRemove(_data.Tree, nodeId))
                return RefundResult.BreaksPath;
            if (State.Gold < cost.Value)
                return RefundResult.NotEnoughGold;
            State.Gold -= cost.Value;
            State.Tree.Remove(nodeId);
            RebuildTree();
            return RefundResult.Refunded;
        }

        public RefundResult FreeFullReset(long nowUnixMs)
        {
            if (!State.Tree.FreeResetReady(_data.Tree, nowUnixMs))
                return RefundResult.NotYet;
            State.Tree.Clear();
            State.Tree.LastFreeResetUnixMs = nowUnixMs;
            RebuildTree();
            return RefundResult.Refunded;
        }

        public void RequestCast(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < MaxRequestedSlots)
                _castRequests |= 1UL << slotIndex;
        }

        public void ApplyOffline(OfflineResult result)
        {
            OfflineCalculator.Apply(State, result);
            _stage.EnsureEnemy();
        }

        public bool CompleteActivity(string activityId) =>
            _data.Ascendancy.IsActivity(activityId) && State.Ascendancy.Complete(activityId);

        public SubclassResult ChooseSubclass(string subclassId) =>
            AfterSubclassChange(State.Ascendancy.Choose(_data.Ascendancy, State.Tree.StartClass, subclassId));

        public SubclassResult ChangeSubclassFree(string subclassId, long nowUnixMs) =>
            AfterSubclassChange(State.Ascendancy.ChangeFree(_data.Ascendancy, State.Tree.StartClass, subclassId, nowUnixMs));

        public SubclassResult ChangeSubclassPaid(string subclassId) =>
            AfterSubclassChange(State.Ascendancy.ChangePaid(_data.Ascendancy, State.Tree.StartClass, subclassId));

        public AllocationResult AllocateSubclassNode(string nodeId)
        {
            var result = State.Ascendancy.Allocate(_data.Ascendancy, nodeId);
            if (result == AllocationResult.Allocated)
                RebuildTree();
            return result;
        }

        private SubclassResult AfterSubclassChange(SubclassResult result)
        {
            if (result == SubclassResult.Chosen)
                RebuildTree();
            return result;
        }

        public bool ChangeMastery(string nodeId, int option)
        {
            if (!State.Tree.ChangeMastery(_data.Tree, nodeId, option))
                return false;
            RebuildTree();
            return true;
        }

        public void SwitchPreset()
        {
            var current = new BuildPreset(State.Tree);
            foreach (var slot in State.Skills)
                current.Skills.Add((slot.Skill, slot.Trigger));

            var next = State.OtherPreset;
            next.Tree.PointsEarned = State.Tree.PointsEarned;
            while (State.Skills.Count > 0)
                _skills.Unequip(State.Skills.Count - 1);
            State.Tree = next.Tree;
            State.OtherPreset = current;
            State.ActivePreset = 1 - State.ActivePreset;
            RebuildTree();

            foreach (var (skill, trigger) in next.Skills)
            {
                if (_book.Equip(skill) != BookResult.Done)
                    continue;
                var index = State.Skills.Count - 1;
                State.Skills[index].StartCooldown((int)System.Math.Round(skill.Skill!.CooldownSeconds * TicksPerSecond));
                if (trigger != null)
                    _book.SetTrigger(index, trigger);
            }
        }

        public void RebuildTree()
        {
            State.Tree.DropUnreachable(_data.Tree);
            State.OtherPreset.Tree.DropUnreachable(_data.Tree);
            var modifiers = new List<Modifier>(State.Tree.Modifiers(_data.Tree));
            modifiers.AddRange(State.Ascendancy.Modifiers(_data.Ascendancy));
            _treeLayer.Set(modifiers);
            _skills.RefreshAuras();
            _partyBuffs.Rebuild();
        }

        public void UseData(GameData data)
        {
            _data = data;
            _inputRules.UseCaps(data.InputCaps);
            _focus.UseRules(data.Combat.Focus);
            _resource.UseRules(data.Combat.Resources);
            _combat.UseRules(data.Combat);
            _stage.UseData(data.Stages, data.Enemies);
            _loot.UseData(data.Stages);
            _levels.UseData(data.Combat.Hero, data.Stages, data.TreePoints);
            _dps.UseRules(data.Combat.Offline, data.Combat.Focus);
            _skills.UseGems(data.Gems);
            _weapons.UseCatalog(data.Weapons);
            _skills.UseWeapons(data.Weapons);
            _book.UseRules(data.SkillBook);
            _prestige.UseRules(data.Prestige);
            _upgrades.UseRules(data.Combat.StrikeUpgrade);
            _artifacts.UseCatalog(data.Artifacts, data.Rift);
            _tutorial.UseRules(data.Tutorial);
            _partyBuffs.UseRules(data.Party.Buffs);
            _synergy.UseRules(data.Synergy);
            _rift.UseData(data);
            RebuildTree();
        }

        public void Tick()
        {
            Hook?.BeforeTick();
            var firstEvent = Events.Count;
            LastInput = Permissions.Apply(_input.Drain());
            SessionInput.Add(LastInput, MetersPerPixel);
            State.TotalInput.Add(LastInput, MetersPerPixel);
            var raw = _inputRules.Apply(LastInput);
            var input = raw.WithMouse(raw.MousePx * System.Math.Max(0, Hero.Value(CombatStats.MouseDistance, 1, StatTags.Mouse)));
            var idleTicksBefore = State.IdleTicks;
            _focus.Tick(input);
            _combat.NotePause(idleTicksBefore, input.HasInput);
            _resource.Tick(input);
            _skills.Tick(input, idleTicksBefore);
            _skills.CastFromButtons(input);
            CastRequested();
            _triggers.Tick(input);
            _partyBuffs.Tick();
            _synergy.Tick();
            _rift.Tick();
            _stage.Tick();
            _combat.Tick(input);
            _dps.Tick(_combat.LastTickDamage);
            State.Tick++;
            if (Permissions.Consent)
                _tutorial.Tick(LastInput, Events, firstEvent);
            Hook?.AfterTick();
        }

        private void CastRequested()
        {
            for (var index = 0; _castRequests != 0; index++)
            {
                var bit = 1UL << index;
                if ((_castRequests & bit) == 0)
                    continue;
                _castRequests &= ~bit;
                _skills.TryCast(index);
            }
        }
    }
}
