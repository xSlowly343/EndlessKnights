using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Rift
{
    public sealed class RiftModifier
    {
        public RiftModifier(string id, IReadOnlyList<string> unlessMarks, IReadOnlyList<string> favours, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            UnlessMarks = unlessMarks;
            Favours = favours;
            Modifiers = modifiers;
            EnemyRegen = Sum(modifiers, "enemyRegen", ModifierKind.Flat);
            BossShield = Sum(modifiers, "bossShield", ModifierKind.Flat);
            ExtraStageEnemies = Sum(modifiers, "stageEnemies", ModifierKind.Increased);
        }

        public string Id { get; }
        public IReadOnlyList<string> UnlessMarks { get; }
        public IReadOnlyList<string> Favours { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public double EnemyRegen { get; }
        public double BossShield { get; }
        public double ExtraStageEnemies { get; }

        private static double Sum(IReadOnlyList<Modifier> modifiers, string stat, ModifierKind kind)
        {
            var sum = 0.0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Kind == kind && modifiers[i].Stat.Equals(new StatId(stat)))
                    sum += modifiers[i].Value;
            }
            return sum;
        }
    }
}
