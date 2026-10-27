using System;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Tree;
using Game.Core.Content.Rift;
using Game.Core.Content.Synergy;
using Game.Core.Content.Weapons;
using Game.Core.Party;
using Game.Core.Text;
using Game.Core.Ui;

namespace Game.Core.Content
{
    public sealed class GameData
    {
        public static readonly string[] FileNames = { StageTable.FileName, EnemyCatalog.FileName, InputCaps.FileName, CombatRules.FileName, GemCatalog.FileName, SkillBookRules.FileName, PassiveTree.FileName, TreePointRules.FileName, AscendancyCatalog.FileName, PrestigeRules.FileName, ArtifactCatalog.FileName, PartyRules.FileName, TutorialRules.FileName, StringTable.FileName, SynergyRules.FileName, RiftRules.FileName, WeaponCatalog.FileName, ItemCatalog.FileName, DropRules.FileName, PanelUnlocks.FileName, ChangeLog.FileName };

        private GameData(StageTable stages, EnemyCatalog enemies, InputCaps inputCaps, CombatRules combat, GemCatalog gems, SkillBookRules skillBook, PassiveTree tree, TreePointRules treePoints, AscendancyCatalog ascendancy, PrestigeRules prestige, ArtifactCatalog artifacts, PartyRules party, TutorialRules tutorial, StringTable strings, SynergyRules synergy, RiftRules rift, WeaponCatalog weapons, ItemCatalog items, DropRules drops, PanelUnlocks panel, ChangeLog changeLog)
        {
            Items = items;
            Stages = stages;
            Enemies = enemies;
            InputCaps = inputCaps;
            Combat = combat;
            Gems = gems;
            Tree = tree;
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
        }

        public StageTable Stages { get; }
        public EnemyCatalog Enemies { get; }
        public InputCaps InputCaps { get; }
        public CombatRules Combat { get; }
        public GemCatalog Gems { get; }
        public PassiveTree Tree { get; }
        public TreePointRules TreePoints { get; }
        public AscendancyCatalog Ascendancy { get; }
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

        public static GameData Load(Func<string, string> readFile)
        {
            var stages = StageTable.Load(readFile(StageTable.FileName));
            var enemies = EnemyCatalog.Load(readFile(EnemyCatalog.FileName), stages.Biomes);
            var inputCaps = InputCaps.Load(readFile(InputCaps.FileName));
            var combat = CombatRules.Load(readFile(CombatRules.FileName));
            var gems = GemCatalog.Load(readFile(GemCatalog.FileName));
            var skillBook = SkillBookRules.Load(readFile(SkillBookRules.FileName), gems);
            var tree = PassiveTree.Load(readFile(PassiveTree.FileName));
            var treePoints = TreePointRules.Load(readFile(TreePointRules.FileName));
            var ascendancy = AscendancyCatalog.Load(readFile(AscendancyCatalog.FileName));
            ascendancy.RequireClassesAndIds(tree);
            var prestige = PrestigeRules.Load(readFile(PrestigeRules.FileName));
            var artifacts = ArtifactCatalog.Load(readFile(ArtifactCatalog.FileName));
            var party = PartyRules.Load(readFile(PartyRules.FileName));
            var tutorial = TutorialRules.Load(readFile(TutorialRules.FileName));
            var synergy = SynergyRules.Load(readFile(SynergyRules.FileName));
            synergy.RequireGemMarks(gems);
            var rift = RiftRules.Load(readFile(RiftRules.FileName));
            rift.RequireMarks(synergy);
            var weapons = WeaponCatalog.Load(readFile(WeaponCatalog.FileName));
            weapons.BindGems(gems);
            ascendancy.RequirePathSkills(gems, weapons);
            var items = ItemCatalog.Load(readFile(ItemCatalog.FileName));
            var drops = DropRules.Load(readFile(DropRules.FileName));
            var panel = PanelUnlocks.Load(readFile(PanelUnlocks.FileName));
            var changeLog = ChangeLog.Load(readFile(ChangeLog.FileName));
            var strings = StringTable.Load(readFile(StringTable.FileName));
            Texts.Use(strings);
            return new GameData(stages, enemies, inputCaps, combat, gems, skillBook, tree, treePoints, ascendancy, prestige, artifacts, party, tutorial, strings, synergy, rift, weapons, items, drops, panel, changeLog);
        }
    }
}
