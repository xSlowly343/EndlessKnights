using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Tree;
using Game.Core.Content.Rift;
using Game.Core.Content.Slates;
using Game.Core.Content.Synergy;
using Game.Core.Content.Weapons;
using Game.Core.Party;
using Game.Core.Text;
using Game.Core.Ui;

namespace Game.Core.Content
{
    public sealed class GameData
    {
        public static readonly string[] FileNames = { StageTable.FileName, EnemyCatalog.FileName, InputCaps.FileName, CombatRules.FileName, GemCatalog.FileName, SkillBookRules.FileName, TreePointRules.FileName, AscendancyCatalog.FileName, PrestigeRules.FileName, ArtifactCatalog.FileName, PartyRules.FileName, TutorialRules.FileName, StringTable.FileName, SynergyRules.FileName, RiftRules.FileName, WeaponCatalog.FileName, ItemCatalog.FileName, DropRules.FileName, PanelUnlocks.FileName, ChangeLog.FileName, ChronicleCatalog.FileName, AtlasCatalog.FileName, ResonanceCatalog.FileName, SpiritCatalog.FileName, UnlockRules.FileName, DungeonRules.FileName, RaidRules.FileName, ClanTreeCatalog.FileName, MentorRules.FileName, TreeTemplates.FileName, SlateRules.FileName, CharacterCatalog.FileName, QuestRules.FileName, AchievementCatalog.FileName, BestiaryCatalog.FileName, CosmeticCatalog.FileName, SeasonPassRules.FileName, EventRules.FileName, PeacefulRules.FileName, CampRules.FileName, GardenRules.FileName, AlchemyRules.FileName, ExpeditionRules.FileName, TownRules.FileName, FishingRules.FileName, CookingRules.FileName, ForgeRules.FileName, MuseumRules.FileName, DecorRules.FileName, ChestCatalog.FileName, TournamentRules.FileName, ClanCampRules.FileName, GuildRules.FileName, TalentRules.FileName, HideoutRules.FileName, CampLevelRules.FileName, ApprenticeRules.FileName, CampModuleRules.FileName, DigRules.FileName };

        private GameData(StageTable stages, EnemyCatalog enemies, InputCaps inputCaps, CombatRules combat, GemCatalog gems, SkillBookRules skillBook, TreePointRules treePoints, AscendancyCatalog ascendancy, PrestigeRules prestige, ArtifactCatalog artifacts, PartyRules party, TutorialRules tutorial, StringTable strings, SynergyRules synergy, RiftRules rift, WeaponCatalog weapons, ItemCatalog items, DropRules drops, PanelUnlocks panel, ChangeLog changeLog, ChronicleCatalog chronicle, AtlasCatalog atlas, ResonanceCatalog resonance, SpiritCatalog spirits)
        {
            Items = items;
            Stages = stages;
            Enemies = enemies;
            InputCaps = inputCaps;
            Combat = combat;
            Gems = gems;
            TreePoints = treePoints;
            Ascendancy = ascendancy;
            SkillBook = skillBook;
            Prestige = prestige;
            Artifacts = artifacts;
            Party = party;
            Strings = strings;
            Synergy = synergy;
            Rift = rift;
            Weapons = weapons;
            Drops = drops;
            Panel = panel;
            Tutorial = tutorial;
            ChangeLog = changeLog;
            Chronicle = chronicle;
            Atlas = atlas;
            Resonance = resonance;
            Spirits = spirits;
        }

        public StageTable Stages { get; }
        public EnemyCatalog Enemies { get; }
        public InputCaps InputCaps { get; }
        public CombatRules Combat { get; }
        public GemCatalog Gems { get; }
        public TreePointRules TreePoints { get; }
        public AscendancyCatalog Ascendancy { get; }
        public CharacterCatalog Characters { get; private set; } = null!;
        public SkillBookRules SkillBook { get; }
        public PrestigeRules Prestige { get; }
        public ArtifactCatalog Artifacts { get; }
        public PartyRules Party { get; }
        public TutorialRules Tutorial { get; }
        public StringTable Strings { get; }
        public SynergyRules Synergy { get; }
        public RiftRules Rift { get; }
        public WeaponCatalog Weapons { get; }
        public ItemCatalog Items { get; }
        public DropRules Drops { get; }
        public PanelUnlocks Panel { get; }
        public ChangeLog ChangeLog { get; }
        public ChronicleCatalog Chronicle { get; }
        public AtlasCatalog Atlas { get; }
        public ResonanceCatalog Resonance { get; }
        public SpiritCatalog Spirits { get; }
        public UnlockRules Unlocks { get; private set; } = UnlockRules.AllOpen;
        public DungeonRules Dungeons { get; private set; } = null!;
        public RaidRules Raid { get; private set; } = null!;
        public ClanTreeCatalog ClanTree { get; private set; } = null!;
        public MentorRules Mentor { get; private set; } = null!;
        public TreeTemplates TreeTemplates { get; private set; } = null!;
        public SlateRules Slates { get; private set; } = null!;
        public QuestRules Quests { get; private set; } = null!;
        public AchievementCatalog Achievements { get; private set; } = null!;
        public BestiaryCatalog Bestiary { get; private set; } = null!;
        public CosmeticCatalog Cosmetics { get; private set; } = null!;
        public SeasonPassRules SeasonPass { get; private set; } = null!;
        public ChestCatalog Chests { get; private set; } = null!;
        public TournamentRules Tournaments { get; private set; } = null!;
        public ClanCampRules ClanCamp { get; private set; } = null!;
        public GuildRules Guilds { get; private set; } = null!;
        public TalentRules Talents { get; private set; } = null!;
        public HideoutRules Hideout { get; private set; } = null!;
        public CampLevelRules CampLevels { get; private set; } = null!;
        public ApprenticeRules Apprentices { get; private set; } = null!;
        public CampModuleRules CampModules { get; private set; } = null!;
        public DigRules Digs { get; private set; } = null!;
        public EventRules Events { get; private set; } = null!;
        public PeacefulRules Peaceful { get; private set; } = null!;
        public CampRules Camp { get; private set; } = null!;
        public GardenRules Garden { get; private set; } = null!;
        public AlchemyRules Alchemy { get; private set; } = null!;
        public ExpeditionRules Expeditions { get; private set; } = null!;
        public TownRules Town { get; private set; } = null!;
        public FishingRules Fishing { get; private set; } = null!;
        public CookingRules Cooking { get; private set; } = null!;
        public ForgeRules Forge { get; private set; } = null!;
        public MuseumRules Museum { get; private set; } = null!;
        public DecorRules Decor { get; private set; } = null!;
        public IReadOnlyDictionary<string, ClassTree> ClassTrees { get; private set; } = new Dictionary<string, ClassTree>();

        public IReadOnlyDictionary<string, ClassTree> CommonTrees { get; private set; } = new Dictionary<string, ClassTree>();

        public ClassTree? ClassTreeOf(string heroClass) => ClassTrees.TryGetValue(heroClass, out var tree) ? tree : null;

        public ClassTree? CommonTreeOf(string treeId) => CommonTrees.TryGetValue(treeId, out var tree) ? tree : null;

        public static GameData Load(Func<string, string> readFile)
        {
            var stages = StageTable.Load(readFile(StageTable.FileName));
            var enemies = EnemyCatalog.Load(readFile(EnemyCatalog.FileName), stages.Biomes);
            var inputCaps = InputCaps.Load(readFile(InputCaps.FileName));
            var combat = CombatRules.Load(readFile(CombatRules.FileName));
            var gems = GemCatalog.Load(readFile(GemCatalog.FileName));
            var skillBook = SkillBookRules.Load(readFile(SkillBookRules.FileName), gems);
            var treePoints = TreePointRules.Load(readFile(TreePointRules.FileName));
            var ascendancy = AscendancyCatalog.Load(readFile(AscendancyCatalog.FileName));
            ascendancy.RequireKnownClasses();
            var prestige = PrestigeRules.Load(readFile(PrestigeRules.FileName));
            var artifacts = ArtifactCatalog.Load(readFile(ArtifactCatalog.FileName));
            var party = PartyRules.Load(readFile(PartyRules.FileName));
            var characters = CharacterCatalog.Load(readFile(CharacterCatalog.FileName));
            characters.RequireRoster(party.Search.Roles);
            characters.RequirePaths(ascendancy);
            var tutorial = TutorialRules.Load(readFile(TutorialRules.FileName));
            var synergy = SynergyRules.Load(readFile(SynergyRules.FileName));
            synergy.RequirePartyRoles(party.Search.RoleOf);
            var rift = RiftRules.Load(readFile(RiftRules.FileName));
            rift.RequireRoles(synergy);
            var weapons = WeaponCatalog.Load(readFile(WeaponCatalog.FileName));
            weapons.BindGems(gems);
            SkillRoster.Require(gems, weapons.Types);
            ascendancy.RequirePathSkills(gems, weapons);
            var items = ItemCatalog.Load(readFile(ItemCatalog.FileName));
            items.BindGems(gems);
            var drops = DropRules.Load(readFile(DropRules.FileName));
            var panel = PanelUnlocks.Load(readFile(PanelUnlocks.FileName));
            var changeLog = ChangeLog.Load(readFile(ChangeLog.FileName));
            var chronicle = ChronicleCatalog.Load(readFile(ChronicleCatalog.FileName));
            var atlas = AtlasCatalog.Load(readFile(AtlasCatalog.FileName));
            var resonance = ResonanceCatalog.Load(readFile(ResonanceCatalog.FileName));
            var spirits = SpiritCatalog.Load(readFile(SpiritCatalog.FileName));
            var unlocks = UnlockRules.Load(readFile(UnlockRules.FileName));
            var dungeons = DungeonRules.Load(readFile(DungeonRules.FileName));
            var raid = RaidRules.Load(readFile(RaidRules.FileName));
            var clanTree = ClanTreeCatalog.Load(readFile(ClanTreeCatalog.FileName));
            var mentor = MentorRules.Load(readFile(MentorRules.FileName));
            var slates = SlateRules.Load(readFile(SlateRules.FileName));
            var quests = QuestRules.Load(readFile(QuestRules.FileName));
            var achievements = AchievementCatalog.Load(readFile(AchievementCatalog.FileName));
            var bestiary = BestiaryCatalog.Load(readFile(BestiaryCatalog.FileName));
            var cosmetics = CosmeticCatalog.Load(readFile(CosmeticCatalog.FileName));
            var seasonPass = SeasonPassRules.Load(readFile(SeasonPassRules.FileName), cosmetics);
            var chests = ChestCatalog.Load(readFile(ChestCatalog.FileName));
            var tournaments = TournamentRules.Load(readFile(TournamentRules.FileName));
            var clanCamp = ClanCampRules.Load(readFile(ClanCampRules.FileName));
            var seasonalEvents = EventRules.Load(readFile(EventRules.FileName), cosmetics);
            var peaceful = PeacefulRules.Load(readFile(PeacefulRules.FileName));
            var camp = CampRules.Load(readFile(CampRules.FileName));
            var garden = GardenRules.Load(readFile(GardenRules.FileName));
            var alchemy = AlchemyRules.Load(readFile(AlchemyRules.FileName));
            var expeditions = ExpeditionRules.Load(readFile(ExpeditionRules.FileName), stages.Biomes);
            var fishing = FishingRules.Load(readFile(FishingRules.FileName), camp, stages.Biomes);
            var cooking = CookingRules.Load(readFile(CookingRules.FileName), camp, alchemy);
            var forge = ForgeRules.Load(readFile(ForgeRules.FileName), camp, stages.Biomes);
            var digs = DigRules.Load(readFile(DigRules.FileName), camp, stages.Biomes);
            var museum = MuseumRules.Load(readFile(MuseumRules.FileName), fishing, garden, cooking, alchemy, expeditions, digs);
            var decor = DecorRules.Load(readFile(DecorRules.FileName), camp);
            var town = TownRules.Load(readFile(TownRules.FileName), alchemy, garden, stages.Biomes);
            var characterIds = new List<string>();
            foreach (var character in characters.All)
                characterIds.Add(character.Id);
            var guilds = GuildRules.Load(readFile(GuildRules.FileName), stages.Biomes, characterIds);
            var talents = TalentRules.Load(readFile(TalentRules.FileName));
            var hideout = HideoutRules.Load(readFile(HideoutRules.FileName));
            var campLevels = CampLevelRules.Load(readFile(CampLevelRules.FileName), camp);
            var apprentices = ApprenticeRules.Load(readFile(ApprenticeRules.FileName), camp, alchemy);
            var campModules = CampModuleRules.Load(readFile(CampModuleRules.FileName), campLevels);
            var strings = StringTable.Load(readFile(StringTable.FileName));
            Texts.Use(strings);
            var treeTemplates = TreeTemplates.Load(readFile(TreeTemplates.FileName));
            var classTrees = new Dictionary<string, ClassTree>(StringComparer.Ordinal);
            foreach (var heroClass in treeTemplates.Classes)
            {
                classTrees[heroClass] = ClassTreeLoader.Load(treeTemplates, heroClass, readFile(ClassTreeLoader.FileNameOf(heroClass)));
                classTrees[heroClass].RequireKnownSkills(gems);
            }
            var commonTrees = new Dictionary<string, ClassTree>(StringComparer.Ordinal);
            foreach (var treeId in treeTemplates.CommonTrees)
                commonTrees[treeId] = ClassTreeLoader.LoadCommon(treeTemplates, treeId, readFile(ClassTreeLoader.CommonFileNameOf(treeId)));
            return new GameData(stages, enemies, inputCaps, combat, gems, skillBook, treePoints, ascendancy, prestige, artifacts, party, tutorial, strings, synergy, rift, weapons, items, drops, panel, changeLog, chronicle, atlas, resonance, spirits) { Characters = characters, Unlocks = unlocks, Slates = slates, Quests = quests, Achievements = achievements, Bestiary = bestiary, Cosmetics = cosmetics, SeasonPass = seasonPass, Chests = chests, Tournaments = tournaments, ClanCamp = clanCamp, Guilds = guilds, Talents = talents, Hideout = hideout, CampLevels = campLevels, Apprentices = apprentices, CampModules = campModules, Digs = digs, Events = seasonalEvents, Peaceful = peaceful, Camp = camp, Garden = garden, Alchemy = alchemy, Expeditions = expeditions, Town = town, Fishing = fishing, Cooking = cooking, Forge = forge, Museum = museum, Decor = decor, Dungeons = dungeons, Raid = raid, ClanTree = clanTree, Mentor = mentor, TreeTemplates = treeTemplates, ClassTrees = classTrees, CommonTrees = commonTrees };
        }
    }
}
