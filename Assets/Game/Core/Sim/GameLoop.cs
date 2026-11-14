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
        private readonly PartyGrowthLayer _partyGrowth;
        private readonly RiftSystem _rift;
        private readonly WeaponSystem _weapons;
        private readonly DropSystem _drops;
        private readonly QuestSystem _quests;
        private readonly AchievementSystem _achievements;
        private readonly BestiarySystem _bestiary;
        private readonly CosmeticSystem _cosmetics;
        private readonly SeasonPassSystem _seasonPass;
        private readonly TournamentSystem _tournament;
        private readonly ClanCampSystem _clanCamp;
        private readonly GuildSystem _guilds;
        private MasterTrial? _trial;
        private readonly TalentSystem _talents;
        private readonly HideoutSystem _hideout;
        private readonly CampLevelSystem _campLevels;
        private readonly ApprenticeSystem _apprentices;
        private readonly CampModuleSystem _modules;
        private readonly DigSystem _digs;
        private readonly CaravanSystem _caravan;
        private readonly DistillSystem _distill;
        private readonly ColossusSeedSystem _colossus;
        private readonly OverloadSystem _overload;
        private readonly PathSystem _paths;
        private readonly GemExperienceSystem _gemExperience;
        private readonly OdditySystem _oddities;
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
        private readonly CompanionSystem _companions;
        private readonly NestSystem _nest;
        private readonly CompanionStrip _strip;
        private readonly StripScene _scene;
        private readonly CompanionGearSystem _companionGear;
        private readonly CompanionWorkSystem _companionWork;
        private readonly CompanionAbilitySystem _companionAbilities;
        private readonly MountSystem _mount;
        private readonly FacetSystem _facets;
        private readonly MenagerieTreeSystem _menagerie;
        private readonly TownSystem _town;
        private readonly FishingSystem _fishing;
        private readonly CookingSystem _cooking;
        private readonly RiftSupplySystem _riftSupply;
        private readonly ForgeSystem _forge;
        private readonly MuseumSystem _museum;
        private readonly ModifierLayer[] _homeOnly;
        private readonly RiftGiftSystem _riftGifts;
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
            _oddities = new OdditySystem(state, hero, Events, _stage, _loot, _drops);
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
            _unlocks = new Unlocks(data.Unlocks, state, Events) { Rift = data.Rift };
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
            _talents = new TalentSystem(data.Talents, state);
            _camp = new CampSystem(data.Camp, state, Events) { Unlocks = _unlocks };
            _unlocks.Camp = _camp;
            _clanCamp = new ClanCampSystem(data.ClanCamp, state, hero, _camp);
            _hideout = new HideoutSystem(data.Hideout, data.ClanCamp, data.Garden, state, _camp);
            _guilds = new GuildSystem(data.Guilds, state, data.Stages, data.Fishing, data.Garden, Events);
            _campLevels = new CampLevelSystem(data.CampLevels, state, _camp, _guilds);
            _apprentices = new ApprenticeSystem(data.Apprentices, state, _camp, _guilds);
            _modules = new CampModuleSystem(data.CampModules, state, _camp, _campLevels, Events, data.Forge.Building);
            _garden = new GardenSystem(data.Garden, state, _camp, _peaceful, Events);
            _alchemy = new AlchemySystem(data.Alchemy, state, hero, _camp, _peaceful, Events);
            _expeditions = new ExpeditionSystem(data.Expeditions, state, data.Stages, data.Cosmetics, _camp, Events);
            _cooking = new CookingSystem(data.Cooking, state, hero, _camp, _peaceful, Events);
            _riftSupply = new RiftSupplySystem(data.Cooking, data.Alchemy, state, hero);
            _riftGifts = new RiftGiftSystem(data.Rift.Gifts, state, hero, Events);
            _companions = new CompanionSystem(data.Companions, data.Cooking, data.Combat.Conditions, state, _camp, _expeditions, Events);
            _nest = new NestSystem(state, _companions, _camp, data.Stages, Events);
            _nest.GuildLevel = _guilds.Level;
            _companionGear = new CompanionGearSystem(state, _camp, _companions, _expeditions);
            _companionWork = new CompanionWorkSystem(state, _camp, _companions, _expeditions);
            _menagerie = new MenagerieTreeSystem(_companions);
            _strip = new CompanionStrip(state, _companions, _expeditions, _peaceful, Events);
            _scene = new StripScene(data.Panel.Scene, state, _peaceful, Events);
            _forge = new ForgeSystem(data.Forge, state, data.Stages, _camp, _peaceful, _weapons, _gear, Events);
            _mount = new MountSystem(data.Combat.Mount, state, data.Stages, hero);
            _facets = new FacetSystem(data.Combat.Facets, state, hero);
            _mount.Changed = () =>
            {
                RefreshClassAttribute();
                _facets.Rebuild();
            };
            _drops.GuardianKilled = _facets.NoteGuardian;
            _quests.DailyClaimed = _facets.NoteDailyClaim;
            _forge.Attack = data.Combat.Attack;
            _drops.Salvaged = Salvage;
            _seasonal.BossWon = eventId => _drops.GiveEventPiece(eventId);
            _seasonal.CompanionEarned = companion => Nest.GiveEventCompanion(companion);
            _museum = new MuseumSystem(data.Museum, state, hero, _alchemy, Events);
            _decor = new DecorSystem(data.Decor, state, _camp, _garden, _forge, _expeditions);
            _hideout.Decor = _decor;
            _hideout.Companions = _companions;
            _chronicle.Companions = _companions;
            _hideout.Nest = _nest;
            _hideout.Stations = _campLevels;
            _hideout.Kitchen = _cooking;
            _museum.Companions = _companions;
            _forge.Shared = data.Hideout.Crafts;
            _alchemy.Shared = data.Hideout.Crafts;
            _fishing = new FishingSystem(data.Fishing, state, _camp, _peaceful, _expeditions, Events);
            _digs = new DigSystem(data.Digs, state, _camp, _peaceful, Events);
            _companionAbilities = new CompanionAbilitySystem(state, _companions, _expeditions, _garden, _digs, _levels);
            _caravan = new CaravanSystem(data.Caravan, state, _camp, _expeditions);
            _distill = new DistillSystem(data.Distill, data.Colossus, state, _camp, _peaceful, _alchemy, Events);
            _colossus = new ColossusSeedSystem(data.Colossus, state, _camp, _garden, _peaceful, Events);
            _town = new TownSystem(data.Town, state, _camp, _alchemy, _expeditions, Events);
            _stage.ChestGold = data.Atlas.ChestGold;
            _loot.JackpotMultiplier = data.Artifacts.JackpotMultiplier;
            _stage.Encounters = data.Combat.Encounters;
            _stage.Pierce = data.Combat.Pierce;
            _stage.Defense = data.Combat.Defense;
            _stage.Elements = data.Combat.Elements;
            _upgrades = new StrikeUpgradeSystem(data.Combat.StrikeUpgrade, state, hero);
            _prestige = new PrestigeSystem(data.Prestige, state, Events, _stage, _upgrades, hero);
            _tournament = new TournamentSystem(data.Tournaments, state, _prestige);
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
            _book.Evolve();
            _treeLayer = new ModifierLayer(hero);
            _partyBuffs = new PartyBuffs(data.Party.Buffs, state, hero);
            _synergy = new SynergySystem(data.Synergy, state, hero);
            _conditions = new ConditionSystem(state, hero, _combat, _skills, _focus) { Stages = data.Stages, Forge = data.Forge, Synergy = _synergy };
            _ghost = new GhostSystem(data.Party.Ghost, state, _combat, _stage, _synergy);
            _rift = new RiftSystem(data, state, hero, _stage, _artifacts, _synergy) { Slates = _slates, Gear = _gear, BeforeLeave = _dungeons.Retreat, HeroSwapped = OnHeroSwapped };
            _rift.Refresh();
            _drops.Rift = data.Rift;
            _raid = new RaidTally(data.Raid, state, _stage, _ghost, Events, hero);
            _clanTree = new ClanTreeBonus(data.ClanTree, hero, state.ClanBonus);
            _mentor = new MentorBonus(data.Mentor, hero, _levels, state);
            _partyGrowth = new PartyGrowthLayer(data.Party.Growth, hero) { Items = data.Items };
            PartyColossus = new PartyColossusMeter(data.Party.Growth.Colossus);
            MascotStrip = new MascotStrip(data.Party.Growth.Mascot, state, _partyGrowth, Events);
            _homeOnly = new[] { _clanTree.Layer, _mentor.Layer, _alchemy.Layer, _museum.Layer, _cooking.Layer, _clanCamp.Layer, _gates.Layer, _chronicle.Layer };
            MuteHomeOnly();
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
        public PartyActivityMeter PartyActivity { get; } = new PartyActivityMeter();
        public PartyGrowthLayer PartyGrowth => _partyGrowth;
        public PartyColossusMeter PartyColossus { get; }
        public MascotStrip MascotStrip { get; }
        public bool AtPartyColossus => _stage.AtColossus;
        private bool ColossusCounts => _partyGrowth.InParty && _partyColossusOpen;
        private bool _partyColossusOpen;

        public void SetPartyColossusOpen(bool open)
        {
            _partyColossusOpen = open;
            if (!open)
                LeavePartyColossus();
        }

        public bool EnterPartyColossus()
        {
            if (!ColossusCounts || _rift.Inside || _dungeons.Inside || _peaceful.InCamp)
                return false;
            return _stage.EnterColossus();
        }

        public void LeavePartyColossus() => _stage.LeaveColossus();

        private ModifierLayer? _colossusCurse;
        private readonly Modifier[] _curseLine = new Modifier[1];
        private PartyColossusHardship? _curse;
        private long _curseTicks;

        public long ColossusCurseTicks => _curseTicks;

        public void SetPartyColossusHardships(IReadOnlyList<string> active)
        {
            _curse = null;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i] == "curse")
                    _curse = _data.Party.Growth.Colossus.Hardship("curse");
            }
            if (_curse == null)
                _curseTicks = 0;
            ShowCurse();
        }

        private void NotePartyColossus()
        {
            var active = State.IdleTicks <= _data.Party.Contribution.ActiveInputSeconds * TicksPerSecond;
            var before = PartyColossus.Direct;
            PartyColossus.Note(_combat, _data.Stages, State.RecordStage, _stage.AtColossus, active, ColossusCounts, _partyGrowth.ColossusPoints * ColossusCallFactor);
            if (_curse != null && PartyColossus.Direct > before)
                _curseTicks = (long)System.Math.Round(_curse.Number("minutes") * 60 * TicksPerSecond);
            else if (_curseTicks > 0)
                _curseTicks--;
            ShowCurse();
        }

        private void ShowCurse()
        {
            var cursed = _curse != null && _curseTicks > 0 && !_stage.AtColossus;
            if (cursed == (_colossusCurse != null && _colossusCurse.Applied.Count > 0))
                return;
            _colossusCurse ??= new ModifierLayer(Hero);
            _curseLine[0] = new Modifier(CombatStats.Damage, ModifierKind.More, _curse?.Number("damage") ?? 0);
            _colossusCurse.Set(cursed ? _curseLine : System.Array.Empty<Modifier>());
        }
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
        public CompanionSystem Companions => _companions;
        public NestSystem Nest => _nest;
        public CompanionStrip Strip => _strip;
        public StripScene Scene => _scene;
        public CompanionGearSystem CompanionGear => _companionGear;
        public CompanionWorkSystem CompanionWork => _companionWork;
        public CompanionAbilitySystem CompanionAbilities => _companionAbilities;
        public MountSystem Mount => _mount;
        public FacetSystem Facets => _facets;
        public MenagerieTreeSystem Menagerie => _menagerie;
        public TownSystem Town => _town;
        public FishingSystem Fishing => _fishing;
        public CookingSystem Cooking => _cooking;
        public RiftSupplySystem RiftSupply => _riftSupply;
        public RiftGiftSystem RiftGifts => _riftGifts;
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
        public TournamentSystem Tournament => _tournament;
        public ClanCampSystem ClanCamp => _clanCamp;
        public GuildSystem Guilds => _guilds;
        public MasterTrial MasterTrial => _trial ??= new MasterTrial(_data);
        private bool TrialRunning => _trial != null && _trial.Active;

        public MasterTrialResult StartMasterTrial(string building)
        {
            if (!_peaceful.InCamp || _peaceful.State.Place != PeacefulPlace.Town)
                return MasterTrialResult.NotInTown;
            if (TrialRunning)
                return MasterTrialResult.Busy;
            var def = _data.Guilds.Building(building);
            if (def == null || !HeroClassNames.ByName.TryGetValue(def.Master, out var master))
                return MasterTrialResult.NoMaster;
            if (!State.Camp.Guilds.Contains(def.ProjectIds[0]))
                return MasterTrialResult.NotBuilt;
            MasterTrial.Start(master, def.Id, (ulong)State.Tick + 1);
            return MasterTrialResult.Started;
        }

        public void StopMasterTrial()
        {
            if (!TrialRunning)
                return;
            Events.Add(GameEvent.ForStage(GameEventKind.MasterTrialEnded, State.Tick, _trial!.Kills));
            _trial.Stop();
        }

        private void StepMasterTrial()
        {
            if (TrialRunning && _trial!.Step(LastInput))
                StopMasterTrial();
        }
        public TalentSystem Talents => _talents;
        public HideoutSystem Hideout => _hideout;
        public CampLevelSystem CampLevels => _campLevels;
        public ApprenticeSystem Apprentices => _apprentices;
        public CampModuleSystem Modules => _modules;
        public DigSystem Digs => _digs;
        public CaravanSystem Caravan => _caravan;
        public DistillSystem Distill => _distill;
        public ColossusSeedSystem Colossus => _colossus;
        public OverloadSystem Overload => _overload;
        public PathSystem Paths => _paths;
        public GemExperienceSystem GemExperience => _gemExperience;
        public OdditySystem Oddities => _oddities;
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

        private RecapSystem? _recap;

        public GameState State { get; }
        public EventQueue Events { get; } = new EventQueue(EventCapacity);

        public const int EventCapacity = 4096;
        public InputSnapshot LastInput { get; private set; }
        public InputStats SessionInput { get; } = new InputStats();
        public RecapSystem Recap => _recap ??= new RecapSystem(State);
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
            _facets.NoteClassChange();
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
            _book.Evolve();
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
                if (SkillBook.IsClassSkill(reforged) && !_book.OfHeroClass(reforged) && !_book.OpenedByWeapon(reforged))
                    continue;
                var slot = State.Skills[_skills.Equip(reforged, SkillLoadout.Accepts(role, reforged) ? role : (SkillSlotRole?)null)];
                slot.StartCooldown((int)System.Math.Round(slot.Skill.Skill!.CooldownSeconds * TicksPerSecond));
                supports.CopyTo(slot.Supports, 0);
            }
        }

        public ClassTree? ClassTreeDef => State.ClassChosen ? _data.ClassTreeOf(State.ClassName) : null;

        public string ShareBuild() => ClassTreeDef is { } tree ? BuildCode.Encode(tree, State.ClassTree, State.Skills) : string.Empty;

        public SharedBuild? ReadBuild(string? code) => BuildCode.Decode(code, _data.ClassTreeOf, _data.Gems);

        public BuildImportResult ImportBuild(string? code)
        {
            if (ReadBuild(code) is not { } build)
                return BuildImportResult.BadCode;
            if (ClassTreeDef is not { } tree)
                return BuildImportResult.NoClass;
            if (build.TreeId != tree.Id)
                return BuildImportResult.OtherClass;
            BuildCode.UseAsPlan(build, tree, State.ClassTree);
            FillTrees();
            RebuildTree();
            return BuildImportResult.Done;
        }

        public double ClassAttribute => _classAttribute.Points;

        public ClassAttributeDef ClassAttributeDef => ClassTreeDef?.Attribute ?? ClassAttributeDef.None;

        public int ClassElement
        {
            get
            {
                var element = ClassTreeDef?.Element ?? ElementRules.None;
                var strongest = 0.0;
                for (var i = 0; i < ElementRules.Count; i++)
                {
                    var shift = Hero.Value(ElementRules.Shift[i], 0);
                    if (shift > strongest)
                    {
                        strongest = shift;
                        element = i;
                    }
                }
                return element;
            }
        }

        public void RefreshClassAttribute()
        {
            _combat.ClassElement = ClassElement;
            _classAttribute.Refresh(ClassAttributeDef);
            _combat.RefreshAttack(ClassAttribute);
        }

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

        public (ClassTree Tree, ClassTreeAllocation Allocation)? TreeOfNode(string nodeId)
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
            var chosen = State.GemProgress.TryGetValue(skill.ProgressId, out var progress) && tier >= 0 && tier < progress.Branches.Length
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
            allocation.ReturnPoints();
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

        public void SetServerTime(long utcUnixMs)
        {
            State.ServerHour = _data.Combat.Conditions.ServerHour(utcUnixMs);
            if (_partyGrowth.Refresh(utcUnixMs))
                RefreshPeacefulBonus();
        }

        public void SetPartyStanding(bool inParty, long joinedUnixMs, int members, long nowUnixMs)
        {
            _partyGrowth.SetStanding(inParty, joinedUnixMs, members, nowUnixMs);
            RefreshPeacefulBonus();
        }

        public void SetPartyTree(IReadOnlyDictionary<int, int> levels, IReadOnlyList<string> majors)
        {
            _partyGrowth.SetTree(levels, majors);
            RefreshPeacefulBonus();
        }

        public int MascotFoodCount(string food)
        {
            var def = _data.Party.Growth.Mascot.Food(food);
            if (def == null)
                return 0;
            if (def.IsDish)
                return State.Cooking.Meals.Count;
            var total = 0L;
            foreach (var pair in State.Camp.Bag)
            {
                if (pair.Key.StartsWith(def.Prefix, System.StringComparison.Ordinal))
                    total += pair.Value;
            }
            return (int)System.Math.Min(int.MaxValue, total);
        }

        public int TakeMascotFood(string food, int amount)
        {
            var def = _data.Party.Growth.Mascot.Food(food);
            if (def == null || amount <= 0)
                return 0;
            var taken = 0;
            if (def.IsDish)
            {
                while (taken < amount && State.Cooking.Meals.Count > 0)
                {
                    State.Cooking.Meals.RemoveAt(State.Cooking.Meals.Count - 1);
                    taken++;
                }
                return taken;
            }
            var items = new List<string>();
            foreach (var pair in State.Camp.Bag)
            {
                if (pair.Key.StartsWith(def.Prefix, System.StringComparison.Ordinal))
                    items.Add(pair.Key);
            }
            items.Sort(System.StringComparer.Ordinal);
            foreach (var item in items)
            {
                var take = (int)System.Math.Min(amount - taken, _camp.Count(item));
                if (take > 0 && _camp.Take(item, take))
                    taken += take;
                if (taken >= amount)
                    break;
            }
            return taken;
        }

        public void SetPartyMascot(string beast, int level, IReadOnlyDictionary<string, int>? rites = null)
        {
            _partyGrowth.SetMascot(beast, level, rites);
            RefreshPeacefulBonus();
        }

        private ModifierLayer? _callLayer;
        private readonly List<Modifier> _callLines = new List<Modifier>();
        private PartyMascotBeast? _callBeast;
        private double _callColossus = 1;
        private double _mascotActions;

        public double MascotActions => _mascotActions;
        public bool MascotCalling => State.MascotCallTicks > 0;
        public double ColossusCallFactor => MascotCalling ? _callColossus : 1;

        public double TakeMascotActions()
        {
            var actions = _mascotActions;
            _mascotActions = 0;
            return actions;
        }

        public bool StartPartyMascotCall()
        {
            var beast = _data.Party.Growth.Mascot.Beast(_partyGrowth.MascotBeast);
            if (beast == null || !_partyGrowth.InParty)
                return false;
            _callBeast = beast;
            _callLines.Clear();
            _callColossus = _partyGrowth.CallColossus;
            var power = _partyGrowth.CallPower;
            foreach (var line in beast.Call)
            {
                if (!line.Works || !line.Modifier.HasValue)
                    continue;
                var modifier = line.Modifier.Value;
                if (modifier.Stat == PartyGrowthLayer.ColossusDamage)
                    _callColossus *= 1 + modifier.Value * power;
                else
                    _callLines.Add(modifier.WithValue(modifier.Value * power));
            }
            _callLayer ??= new ModifierLayer(Hero);
            _callLayer.Set(_callLines);
            State.MascotCallTicks = (long)System.Math.Round(_partyGrowth.CallSeconds * TicksPerSecond);
            return true;
        }

        private void NoteMascotInput(in EffectiveInput input)
        {
            if (_partyGrowth.InParty && _partyGrowth.MascotBeast.Length > 0)
                _mascotActions += _data.Party.Growth.Mascot.CallActions(input.Hits, input.ButtonPresses, input.MousePx);
        }

        private void TickMascotCall()
        {
            if (State.MascotCallTicks <= 0)
                return;
            foreach (var line in _callBeast?.Call ?? (IReadOnlyList<PartyMascotCallLine>)System.Array.Empty<PartyMascotCallLine>())
            {
                if (line.BossTimerStop && State.FightingBoss)
                    State.BossTimerStopTicks = System.Math.Max(State.BossTimerStopTicks, 1);
                if (line.FocusFull)
                    State.Focus = _focus.Max;
            }
            if (--State.MascotCallTicks > 0)
                return;
            _callColossus = 1;
            _callLayer?.Set(System.Array.Empty<Modifier>());
        }

        public void SetPartySets(IReadOnlyDictionary<string, int> counts)
        {
            _partyGrowth.SetPartySets(counts);
            RefreshPeacefulBonus();
        }

        public IReadOnlyList<string> CollectedPartySets() => _gear.CollectedPartySets();

        public void SetPartySetsOpen(bool open) => _drops.PartySetsOpen = open && _partyGrowth.InParty;

        public bool GivePartySetPiece(string setId, string piece) => _drops.GivePartySetPiece(setId, piece);

        public int BagRoom => _drops.BagRoom;

        public void SetPartyBanner(IReadOnlyList<string> crests)
        {
            _partyGrowth.SetBanner(crests);
            RefreshPeacefulBonus();
        }

        public void SetPartyRelics(IReadOnlyDictionary<string, int> levels)
        {
            _partyGrowth.SetRelics(levels);
            RefreshPeacefulBonus();
        }

        public void SetPartyTrials(IReadOnlyList<int> bestTiers)
        {
            _partyGrowth.SetTrials(bestTiers);
            RefreshPeacefulBonus();
        }

        public void SetPartyGrowthSources(IReadOnlyList<Modifier> sources)
        {
            _partyGrowth.SetSources(sources);
            RefreshPeacefulBonus();
        }

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
            Recap.SetDay(day);
            _seasonal.SetDay(day);
            _town.SetDay(day);
            _companions.SetDay(day);
            _nest.SetDay();
        }

        public QuestResult ClaimQuest(bool weekly, int index)
        {
            var result = _quests.Claim(weekly, index);
            if (result == QuestResult.Done && !weekly)
                _seasonal.NoteDailyQuest();
            return result;
        }

        public QuestResult SwapQuest(int index) => _quests.Swap(index);

        public bool ReturnHomeIfAway(double awaySeconds)
        {
            var after = _dungeons.Rules.RetreatSeconds;
            var fromDungeon = _dungeons.RetreatIfAway(awaySeconds);
            var fromFight = _seasonal.RetreatIfAway(awaySeconds, after);
            var fromRift = _rift.LeaveIfAway(awaySeconds, after);
            return fromDungeon || fromFight || fromRift;
        }

        public void ApplyOffline(OfflineResult result)
        {
            _drops.ApplyOffline(result);
            PartyColossus.NoteOffline(result, _data.Stages, State.RecordStage, ColossusCounts, _partyGrowth.ColossusPoints);
            OfflineCalculator.Apply(State, result);
            AddIdleSeconds(result.AwaySeconds);
            RefreshPeacefulBonus();
            _garden.Advance(result.AwaySeconds);
            _expeditions.Advance(result.AwaySeconds);
            _companions.Advance(result.AwaySeconds);
            _nest.Advance(result.AwaySeconds);
            _apprentices.AdvanceOffline(result.AwaySeconds);
            _caravan.Collect();
            _colossus.Advance(result.AwaySeconds);
            _colossus.Collect();
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
            _drops.Rift = data.Rift;
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
            _tournament.UseRules(data.Tournaments);
            _clanCamp.UseRules(data.ClanCamp);
            _guilds.UseRules(data.Guilds);
            _talents.UseRules(data.Talents);
            _hideout.UseRules(data.Hideout, data.ClanCamp, data.Garden);
            _campLevels.UseRules(data.CampLevels);
            _apprentices.UseRules(data.Apprentices);
            _modules.UseRules(data.CampModules);
            _skills.UseGems(data.Gems);
            _skills.Levels = data.Gems.Levels;
            _gemExperience.UseLevels(data.Gems.Levels);
            _weapons.UseCatalog(data.Weapons);
            _drops.UseData(data.Drops, data.Stages, data.Items);
            _reroll.UseRules(data.Drops);
            _gear.UseCatalog(data.Items);
            _gates.UseStages(data.Stages);
            _unlocks.Rules = data.Unlocks;
            _unlocks.Rift = data.Rift;
            _drops.Dungeons = data.Dungeons;
            _slates.Rules = data.Slates;
            _dungeons.UseData(data);
            _seasonal.UseData(data);
            _peaceful.UseRules(data.Peaceful);
            _scene.UseRules(data.Panel.Scene);
            _camp.UseRules(data.Camp);
            _garden.UseRules(data.Garden);
            _alchemy.UseRules(data.Alchemy);
            _alchemy.Shared = data.Hideout.Crafts;
            _expeditions.UseRules(data.Expeditions);
            _companions.UseRules(data.Companions);
            _menagerie.Changed();
            _town.UseRules(data.Town);
            _fishing.UseRules(data.Fishing);
            _digs.UseRules(data.Digs);
            _caravan.UseRules(data.Caravan);
            _distill.UseRules(data.Distill);
            _colossus.UseRules(data.Colossus);
            _cooking.UseRules(data.Cooking);
            _riftSupply.UseRules(data.Cooking, data.Alchemy);
            _riftGifts.UseRules(data.Rift.Gifts);
            _forge.UseRules(data.Forge);
            _forge.Shared = data.Hideout.Crafts;
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
            _stage.Pierce = data.Combat.Pierce;
            _stage.Defense = data.Combat.Defense;
            _stage.Elements = data.Combat.Elements;
            _tutorial.UseRules(data.Tutorial);
            _partyBuffs.UseRules(data.Party.Buffs);
            _synergy.UseRules(data.Synergy);
            _ghost.UseRules(data.Party.Ghost);
            _raid.UseRules(data.Raid);
            _clanTree.UseCatalog(data.ClanTree);
            _mentor.UseRules(data.Mentor);
            _partyGrowth.UseRules(data.Party.Growth);
            PartyColossus.UseRules(data.Party.Growth.Colossus);
            MascotStrip.UseRules(data.Party.Growth.Mascot);
            _rift.UseData(data);
            RebuildTree();
            SyncPathSkill();
        }

        private void Salvage(ItemDrop drop)
        {
            if (!_rift.SalvageSparks(drop.Rarity))
                _forge.SalvageOre(drop);
            _mount.NoteSalvage(drop);
        }

        private void WearWindowSet()
        {
            var rift = State.Rift;
            var set = RiftWindowSet.For(_data.Items, _data.Rift, rift.WindowId);
            if (set == null)
                return;
            rift.WindowSet = set.Id;
            foreach (var piece in RiftWindowSet.Roll(_data.Items, set, _data.Rift.WindowSet, rift.WindowId))
            {
                State.Drops.Gear.Add(piece);
                _gear.NoteFound(piece);
                _gear.Equip(piece);
            }
            _rift.Refresh();
        }

        private void MuteHomeOnly()
        {
            var inside = State.Rift.Inside;
            for (var i = 0; i < _homeOnly.Length; i++)
                _homeOnly[i].Muted = inside;
        }

        private void OnHeroSwapped(bool fresh)
        {
            MuteHomeOnly();
            UseData(_data);
            if (fresh)
            {
                _book.GrantStartingGems();
                _weapons.GiveClassStarter();
                WearWindowSet();
            }
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

        public void RefreshPeacefulBonus()
        {
            var bonus = State.PeacefulBonus;
            bonus.Party = Basket(CombatStats.PartyPeaceful);
            var party = 1 + bonus.Party;
            bonus.Speed = Basket(CombatStats.PeacefulSpeed) * party;
            bonus.Yield = Basket(CombatStats.PeacefulYield) * party;
            bonus.Luck = Basket(CombatStats.PeacefulLuck) * party;
            bonus.Mastery = Basket(CombatStats.PeacefulMastery) * party;
        }

        private double Basket(StatId stat)
        {
            var totals = Hero.TotalsOf(stat);
            return (1 + totals.Flat + totals.IncreasedMultiplier - 1) * totals.MoreMultiplier - 1;
        }

        public void Tick()
        {
            Hook?.BeforeTick();
            var firstEvent = Events.Count;
            MuteHomeOnly();
            RefreshClassAttribute();
            LastInput = Permissions.Apply(_input.Drain());
            SessionInput.Add(LastInput, MetersPerPixel);
            State.TotalInput.Add(LastInput, MetersPerPixel);
            SessionInput.PlaySeconds += TickSeconds;
            State.TotalInput.PlaySeconds += TickSeconds;
            _talents.Tick();
            RefreshPeacefulBonus();
            _peaceful.Tick(TrialRunning ? InputSnapshot.Empty : LastInput, MetersPerPixel);
            _camp.Tick();
            _unlocks.Tick();
            _colossus.Tick();
            _garden.Tick();
            _alchemy.Tick();
            _expeditions.Tick();
            _companions.Tick();
            _nest.Tick();
            _fishing.Tick();
            _digs.Tick();
            _caravan.Tick();
            _distill.Tick();
            _cooking.Tick();
            _forge.Tick();
            _museum.Tick();
            RebuildOnMuseum();
            if (TrialRunning && (!_peaceful.InCamp || _peaceful.State.Place != PeacefulPlace.Town))
                StopMasterTrial();
            if (_peaceful.InCamp)
            {
                StepMasterTrial();
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
            _book.Evolve();
            _book.TrimSockets();
            _skills.Tick(input, idleTicksBefore);
            _skills.CastFromButtons(input);
            CastRequested();
            _skills.CastAutomatic();
            _partyBuffs.Tick();
            _ghost.NoteInput(input.HasInput);
            PartyActivity.NoteInput(input.HasInput);
            NoteMascotInput(input);
            _ghost.Tick();
            _synergy.Tick();
            _conditions.NoteInput(input);
            _conditions.Tick();
            _rift.Tick();
            TickMascotCall();
            _stage.Tick();
            _oddities.Tick(input);
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
            _tournament.Tick();
            _alchemy.BattleTick();
            _cooking.BattleTick();
            _riftSupply.Tick();
            _riftGifts.Tick();
            _apprentices.BattleTick();
            _modules.NoteBosses(firstEvent);
            _digs.NoteGuardians(firstEvent);
            _guilds.Tick(firstEvent);
            _colossus.NoteGuardians(firstEvent);
            _nest.NoteGuardians(firstEvent);
            _strip.Tick(firstEvent, LastInput.HasAnyInput);
            MascotStrip.Tick(firstEvent, LastInput.HasAnyInput);
            _scene.Tick();
            _forge.NoteKills(firstEvent);
            _artifacts.TickAwakening();
            _bestiary.Tick(firstEvent);
            _achievements.Tick();
            _dps.Tick(_combat.LastTickDamage);
            State.DamageWindow.Record(State.TotalDamage);
            NotePartyColossus();
            State.Tick++;
            if (Permissions.Consent)
                _tutorial.Tick(LastInput, Events, firstEvent);
            Hook?.AfterTick();
        }

        private void CampTick(int firstEvent)
        {
            _castRequests = 0;
            _apprentices.Collect();
            _caravan.Collect();
            _colossus.Collect();
            _quests.Tick(firstEvent);
            _seasonal.Tick(firstEvent);
            _seasonPass.Tick();
            _artifacts.SkipAwakening();
            _guilds.Tick(firstEvent);
            _achievements.Tick();
            _strip.Tick(firstEvent, LastInput.HasAnyInput);
            _scene.Tick();
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
