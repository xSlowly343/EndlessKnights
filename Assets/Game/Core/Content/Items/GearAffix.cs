using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class GearAffix
    {
        public GearAffix(AffixDef def, int tier, double value)
        {
            Def = def;
            Tier = tier;
            Value = value;
        }

        public AffixDef Def { get; }
        public int Tier { get; }
        public double Value { get; }

        public void AddModifiers(List<Modifier> modifiers)
        {
            foreach (var stat in Def.Stats)
                modifiers.Add(new Modifier(stat.Stat, stat.Kind, Value, stat.Tags));
        }
    }
}
