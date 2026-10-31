using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
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
        private readonly SynergyStrikes _synergyStrikes;
        private readonly GhostSystem _ghost;
        private readonly RaidTally _raid;
        private readonly ClanTreeBonus _clanTree;
        private readonly MentorBonus _mentor;
        private readonly RiftSystem _rift;
        private readonly WeaponSystem _weapons;
        private readonly DropSystem _drops;
        private readonly OverloadSystem _overload;
        private readonly BannerSystem _banner;
        private readonly OverdriveSystem _overdrive;
        private readonly StandingAuraSystem _standingAuras;
        private readonly PathSystem _paths;
        private readonly GemExperienceSystem _gemExperience;
        private readonly GearSystem _gear;
        private readonly GateSystem _gates;
        private readonly Unlocks _unlocks;
        private readonly DungeonSystem _dungeons;
        private readonly SlateSystem _slates;
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
            _levels = new LevelSystem(data.Combat.Hero, data.Stages, data.TreePoints, state, Events) { CommonSlots = data.TreeTemplates.CommonSlots };
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
            _gates = new GateSystem(data.Stages, state, hero, Events);
            _stage.Gates = _gates;
            _chronicle = new ChronicleSystem(data.Chronicle, state, hero, Events, _levels);
            _atlas = new AtlasSystem(data.Atlas, state, hero);
            _resonance = new ResonanceSystem(data.Resonance, state, hero);
            _spirits = new SpiritSystem(data.Spirits, state, _combat);
            _unlocks = new Unlocks(data.Unlocks, state);
            _artifacts.Unlocks = _unlocks;
            _atlas.Unlocks = _unlocks;
            _resonance.Unlocks = _unlocks;
            _spirits.Unlocks = _unlocks;
            _chronicle.Unlocks = _unlocks;
            _drops.Unlocks = _unlocks;
            _slates = new SlateSystem(data.Slates, state, hero, Events) { Unlocks = _unlocks };
            _drops.Slates = _slates;
            _artifacts.Rebuilt = _slates.Rebuild;
            _drops.Dungeons = data.Dungeons;
            _dungeons = new DungeonSystem(data, state, hero, _stage, _drops, Events) { Unlocks = _unlocks };
            _stage.ChestGold = data.Atlas.ChestGold;
            _loot.JackpotMultiplier = data.Artifacts.JackpotMultiplier;
            _stage.Encounters = data.Combat.Encounters;
            _upgrades = new StrikeUpgradeSystem(data.Combat.StrikeUpgrade, state, hero);
            _prestige = new PrestigeSystem(data.Prestige, state, Events, _stage, _upgrades, hero);
            _book = new SkillBook(data.SkillBook, state, _skills, hero);
            _banner = new BannerSystem(state, hero, _combat, _skills);
            _overdrive = new OverdriveSystem(state, hero, _combat, _skills);
            _standingAuras = new StandingAuraSystem(state, hero, _combat, _skills);
            _paths = new PathSystem(state, hero, _combat, _resource, _skills);
            _skills.Levels = data.Gems.Levels;
            _gemExperience = new GemExperienceSystem(data.Gems.Levels, state, hero, _skills, Events);
            _treeLayer = new ModifierLayer(hero);
            _partyBuffs = new PartyBuffs(data.Party.Buffs, state, hero);
            _synergy = new SynergySystem(data.Synergy, state, hero);
            _synergyStrikes = new SynergyStrikes(state, hero, _synergy, _skills.Dots, _resource);
            _ghost = new GhostSystem(data.Party.Ghost, state, _combat, _stage, _synergy);
            _rift = new RiftSystem(data, state, hero, _stage, _artifacts, _synergy) { Slates = _slates };
            _raid = new RaidTally(data.Raid, state, _stage, _ghost, Events, hero);
            _clanTree = new ClanTreeBonus(data.ClanTree, hero, state.ClanBonus);
            _mentor = new MentorBonus(data.Mentor, hero, _levels, state);
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
        public SynergyStrikes SynergyStrikes => _synergyStrikes;
        public GhostSystem Ghost => _ghost;
        public RaidTally Raid => _raid;
        public ClanTreeBonus ClanTree => _clanTree;
        public MentorBonus Mentor => _mentor;
        public RiftSystem Rift => _rift;
        public WeaponSystem Weapons => _weapons;
        public GearSystem Gear => _gear;
        public GateSystem Gates => _gates;
        public Unlocks Unlocks => _unlocks;
        public DungeonSystem Dungeons => _dungeons;
        public SlateSystem Slates => _slates;
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

        public bool ClassChoiceReady => !State.ClassChosen && State.HeroLevel >= _data.TreePoints.ClassChoiceLevel;

        public ClassChoiceResult ChooseClass(HeroClass heroClass)
        {
            if (State.ClassChosen)
                return ClassChoiceResult.AlreadyChosen;
            if (!ClassChoiceReady)
                return ClassChoiceResult.NotYet;
            State.Class = heroClass;
            State.ClassChosen = true;
            State.ClassTree = new ClassTreeAllocation(State.ClassName);
            State.OtherPreset = new BuildPreset(new ClassTreeAllocation(State.ClassName));
            State.Resource = 0;
            _levels.SyncTreePoints();
            _weapons.GiveClassStarter();
            _book.GrantClassGems();
            RebuildTree();
            return ClassChoiceResult.Chosen;
        }

        public bool ClassChangeIsFree => State.ClassChanges < _data.Ascendancy.FreeClassChanges;

        public ClassChangeResult ChangeClassFree(HeroClass heroClass) =>
            ClassChangeIsFree ? ChangeClass(heroClass) : ClassChangeResult.NotFree;

        public ClassChangeResult ChangeClassPaid(HeroClass heroClass) => ChangeClass(heroClass);

        private ClassChangeResult ChangeClass(HeroClass heroClass)
        {
            if (!State.ClassChosen)
                return ClassChangeResult.NoClassYet;
            if (State.Class == heroClass)
                return ClassChangeResult.SameClass;
            var from = _data.Weapons.TypeOf(State.Class);
            var to = _data.Weapons.TypeOf(heroClass);
            var reforge = new GemReforge(_data.Weapons.Reforge, from, to);
            var equipped = TakeOffSkills();
            State.Class = heroClass;
            State.ClassChanges++;
            reforge.Apply(State, _data.Gems);
            _weapons.Reforge(from, to);
            State.ClassTree = new ClassTreeAllocation(State.ClassName);
            State.OtherPreset = reforge.Preset(State.OtherPreset, new ClassTreeAllocation(State.ClassName));
            State.Ascendancy.Reset();
            State.Resource = 0;
            _levels.SyncTreePoints();
            RebuildTree();
            SyncPathSkill();
            PutOnSkills(equipped, reforge);
            _skills.RefreshArmed();
            return ClassChangeResult.Changed;
        }

        private List<(GemDef Skill, GemDef? Trigger, GemDef?[] Supports)> TakeOffSkills()
        {
            var equipped = new List<(GemDef, GemDef?, GemDef?[])>(State.Skills.Count);
            foreach (var slot in State.Skills)
                equipped.Add((slot.Skill, slot.Trigger, (GemDef?[])slot.Supports.Clone()));
            while (State.Skills.Count > 0)
                _skills.Unequip(State.Skills.Count - 1);
            return equipped;
        }

        private void PutOnSkills(List<(GemDef Skill, GemDef? Trigger, GemDef?[] Supports)> equipped, GemReforge reforge)
        {
            foreach (var (skill, trigger, supports) in equipped)
            {
                if (skill.Ascendancy != null)
                    continue;
                var slot = State.Skills[_skills.Equip(reforge.Of(skill), trigger)];
                slot.StartCooldown((int)System.Math.Round(slot.Skill.Skill!.CooldownSeconds * TicksPerSecond));
                supports.CopyTo(slot.Supports, 0);
            }
        }

        public ClassTree? ClassTreeDef => State.ClassChosen ? _data.ClassTreeOf(State.ClassName) : null;

        public bool ClassTreeResetFree => State.GatesCleared.Count == 0;

        public IReadOnlyList<CommonTreeSlotRule> CommonTreeSlots => _data.TreeTemplates.CommonSlots;

        public bool CommonTreeSlotOpen(int slot) => slot >= 0 && slot < CommonTreeSlots.Count && CommonTreeSlots[slot].OpenAt(State.HeroLevel);

        public ClassTree? CommonTreeDef(int slot) =>
            slot >= 0 && slot < State.CommonTrees.Count && State.CommonTrees[slot] is { } allocation ? _data.CommonTreeOf(allocation.TreeId) : null;

        public ClassTreeAllocation? CommonTreeAllocation(int slot) => CommonTreeDef(slot) != null ? State.CommonTrees[slot] : null;

        public BigNumber CommonTreeChangeCost(int slot) =>
            CommonTreeDef(slot) is { } tree ? ResetCost(tree, State.CommonTrees[slot]!) : BigNumber.Zero;

        public CommonTreeResult ChooseCommonTree(int slot, string treeId)
        {
            if (!CommonTreeSlotOpen(slot))
                return CommonTreeResult.SlotClosed;
            if (_data.CommonTreeOf(treeId) == null)
                return CommonTreeResult.UnknownTree;
            for (var other = 0; other < State.CommonTrees.Count; other++)
            {
                if (State.CommonTrees[other]?.TreeId == treeId)
                    return other == slot ? CommonTreeResult.SameTree : CommonTreeResult.TakenInOtherSlot;
            }
            var cost = CommonTreeChangeCost(slot);
            if (State.Gold < cost)
                return CommonTreeResult.NotEnoughGold;
            State.Gold -= cost;
            State.CommonTrees[slot] = new ClassTreeAllocation(treeId) { PointsEarned = CommonTreeSlots[slot].PointsAt(State.HeroLevel) };
            RebuildTree();
            return CommonTreeResult.Chosen;
        }

        private (ClassTree Tree, ClassTreeAllocation Allocation)? TreeOfNode(string nodeId)
        {
            if (ClassTreeDef is { } classTree && classTree.TryGet(nodeId, out _))
                return (classTree, State.ClassTree);
            for (var slot = 0; slot < State.CommonTrees.Count; slot++)
            {
                if (CommonTreeDef(slot) is { } tree && tree.TryGet(nodeId, out _))
                    return (tree, State.CommonTrees[slot]!);
            }
            return null;
        }

        private (ClassTree Tree, ClassTreeAllocation Allocation)? TreeOfMajor(string talentId)
        {
            if (ClassTreeDef is { } classTree && classTree.OffersMajor(talentId))
                return (classTree, State.ClassTree);
            for (var slot = 0; slot < State.CommonTrees.Count; slot++)
            {
                if (CommonTreeDef(slot) is { } tree && tree.OffersMajor(talentId))
                    return (tree, State.CommonTrees[slot]!);
            }
            return null;
        }

        public RankResult RaiseNode(string nodeId)
        {
            if (!State.ClassChosen)
                return RankResult.ChooseClassFirst;
            if (TreeOfNode(nodeId) is not { } found)
                return RankResult.UnknownNode;
            var result = found.Allocation.Raise(found.Tree, nodeId);
            if (result == RankResult.Done)
                RebuildTree();
            return result;
        }

        public BigNumber? LowerCost(string nodeId)
        {
            if (TreeOfNode(nodeId) is not { } found || found.Allocation.RankOf(nodeId) == 0)
                return null;
            return ClassTreeResetFree ? BigNumber.Zero : found.Tree.Respec.NodeCost(found.Tree[nodeId].Row, State.HeroLevel, false);
        }

        public RefundResult LowerNode(string nodeId)
        {
            var cost = LowerCost(nodeId);
            if (TreeOfNode(nodeId) is not { } found || cost == null)
                return RefundResult.NotAllocated;
            if (found.Allocation.CheckLower(found.Tree, nodeId) != RankResult.Done)
                return RefundResult.BreaksPath;
            if (State.Gold < cost.Value)
                return RefundResult.NotEnoughGold;
            State.Gold -= cost.Value;
            found.Allocation.Lower(found.Tree, nodeId);
            RebuildTree();
            return RefundResult.Refunded;
        }

        private BigNumber ResetCost(ClassTree tree, ClassTreeAllocation allocation)
        {
            if (ClassTreeResetFree)
                return BigNumber.Zero;
            var total = BigNumber.Zero;
            foreach (var pair in allocation.Ranks)
            {
                if (tree.TryGet(pair.Key, out var node))
                    total += tree.Respec.NodeCost(node.Row, State.HeroLevel, false) * BigNumber.FromDouble(pair.Value);
            }
            return total * BigNumber.FromDouble(tree.Respec.FullResetShare);
        }

        public BigNumber ClassTreeResetCost() => ClassTreeDef is { } tree ? ResetCost(tree, State.ClassTree) : BigNumber.Zero;

        public RefundResult ResetClassTree() => ClassTreeDef is { } tree ? Reset(tree, State.ClassTree) : RefundResult.NotAllocated;

        public BigNumber CommonTreeResetCost(int slot) => CommonTreeChangeCost(slot);

        public RefundResult ResetCommonTree(int slot) => CommonTreeDef(slot) is { } tree ? Reset(tree, State.CommonTrees[slot]!) : RefundResult.NotAllocated;

        private RefundResult Reset(ClassTree tree, ClassTreeAllocation allocation)
        {
            if (allocation.PointsSpent == 0)
                return RefundResult.NotAllocated;
            var cost = ResetCost(tree, allocation);
            if (State.Gold < cost)
                return RefundResult.NotEnoughGold;
            State.Gold -= cost;
            allocation.Clear();
            RebuildTree();
            return RefundResult.Refunded;
        }

        public int FillTrees()
        {
            var raised = ClassTreeDef is { } classTree ? State.ClassTree.FillFromPlan(classTree) : 0;
            for (var slot = 0; slot < State.CommonTrees.Count; slot++)
            {
                if (CommonTreeDef(slot) is { } tree)
                    raised += State.CommonTrees[slot]!.FillFromPlan(tree);
            }
            if (raised > 0)
                RebuildTree();
            return raised;
        }

        public RankResult ChooseMajorTalent(int at, string talentId)
        {
            if (TreeOfMajor(talentId) is not { } found)
                return RankResult.UnknownMajor;
            if (State.FightingBoss && found.Allocation.Majors.TryGetValue(at, out var chosen) && chosen != talentId)
                return RankResult.InBossFight;
            var result = found.Allocation.ChooseMajor(found.Tree, at, talentId);
            if (result == RankResult.Done)
                RebuildTree();
            return result;
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
            AddIdleSeconds(result.AwaySeconds);
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
            AfterSubclassChange(State.Ascendancy.Choose(_data.Ascendancy, State.ClassName, subclassId));

        public SubclassResult ChangeSubclassFree(string subclassId, long nowUnixMs) =>
            AfterSubclassChange(State.Ascendancy.ChangeFree(_data.Ascendancy, State.ClassName, subclassId, nowUnixMs));

        public SubclassResult ChangeSubclassPaid(string subclassId) =>
            AfterSubclassChange(State.Ascendancy.ChangePaid(_data.Ascendancy, State.ClassName, subclassId));

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

        public void SwitchPreset()
        {
            var current = new BuildPreset(State.ClassTree) { CommonTrees = State.CommonTrees };
            foreach (var slot in State.Skills)
            {
                current.Skills.Add((slot.Skill, slot.Trigger));
                current.Supports.Add((GemDef?[])slot.Supports.Clone());
            }

            var next = State.OtherPreset;
            next.ClassTree.PointsEarned = State.TreePointsEarned;
            while (State.Skills.Count > 0)
                _skills.Unequip(State.Skills.Count - 1);
            State.ClassTree = next.ClassTree;
            State.CommonTrees = next.CommonTrees;
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
            var modifiers = ClassTreeDef is { } tree ? ClassTreeModifiers(tree) : new List<Modifier>();
            State.ClassTreeActive = ClassTreeDef != null;
            modifiers.AddRange(CommonTreeModifiers());
            modifiers.AddRange(State.Ascendancy.Modifiers(_data.Ascendancy));
            for (var i = 0; i < modifiers.Count; i++)
                modifiers[i] = modifiers[i].InPool(ModifierPool.Tree);
            _treeLayer.Set(modifiers);
            _slates.Rebuild();
            _skills.RefreshAuras();
            _partyBuffs.Rebuild();
        }

        private List<Modifier> ClassTreeModifiers(ClassTree tree)
        {
            State.ClassTree = Fitted(State.ClassTree, tree);
            State.OtherPreset.ClassTree = Fitted(State.OtherPreset.ClassTree, tree);
            return State.ClassTree.Modifiers(tree);
        }

        private List<Modifier> CommonTreeModifiers()
        {
            State.CommonTrees = FittedCommon(State.CommonTrees);
            State.OtherPreset.CommonTrees = FittedCommon(State.OtherPreset.CommonTrees);
            var modifiers = new List<Modifier>();
            for (var slot = 0; slot < State.CommonTrees.Count; slot++)
            {
                if (CommonTreeDef(slot) is { } tree)
                    modifiers.AddRange(State.CommonTrees[slot]!.Modifiers(tree));
            }
            return modifiers;
        }

        private List<ClassTreeAllocation?> FittedCommon(List<ClassTreeAllocation?> slots)
        {
            var rules = CommonTreeSlots;
            var fitted = new List<ClassTreeAllocation?>(rules.Count);
            for (var slot = 0; slot < rules.Count; slot++)
            {
                var allocation = slot < slots.Count ? slots[slot] : null;
                var tree = allocation == null ? null : _data.CommonTreeOf(allocation.TreeId);
                if (tree == null || !rules[slot].OpenAt(State.HeroLevel) || fitted.Exists(other => other?.TreeId == allocation!.TreeId))
                {
                    fitted.Add(null);
                    continue;
                }
                allocation!.PointsEarned = rules[slot].PointsAt(State.HeroLevel);
                allocation.DropInvalid(tree);
                if (allocation.PointsSpent > allocation.PointsEarned)
                    allocation.Clear();
                fitted.Add(allocation);
            }
            return fitted;
        }

        private ClassTreeAllocation Fitted(ClassTreeAllocation allocation, ClassTree tree)
        {
            if (allocation.TreeId != tree.Id)
                allocation = new ClassTreeAllocation(tree.Id);
            allocation.PointsEarned = State.TreePointsEarned;
            allocation.DropInvalid(tree);
            if (allocation.PointsSpent > allocation.PointsEarned)
                allocation.Clear();
            return allocation;
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
            _levels.CommonSlots = data.TreeTemplates.CommonSlots;
            _dps.UseRules(data.Combat.Offline, data.Combat.Focus);
            _skills.UseGems(data.Gems);
            _skills.Levels = data.Gems.Levels;
            _gemExperience.UseLevels(data.Gems.Levels);
            _weapons.UseCatalog(data.Weapons);
            _drops.UseData(data.Drops, data.Stages, data.Gems, data.Items);
            _gear.UseCatalog(data.Items);
            _gates.UseStages(data.Stages);
            _unlocks.Rules = data.Unlocks;
            _drops.Dungeons = data.Dungeons;
            _slates.Rules = data.Slates;
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
            _mentor.UseRules(data.Mentor);
            _rift.UseData(data);
            RebuildTree();
            SyncPathSkill();
        }

        private void CountIdle()
        {
            if (State.IdleTicks * TickSeconds < Data.TreePoints.IdleAfterSeconds)
                return;
            SessionInput.IdleSeconds += TickSeconds;
            AddIdleSeconds(TickSeconds);
        }

        private void AddIdleSeconds(double seconds)
        {
            if (seconds <= 0)
                return;
            var hoursBefore = System.Math.Floor(State.TotalInput.IdleSeconds / 3600);
            State.TotalInput.IdleSeconds += seconds;
            if (System.Math.Floor(State.TotalInput.IdleSeconds / 3600) > hoursBefore)
                _levels.SyncTreePoints();
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
            CountIdle();
            _combat.NotePause(idleTicksBefore, input.HasInput);
            _resource.Tick(input);
            _banner.Tick(input);
            _overdrive.Tick();
            _standingAuras.Tick();
            _skills.Tick(input, idleTicksBefore);
            _skills.CastFromButtons(input);
            CastRequested();
            _triggers.Tick(input);
            _partyBuffs.Tick();
            _ghost.NoteInput(input.HasInput);
            _ghost.Tick();
            _synergy.Tick();
            _synergyStrikes.Tick();
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
