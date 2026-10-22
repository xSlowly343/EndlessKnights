using System;

namespace Game.Core.Content
{
    public sealed class GameData
    {
        public static readonly string[] FileNames = { StageTable.FileName, EnemyCatalog.FileName, InputCaps.FileName, CombatRules.FileName };

        private GameData(StageTable stages, EnemyCatalog enemies, InputCaps inputCaps, CombatRules combat)
        {
            Stages = stages;
            Enemies = enemies;
            InputCaps = inputCaps;
            Combat = combat;
        }

        public StageTable Stages { get; }
        public EnemyCatalog Enemies { get; }
        public InputCaps InputCaps { get; }
        public CombatRules Combat { get; }

        public static GameData Load(Func<string, string> readFile)
        {
            var stages = StageTable.Load(readFile(StageTable.FileName));
            var enemies = EnemyCatalog.Load(readFile(EnemyCatalog.FileName), stages.Biomes);
            var inputCaps = InputCaps.Load(readFile(InputCaps.FileName));
            var combat = CombatRules.Load(readFile(CombatRules.FileName));
            return new GameData(stages, enemies, inputCaps, combat);
        }
    }
}
