using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class EnemyDef
    {
        public EnemyDef(string id, string biome, EnemyType type, double hpMultiplier, double goldMultiplier, string sprite, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            Biome = biome;
            Type = type;
            HpMultiplier = hpMultiplier;
            GoldMultiplier = goldMultiplier;
            Sprite = sprite;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string Biome { get; }
        public EnemyType Type { get; }
        public double HpMultiplier { get; }
        public double GoldMultiplier { get; }
        public string Sprite { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }
}
