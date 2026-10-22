using System;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;

namespace Game.Core.Content
{
    public sealed class GameData
    {
        public static readonly string[] FileNames = { StageTable.FileName, EnemyCatalog.FileName, InputCaps.FileName, CombatRules.FileName, GemCatalog.FileName, SkillBookRules.FileName, PassiveTree.FileName, PrestigeRules.FileName };

        private GameData(StageTable stages, EnemyCatalog enemies, InputCaps inputCaps, CombatRules combat, GemCatalog gems, SkillBookRules skillBook, PassiveTree tree, PrestigeRules prestige)
        {
            Stages = stages;
            Enemies = enemies;
            InputCaps = inputCaps;
            Combat = combat;
            Gems = gems;
            Tree = tree;
            SkillBook = skillBook;
            Prestige = prestige;
        }

        public StageTable Stages { get; }
        public EnemyCatalog Enemies { get; }
        public InputCaps InputCaps { get; }
        public CombatRules Combat { get; }
        public GemCatalog Gems { get; }
        public PassiveTree Tree { get; }
        public SkillBookRules SkillBook { get; }
        public PrestigeRules Prestige { get; }

        public static GameData Load(Func<string, string> readFile)
        {
            var stages = StageTable.Load(readFile(StageTable.FileName));
            var enemies = EnemyCatalog.Load(readFile(EnemyCatalog.FileName), stages.Biomes);
            var inputCaps = InputCaps.Load(readFile(InputCaps.FileName));
            var combat = CombatRules.Load(readFile(CombatRules.FileName));
            var gems = GemCatalog.Load(readFile(GemCatalog.FileName));
            foreach (var gem in gems.All)
            {
                if (gem.Skill != null && gem.Skill.NoteCost > combat.Classes.NotesPerMelody)
                    throw new ContentException(GemCatalog.FileName, gem.Id, $"costs {gem.Skill.NoteCost} notes, but a melody holds only {combat.Classes.NotesPerMelody} ({CombatRules.FileName})");
            }
            var skillBook = SkillBookRules.Load(readFile(SkillBookRules.FileName), gems);
            var tree = PassiveTree.Load(readFile(PassiveTree.FileName));
            var prestige = PrestigeRules.Load(readFile(PrestigeRules.FileName));
            return new GameData(stages, enemies, inputCaps, combat, gems, skillBook, tree, prestige);
        }
    }
}
