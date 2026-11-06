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
        private readonly MentorBonus _mentor;
        private readonly RiftSystem _rift;
        private readonly WeaponSystem _weapons;
        private readonly DropSystem _drops;
        private readonly QuestSystem _quests;
        private readonly AchievementSystem _achievements;
        private readonly BestiarySystem _bestiary;
        private readonly CosmeticSystem _cosmetics;
        private readonly SeasonPassSystem _seasonPass;
        private readonly OverloadSystem _overload;
        private readonly PathSystem _paths;
        private readonly GemExperienceSystem _gemExperience;
        private readonly GearSystem _gear;
        private readonly RerollSystem _reroll;
        private readonly ClassAttributeSystem _classAttribute;
        private readonly GateSystem _gates;
        private readonly Unlocks _unlocks;
        private readonly DungeonSystem _dungeons;
        private readonly EventSystem _seasonal;
        private readonly PeacefulSystem _peaceful;
        private readonly CampSystem _camp;
        private readonly GardenSystem _garden;
        private readonly AlchemySystem _alchemy;
        private readonly ExpeditionSystem _expeditions;
        private readonly TownSystem _town;
        private readonly FishingSystem _fishing;
        private readonly CookingSystem _cooking;
        private readonly ForgeSystem _forge;
        private readonly MuseumSystem _museum;
        private readonly DecorSystem _decor;
        private readonly SlateSystem _slates;
        private readonly ConditionSystem _conditions;
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
            _weapons = new WeaponSystem(data.Weapons, state, hero);
            _gear = new GearSystem(data.Items, state, hero);
            _classAttribute = new ClassAttributeSystem(hero);
            _gear.Rebuilt = () =>
            {
                _weapons.RefreshInnate();
                RefreshClassAttribute();
            };
            _weapons.RefreshInnate();
            _drops = new DropSystem(data.Drops, data.Stages, data.Items, _weapons, _gear, state, hero, Events);
            _quests = new QuestSystem(data.Quests, state, Events);
            _achievements = new AchievementSystem(data.Achievements, state, Events);
            _bestiary = new BestiarySystem(data.Bestiary, state, Events);
            _cosmetics = new CosmeticSystem(data.Cosmetics, state);
            _seasonPass = new SeasonPassSystem(data.SeasonPass, state);
            _stage.Drops = _drops;
            _reroll = new RerollSystem(data.Drops, state, _gear, _weapons);
            _combat.Dreamcatcher.UseLoot(_drops, _loot);
            _combat.Dreamcatcher.UseEnemies(data.Enemies);
            _combat.Dowser.UseLoot(_drops, _loot);
            _stage.RewardListener = new KillRewardListeners(_combat.Dreamcatcher, _combat.Dowser, _combat.Soulbinder, _combat.Ghostcaptain, _combat.Mothcaller, _combat.Cartomancer, _combat.Herbwitch);
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
            _seasonal = new EventSystem(data, state, _stage, Events);
            _peaceful = new PeacefulSystem(data.Peaceful, state, Events);
            _camp = new CampSystem(data.Camp, state, Events);
            _garden = new GardenSystem(data.Garden, state, _camp, _peaceful, Events);
            _alchemy = new AlchemySystem(data.Alchemy, state, hero, _camp, _peaceful, Events);
            _expeditions = new ExpeditionSystem(data.Expeditions, state, data.Stages, data.Cosmetics, _camp, Events);
            _cooking = new CookingSystem(data.Cooking, state, hero, _camp, _peaceful, Events);
            _forge = new ForgeSystem(data.Forge, state, data.Stages, _camp, _peaceful, _weapons, _gear, Events);
            _drops.Salvaged = _forge.SalvageOre;
            _seasonal.BossWon = eventId => _drops.GiveEventPiece(eventId);
            _museum = new MuseumSystem(data.Museum, state, hero, _alchemy, Events);
            _decor = new DecorSystem(data.Decor, state, _camp, _garden, _forge, _expeditions);
            _fishing = new FishingSystem(data.Fishing, state, _camp, _peaceful, _expeditions, Events);
            _town = new TownSystem(data.Town, state, _camp, _alchemy, _expeditions, Events);
            _stage.ChestGold = data.Atlas.ChestGold;
            _loot.JackpotMultiplier = data.Artifacts.JackpotMultiplier;
            _stage.Encounters = data.Combat.Encounters;
            _upgrades = new StrikeUpgradeSystem(data.Combat.StrikeUpgrade, state, hero);
            _prestige = new PrestigeSystem(data.Prestige, state, Events, _stage, _upgrades, hero);
            _book = new SkillBook(data.SkillBook, data.Weapons, state, _skills, hero);
            if (state.LegacyGems)
            {
                _book.ForgetLegacyGems();
                state.LegacyGems = false;
            }
            _weapons.HeldChanged = _book.KeepOpen;
            _paths = new PathSystem(state, hero, _combat, _resource, _skills);
            _skills.Levels = data.Gems.Levels;
            _gemExperience = new GemExperienceSystem(data.Gems.Levels, state, hero, _skills, Events);
            _skills.RefreshBranches();
            _treeLayer = new ModifierLayer(hero);
            _partyBuffs = new PartyBuffs(data.Party.Buffs, state, hero);
            _synergy = new SynergySystem(data.Synergy, state, hero);
            _conditions = new ConditionSystem(state, hero, _combat, _skills, _focus) { Stages = data.Stages, Forge = data.Forge };
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

        public IReadOnlyList<Modifier> ClassAttributeModifiers => _classAttribute.Modifiers;
        public CombatSystem Combat => _combat;
        public StageSystem Stages => _stage;
        public FocusSystem Focus => _focus;
        public ResourceSystem Resource => _resource;
        public ConditionSystem Conditions => _conditions;
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
        public MentorBonus Mentor => _mentor;
        public RiftSystem Rift => _rift;
        public WeaponSystem Weapons => _weapons;
        public GearSystem Gear => _gear;
        public RerollSystem Reroll => _reroll;
        public GateSystem Gates => _gates;
        public Unlocks Unlocks => _unlocks;
        public DungeonSystem Dungeons => _dungeons;
        public EventSystem SeasonalEvents => _seasonal;
        public PeacefulSystem Peaceful => _peaceful;
        public CampSystem Camp => _camp;
        public GardenSystem Garden => _garden;
        public AlchemySystem Alchemy => _alchemy;
        public ExpeditionSystem Expeditions => _expeditions;
        public TownSystem Town => _town;
        public FishingSystem Fishing => _fishing;
        public CookingSystem Cooking => _cooking;
        public ForgeSystem Forge => _forge;
        public MuseumSystem Museum => _museum;
        public DecorSystem Decor => _decor;
        public SlateSystem Slates => _slates;
        public DropSystem Drops => _drops;
        public QuestSystem Quests => _quests;
        public AchievementSystem Achievements => _achievements;
        public BestiarySystem Bestiary => _bestiary;
        public CosmeticSystem Cosmetics => _cosmetics;
        public SeasonPassSystem SeasonPass => _seasonPass;
        public OverloadSystem Overload => _overload;
        public PathSystem Paths => _paths;
        public GemExperienceSystem GemExperience => _gemExperience;
        public GameData Data => _data;
        public BossHint? BossHint => BossHint.For(State, _data.Stages.BossHint, _prestige, _upgrades, UnusedLevers());

        private BossHintOptions UnusedLevers()
        {
            var levers = BossHintOptions.None;
            if (State.TreePointsLeft > 0 || HasCommonTreePoints())
                levers |= BossHintOptions.TreePoints;
            if (State.Ascendancy.ChoicesWaiting(_data.Ascendancy, State.HeroLevel) > 0)
                levers |= BossHintOptions.PathChoice;
            if (HasEmptyGearSlotWithItem())
                levers |= BossHintOptions.EmptyGearSlot;
            if (HasEmptySocketWithSupport())
                levers |= BossHintOptions.EmptySocket;
            return levers;
        }

        private bool HasCommonTreePoints()
        {
            for (var slot = 0; slot < CommonTreeSlots.Count; slot++)
            {
                if (CommonTreeSlotOpen(slot) && (CommonTreeAllocation(slot)?.PointsLeft ?? 1) > 0)
                    return true;
            }
            return false;
        }

        private bool HasEmptyGearSlotWithItem()
        {
            foreach (var item in _gear.Bag)
            {
                if (item.Wearable && _gear.In(_gear.FreeSlotFor(item)) == null)
                    return true;
            }
            return false;
        }

        private readonly System.Collections.Generic.List<Content.Gems.GemDef> _hintSupports = new System.Collections.Generic.List<Content.Gems.GemDef>();

        private bool HasEmptySocketWithSupport()
        {
            var fitting = _hintSupports;
            for (var index = 0; index < State.Skills.Count; index++)
            {
                _book.SupportsFor(index, _data.Gems.All, fitting);
                for (var socket = 0; socket < _book.SocketsAt(index); socket++)
                {
                    if (State.Skills[index].Supports[socket] == null && fitting.Exists(support => !SkillBook.OnSkill(State.Skills[index], support)))
                        return true;
                }
            }
            return false;
        }

        public GameState State { get; }
        public EventQueue Events { get; } = new EventQueue(EventCapacity);

        public const int EventCapacity = 4096;
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
            return ClassChangeResult.Changed;
        }

        private List<(GemDef Skill, GemDef?[] Supports, SkillSlotRole Role)> TakeOffSkills()
        {
            var equipped = new List<(GemDef, GemDef?[], SkillSlotRole)>(State.Skills.Count);
            foreach (var slot in State.Skills)
                equipped.Add((slot.Skill, (GemDef?[])slot.Supports.Clone(), slot.Role));
            while (State.Skills.Count > 0)
                _skills.Unequip(State.Skills.Count - 1);
            return equipped;
        }

        private void PutOnSkills(List<(GemDef Skill, GemDef?[] Supports, SkillSlotRole Role)> equipped, GemReforge reforge)
        {
            foreach (var (skill, supports, role) in equipped)
            {
                if (skill.Ascendancy != null)
                    continue;
                var reforged = reforge.Of(skill);
                var slot = State.Skills[_skills.Equip(reforged, SkillLoadout.Accepts(role, reforged) ? role : (SkillSlotRole?)null)];
                slot.StartCooldown((int)System.Math.Round(slot.Skill.Skill!.CooldownSeconds * TicksPerSecond));
                supports.CopyTo(slot.Supports, 0);
            }
        }

        public ClassTree? ClassTreeDef => State.ClassChosen ? _data.ClassTreeOf(State.ClassName) : null;

        public double ClassAttribute => _classAttribute.Points;

        public ClassAttributeDef ClassAttributeDef => ClassTreeDef?.Attribute ?? ClassAttributeDef.None;

        public void RefreshClassAttribute() => _classAttribute.Refresh(ClassAttributeDef);

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

        public BigNumber BranchChangeCost(GemDef skill, int tier)
        {
            var chosen = State.GemProgress.TryGetValue(skill.Id, out var progress) && tier >= 0 && tier < progress.Branches.Length
                ? progress.Branches[tier]
                : SkillBranches.NoChoice;
            if (chosen == SkillBranches.NoChoice || ClassTreeResetFree || ClassTreeDef is not { } tree)
                return BigNumber.Zero;
            return tree.Respec.NodeCost(tier + 1, State.HeroLevel, false) * BigNumber.FromDouble(System.Math.Max(0, Hero.Value(CombatStats.BranchChangeCost, 1)));
        }

        public BranchResult ChooseBranch(GemDef skill, int tier, int option)
        {
            if (tier < 0 || tier >= skill.Branches.Count || option < 0 || option >= skill.Branches[tier].Options.Count)
                return BranchResult.NoSuchBranch;
            var progress = State.ProgressOf(skill);
            if (progress.Level < skill.Branches[tier].Level)
                return BranchResult.LevelTooLow;
            if (progress.Branches[tier] == option)
                return BranchResult.Done;
            var row = SkillLoadout.RowOf(State.Skills, skill);
            if (row != SkillLoadout.NoSlot && State.Skills[row].Active)
                return BranchResult.SkillBusy;
            var cost = BranchChangeCost(skill, tier);
            if (State.Gold < cost)
                return BranchResult.NotEnoughGold;
            State.Gold -= cost;
            progress.Branches[tier] = option;
            _skills.RefreshBranches();
            return BranchResult.Done;
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

        private int _museumDone = -1;

        private void RebuildOnMuseum()
        {
            var done = State.Museum.Done.Count;
            if (done == _museumDone)
                return;
            _museumDone = done;
            _gear.Rebuild();
        }

        public void SetServerTime(long utcUnixMs) => State.ServerHour = _data.Combat.Conditions.ServerHour(utcUnixMs);

        public static int FateOf(long startedUnixMs, long day) => FateOf(startedUnixMs, day, 3);

        public static int FateOf(long startedUnixMs, long day, int options)
        {
            var mixed = (ulong)startedUnixMs * 0x9E3779B97F4A7C15UL ^ (ulong)day * 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 31;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 29;
            return (int)(mixed % (ulong)System.Math.Max(1, options));
        }

        public int FateOn(long day) => FateOf(State.StartedUnixMs, day, State.FateOptions);

        public void SetLocalDay(long day)
        {
            State.Fate = FateOn(day);
            _drops.SetDay(day);
            _quests.SetDay(day);
            _chronicle.SetDay(day);
            _seasonal.SetDay(day);
            _town.SetDay(day);
        }

        public QuestResult ClaimQuest(bool weekly, int index)
        {
            var result = _quests.Claim(weekly, index);
            if (result == QuestResult.Done && !weekly)
                _seasonal.NoteDailyQuest();
            return result;
        }

        public QuestResult SwapQuest(int index) => _quests.Swap(index);

        public void ApplyOffline(OfflineResult result)
        {
            _drops.ApplyOffline(result);
            OfflineCalculator.Apply(State, result);
            AddIdleSeconds(result.AwaySeconds);
            _garden.Advance(result.AwaySeconds);
            _expeditions.Advance(result.AwaySeconds);
            _stage.EnsureEnemy();
        }

        public bool RebirthGem(GemDef gem) => _book.Rebirth(gem, _data.Gems.Levels.Max);

        public CraftResult CraftSetPiece(string setId, Content.Items.ItemSlot? slot = null, string? weaponType = null, bool secondRing = false) => _drops.CraftSetPiece(setId, slot, weaponType, secondRing);
        public CraftResult CraftUnique(string id) => _drops.CraftUnique(id);

        public TrialResult StartTrial(string trialId) => _chronicle.Start(trialId);

        public bool AbandonTrial() => _chronicle.Abandon();

        public TrialResult StartWeeklyTrial(string trialId) => _chronicle.StartWeekly(trialId);

        public AtlasResult AllocateAtlasNode(string nodeId) => _atlas.Allocate(nodeId);

        public ResonanceResult RaiseResonance(string branchId) => _resonance.Raise(branchId);

        public SpiritResult RaiseSpirit(string spiritId) => _spirits.Raise(spiritId);

        public bool CompleteActivity(string activityId)
        {
            if (!_data.Ascendancy.IsActivity(activityId) || !State.Ascendancy.Complete(activityId))
                return false;
            if (State.Ascendancy.Trait(_data.Ascendancy) != null)
                RebuildTree();
            return true;
        }

        public TraitForkResult PickTraitFork(string optionId)
        {
            var result = State.Ascendancy.PickFork(_data.Ascendancy, State.HeroLevel, optionId);
            if (result == TraitForkResult.Picked)
                RebuildTree();
            return result;
        }

        public SubclassResult ChooseSubclass(string subclassId) =>
            AfterSubclassChange(State.Ascendancy.Choose(_data.Ascendancy, State.ClassName, subclassId));

        public SubclassResult ChangeSubclassFree(string subclassId, long nowUnixMs) =>
            AfterSubclassChange(State.Ascendancy.ChangeFree(_data.Ascendancy, State.ClassName, subclassId, nowUnixMs));

        public SubclassResult ChangeSubclassPaid(string subclassId) =>
            AfterSubclassChange(State.Ascendancy.ChangePaid(_data.Ascendancy, State.ClassName, subclassId));

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
                current.Skills.Add(slot.Skill);
                current.Supports.Add((GemDef?[])slot.Supports.Clone());
                current.Roles.Add(slot.Role);
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
                var skill = next.Skills[i];
                if (_book.Equip(skill) != BookResult.Done)
                    continue;
                var index = State.Skills.Count - 1;
                var role = next.RoleAt(i);
                if (role != null)
                    _book.Assign(index, role.Value);
                State.Skills[index].StartCooldown((int)System.Math.Round(skill.Skill!.CooldownSeconds * TicksPerSecond));
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
            State.PartyMode = State.Ascendancy.PartyMode(_data.Ascendancy);
            for (var i = 0; i < modifiers.Count; i++)
                modifiers[i] = modifiers[i].InPool(ModifierPool.Tree);
            _treeLayer.Set(modifiers);
            RefreshClassAttribute();
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
            _conditions.Stages = data.Stages;
            _conditions.Forge = data.Forge;
            _combat.Dreamcatcher.UseEnemies(data.Enemies);
            _loot.UseData(data.Stages);
            _levels.UseData(data.Combat.Hero, data.Stages, data.TreePoints);
            _levels.CommonSlots = data.TreeTemplates.CommonSlots;
            _dps.UseRules(data.Combat.Offline, data.Combat.Focus);
            _quests.UseRules(data.Quests);
            _achievements.UseCatalog(data.Achievements);
            _bestiary.UseCatalog(data.Bestiary);
            _cosmetics.UseCatalog(data.Cosmetics);
            _seasonPass.UseRules(data.SeasonPass);
            _skills.UseGems(data.Gems);
            _skills.Levels = data.Gems.Levels;
            _gemExperience.UseLevels(data.Gems.Levels);
            _weapons.UseCatalog(data.Weapons);
            _drops.UseData(data.Drops, data.Stages, data.Items);
            _reroll.UseRules(data.Drops);
            _gear.UseCatalog(data.Items);
            _gates.UseStages(data.Stages);
            _unlocks.Rules = data.Unlocks;
            _drops.Dungeons = data.Dungeons;
            _slates.Rules = data.Slates;
            _dungeons.UseData(data);
            _seasonal.UseData(data);
            _peaceful.UseRules(data.Peaceful);
            _camp.UseRules(data.Camp);
            _garden.UseRules(data.Garden);
            _alchemy.UseRules(data.Alchemy);
            _expeditions.UseRules(data.Expeditions);
            _town.UseRules(data.Town);
            _fishing.UseRules(data.Fishing);
            _cooking.UseRules(data.Cooking);
            _forge.UseRules(data.Forge);
            _museum.UseRules(data.Museum);
            _decor.UseRules(data.Decor);
            _skills.UseWeapons(data.Weapons);
            _book.UseRules(data.SkillBook);
            _book.UseWeapons(data.Weapons);
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
            RefreshClassAttribute();
            LastInput = Permissions.Apply(_input.Drain());
            SessionInput.Add(LastInput, MetersPerPixel);
            State.TotalInput.Add(LastInput, MetersPerPixel);
            SessionInput.PlaySeconds += TickSeconds;
            State.TotalInput.PlaySeconds += TickSeconds;
            _peaceful.Tick(LastInput, MetersPerPixel);
            _camp.Tick();
            _garden.Tick();
            _alchemy.Tick();
            _expeditions.Tick();
            _fishing.Tick();
            _cooking.Tick();
            _forge.Tick();
            _museum.Tick();
            RebuildOnMuseum();
            if (_peaceful.InCamp)
            {
                CampTick(firstEvent);
                return;
            }
            var raw = _inputRules.Apply(LastInput);
            var input = raw.WithMouse(raw.MousePx * System.Math.Max(0, Hero.Value(CombatStats.MouseDistance, 1, StatTags.Mouse)));
            var idleTicksBefore = State.IdleTicks;
            State.IdleTicksBeforeInput = idleTicksBefore;
            _focus.Tick(input);
            CountIdle();
            _combat.NotePause(idleTicksBefore, input.HasInput);
            _resource.Tick(input);
            _weapons.ApplyBuiltInSupports(State.Skills);
            _book.TrimSockets();
            _skills.Tick(input, idleTicksBefore);
            _skills.CastFromButtons(input);
            CastRequested();
            _skills.CastAutomatic();
            _partyBuffs.Tick();
            _ghost.NoteInput(input.HasInput);
            _ghost.Tick();
            _synergy.Tick();
            _conditions.NoteInput(input);
            _conditions.Tick();
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
            _quests.Tick(firstEvent);
            _seasonal.Tick(firstEvent);
            _seasonPass.Tick();
            _alchemy.BattleTick();
            _cooking.BattleTick();
            _forge.NoteKills(firstEvent);
            _artifacts.TickAwakening();
            _bestiary.Tick(firstEvent);
            _achievements.Tick();
            _dps.Tick(_combat.LastTickDamage);
            State.DamageWindow.Record(State.TotalDamage);
            State.Tick++;
            if (Permissions.Consent)
                _tutorial.Tick(LastInput, Events, firstEvent);
            Hook?.AfterTick();
        }

        private void CampTick(int firstEvent)
        {
            _castRequests = 0;
            _quests.Tick(firstEvent);
            _seasonal.Tick(firstEvent);
            _seasonPass.Tick();
            _artifacts.SkipAwakening();
            _achievements.Tick();
            State.Tick++;
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
