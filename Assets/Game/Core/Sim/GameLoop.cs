using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
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
        private readonly ChronicleSystem _chronicle;
        private readonly AtlasSystem _atlas;
        private readonly ResonanceSystem _resonance;
        private readonly SpiritSystem _spirits;
        private readonly TutorialSystem _tutorial;
        private readonly PartyBuffs _partyBuffs;
        private readonly SynergySystem _synergy;
        private readonly GhostSystem _ghost;
        private readonly RaidTally _raid;
        private readonly ClanTreeBonus _clanTree;
        private readonly RiftSystem _rift;
        private readonly WeaponSystem _weapons;
        private readonly DropSystem _drops;
        private readonly OverloadSystem _overload;
        private readonly BannerSystem _banner;
        private readonly PathSystem _paths;
        private readonly GemExperienceSystem _gemExperience;
        private readonly GearSystem _gear;
        private readonly JewelSystem _jewels;
        private readonly GateSystem _gates;
        private readonly Unlocks _unlocks;
        private readonly DungeonSystem _dungeons;
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
            _stage.KillTimeListener = new KillResourceSystem(state, hero, _resource, _combat);
            _overload = new OverloadSystem(state, hero, _resource, _combat);
            _skills = new SkillSystem(state, Events, _resource, _combat, hero, data.Weapons);
            _combat.OnKeyHit(_skills);
            _triggers = new TriggerSystem(state, _skills, _resource);
            _weapons = new WeaponSystem(data.Weapons, state, hero, _skills);
            _gear = new GearSystem(data.Items, state, hero);
            _gear.Rebuilt = _weapons.RefreshInnate;
            _weapons.RefreshInnate();
            _drops = new DropSystem(data.Drops, data.Stages, data.Gems, data.Items, _weapons, _gear, state, hero, Events);
            _stage.Drops = _drops;
            _artifacts = new ArtifactSystem(data.Artifacts, data.Rift, state, hero);
            _jewels = new JewelSystem(data.Tree, state, hero);
            _gates = new GateSystem(data.Stages, state, hero, Events);
            _stage.Gates = _gates;
            _artifacts.Rebuilt = _jewels.Rebuild;
            _chronicle = new ChronicleSystem(data.Chronicle, state, hero, Events, _levels);
            _atlas = new AtlasSystem(data.Atlas, state, hero);
            _resonance = new ResonanceSystem(data.Resonance, state, hero);
            _spirits = new SpiritSystem(data.Spirits, state, _combat);
            _unlocks = new Unlocks(data.Unlocks, state);
            _artifacts.Unlocks = _unlocks;
            _jewels.Unlocks = _unlocks;
            _atlas.Unlocks = _unlocks;
            _resonance.Unlocks = _unlocks;
            _spirits.Unlocks = _unlocks;
            _chronicle.Unlocks = _unlocks;
            _drops.Unlocks = _unlocks;
            _drops.Dungeons = data.Dungeons;
            _dungeons = new DungeonSystem(data, state, hero, _stage, _drops, Events) { Unlocks = _unlocks };
            _stage.ChestGold = data.Atlas.ChestGold;
            _loot.JackpotMultiplier = data.Artifacts.JackpotMultiplier;
            _stage.Encounters = data.Combat.Encounters;
            _upgrades = new StrikeUpgradeSystem(data.Combat.StrikeUpgrade, state, hero);
            _prestige = new PrestigeSystem(data.Prestige, state, Events, _stage, _upgrades, hero);
            _book = new SkillBook(data.SkillBook, state, _skills, hero);
            _banner = new BannerSystem(state, hero, _combat, _skills);
            _paths = new PathSystem(state, hero, _combat, _resource, _skills);
            _skills.Levels = data.Gems.Levels;
            _gemExperience = new GemExperienceSystem(data.Gems.Levels, state, hero, _skills, Events);
            _treeLayer = new ModifierLayer(hero);
            _partyBuffs = new PartyBuffs(data.Party.Buffs, state, hero);
            _synergy = new SynergySystem(data.Synergy, state, hero);
            _ghost = new GhostSystem(data.Party.Ghost, state, _combat, _stage, _synergy);
            _rift = new RiftSystem(data, state, hero, _stage, _artifacts, _synergy);
            _raid = new RaidTally(data.Raid, state, _stage, _ghost, Events);
            _clanTree = new ClanTreeBonus(data.ClanTree, hero);
            RebuildTree();
            SyncPathSkill();
            _dps = new DpsMeter(data.Combat.Offline, data.Combat.Focus, state);
            _tutorial = new TutorialSystem(data.Tutorial, state, _upgrades);
            _stage.EnsureEnemy();
        }

        public StatSheet Hero { get; }
        public IReadOnlyList<Modifier> TreeModifiers => _treeLayer.Applied;
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
        public ChronicleSystem Chronicle => _chronicle;
        public AtlasSystem Atlas => _atlas;
        public ResonanceSystem Resonance => _resonance;
        public SpiritSystem Spirits => _spirits;
        public TutorialSystem Tutorial => _tutorial;
        public PartyBuffs PartyBuffs => _partyBuffs;
        public SynergySystem Synergy => _synergy;
        public GhostSystem Ghost => _ghost;
        public RaidTally Raid => _raid;
        public ClanTreeBonus ClanTree => _clanTree;
        public RiftSystem Rift => _rift;
        public WeaponSystem Weapons => _weapons;
        public GearSystem Gear => _gear;
        public JewelSystem Jewels => _jewels;
        public GateSystem Gates => _gates;
        public Unlocks Unlocks => _unlocks;
        public DungeonSystem Dungeons => _dungeons;
        public DropSystem Drops => _drops;
        public OverloadSystem Overload => _overload;
        public PathSystem Paths => _paths;
        public GemExperienceSystem GemExperience => _gemExperience;
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
            _drops.Flush();
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

        public BigNumber FullResetCost(long nowUnixMs)
        {
            if (State.Tree.FreeResetReady(_data.Tree, nowUnixMs))
                return BigNumber.Zero;
            var total = BigNumber.Zero;
            foreach (var id in State.Tree.Allocated)
            {
                var cost = RefundCost(id);
                if (cost != null)
                    total += cost.Value;
            }
            return total * BigNumber.FromDouble(_data.Tree.Respec.FullResetShare);
        }

        public RefundResult FullReset(long nowUnixMs)
        {
            if (State.Tree.PointsSpent == 0)
                return RefundResult.NotAllocated;
            if (State.Tree.FreeResetReady(_data.Tree, nowUnixMs))
                return FreeFullReset(nowUnixMs);
            var cost = FullResetCost(nowUnixMs);
            if (State.Gold < cost)
                return RefundResult.NotEnoughGold;
            State.Gold -= cost;
            State.Tree.Clear();
            RebuildTree();
            return RefundResult.Refunded;
        }

        public void RequestCast(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < MaxRequestedSlots)
                _castRequests |= 1UL << slotIndex;
        }

        public void SetLocalDay(long day) => _drops.SetDay(day);

        public void ApplyOffline(OfflineResult result)
        {
            _drops.ApplyOffline(result);
            OfflineCalculator.Apply(State, result);
            _stage.EnsureEnemy();
        }

        public bool RebirthGem(GemDef gem) => _book.Rebirth(gem, _data.Gems.Levels.Max);

        public bool BuyUncutGem() => _drops.BuyUncut();

        public CraftResult CraftSetPiece(string setId, Content.Items.ItemSlot? slot = null, string? weaponType = null) => _drops.CraftSetPiece(setId, slot, weaponType);

        public TrialResult StartTrial(string trialId) => _chronicle.Start(trialId);

        public bool AbandonTrial() => _chronicle.Abandon();

        public AtlasResult AllocateAtlasNode(string nodeId) => _atlas.Allocate(nodeId);

        public ResonanceResult RaiseResonance(string branchId) => _resonance.Raise(branchId);

        public SpiritResult RaiseSpirit(string spiritId) => _spirits.Raise(spiritId);

        public CutResult CutGem(GemDef gem) => _drops.Cut(gem);

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

        public bool RefundSubclassNode(string nodeId)
        {
            var removed = State.Ascendancy.Remove(_data.Ascendancy, nodeId);
            if (removed)
                RebuildTree();
            return removed;
        }

        private SubclassResult AfterSubclassChange(SubclassResult result)
        {
            if (result == SubclassResult.Chosen)
            {
                RebuildTree();
                SyncPathSkill();
            }
            return result;
        }

        private void SyncPathSkill()
        {
            var path = State.Ascendancy.Subclass;
            for (var i = State.Skills.Count - 1; i >= 0; i--)
            {
                var gem = State.Skills[i].Skill;
                if (gem.Ascendancy != null && gem.Ascendancy != path)
                    _skills.Unequip(i);
            }
            State.OwnedGems.RemoveAll(gem => gem.Ascendancy != null && gem.Ascendancy != path);
            var pathSkill = _data.Gems.ForPath(path);
            if (pathSkill != null && !State.OwnedGems.Contains(pathSkill))
                State.OwnedGems.Add(pathSkill);
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
            {
                current.Skills.Add((slot.Skill, slot.Trigger));
                current.Supports.Add((GemDef?[])slot.Supports.Clone());
            }

            var next = State.OtherPreset;
            next.Tree.PointsEarned = State.Tree.PointsEarned;
            while (State.Skills.Count > 0)
                _skills.Unequip(State.Skills.Count - 1);
            State.Tree = next.Tree;
            State.OtherPreset = current;
            State.ActivePreset = 1 - State.ActivePreset;
            RebuildTree();

            for (var i = 0; i < next.Skills.Count; i++)
            {
                var (skill, trigger) = next.Skills[i];
                if (_book.Equip(skill) != BookResult.Done)
                    continue;
                var index = State.Skills.Count - 1;
                State.Skills[index].StartCooldown((int)System.Math.Round(skill.Skill!.CooldownSeconds * TicksPerSecond));
                if (trigger != null)
                    _book.SetTrigger(index, trigger);
                var supports = next.SupportsAt(i);
                for (var socket = 0; socket < supports.Length; socket++)
                {
                    if (supports[socket] != null)
                        _book.SetSupport(index, socket, supports[socket]);
                }
            }
        }

        public void RebuildTree()
        {
            State.Tree.DropUnreachable(_data.Tree);
            State.OtherPreset.Tree.DropUnreachable(_data.Tree);
            var modifiers = new List<Modifier>(State.Tree.Modifiers(_data.Tree));
            modifiers.AddRange(State.Ascendancy.Modifiers(_data.Ascendancy));
            _treeLayer.Set(modifiers);
            _jewels.Rebuild();
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
            _skills.Levels = data.Gems.Levels;
            _gemExperience.UseLevels(data.Gems.Levels);
            _weapons.UseCatalog(data.Weapons);
            _drops.UseData(data.Drops, data.Stages, data.Gems, data.Items);
            _gear.UseCatalog(data.Items);
            _jewels.Tree = data.Tree;
            _gates.UseStages(data.Stages);
            _unlocks.Rules = data.Unlocks;
            _drops.Dungeons = data.Dungeons;
            _dungeons.UseData(data);
            _skills.UseWeapons(data.Weapons);
            _book.UseRules(data.SkillBook);
            _prestige.UseRules(data.Prestige);
            _upgrades.UseRules(data.Combat.StrikeUpgrade);
            _artifacts.UseCatalog(data.Artifacts, data.Rift);
            _chronicle.UseCatalog(data.Chronicle);
            _atlas.UseCatalog(data.Atlas);
            _resonance.UseCatalog(data.Resonance);
            _spirits.UseCatalog(data.Spirits);
            _stage.ChestGold = data.Atlas.ChestGold;
            _loot.JackpotMultiplier = data.Artifacts.JackpotMultiplier;
            _stage.Encounters = data.Combat.Encounters;
            _tutorial.UseRules(data.Tutorial);
            _partyBuffs.UseRules(data.Party.Buffs);
            _synergy.UseRules(data.Synergy);
            _ghost.UseRules(data.Party.Ghost);
            _raid.UseRules(data.Raid);
            _clanTree.UseCatalog(data.ClanTree);
            _rift.UseData(data);
            RebuildTree();
            SyncPathSkill();
        }

        public void MarkStarted(long nowUnixMs)
        {
            if (State.StartedUnixMs <= 0)
                State.StartedUnixMs = nowUnixMs;
        }

        public void Tick()
        {
            Hook?.BeforeTick();
            var firstEvent = Events.Count;
            LastInput = Permissions.Apply(_input.Drain());
            SessionInput.Add(LastInput, MetersPerPixel);
            State.TotalInput.Add(LastInput, MetersPerPixel);
            SessionInput.PlaySeconds += TickSeconds;
            State.TotalInput.PlaySeconds += TickSeconds;
            var raw = _inputRules.Apply(LastInput);
            var input = raw.WithMouse(raw.MousePx * System.Math.Max(0, Hero.Value(CombatStats.MouseDistance, 1, StatTags.Mouse)));
            var idleTicksBefore = State.IdleTicks;
            State.IdleTicksBeforeInput = idleTicksBefore;
            _focus.Tick(input);
            _combat.NotePause(idleTicksBefore, input.HasInput);
            _resource.Tick(input);
            _banner.Tick(input);
            _skills.Tick(input, idleTicksBefore);
            _skills.CastFromButtons(input);
            CastRequested();
            _triggers.Tick(input);
            _partyBuffs.Tick();
            _ghost.NoteInput(input.HasInput);
            _ghost.Tick();
            _synergy.Tick();
            _rift.Tick();
            _stage.Tick();
            _dungeons.Tick();
            _chronicle.Tick();
            _combat.Tick(input);
            _skills.Minions.Tick(input);
            _skills.Dots.Tick(input);
            _overload.Tick();
            _paths.Tick(input);
            _spirits.Tick(input);
            _gemExperience.Tick();
            _raid.Tick(firstEvent);
            _dps.Tick(_combat.LastTickDamage);
            State.DamageWindow.Record(State.TotalDamage);
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
