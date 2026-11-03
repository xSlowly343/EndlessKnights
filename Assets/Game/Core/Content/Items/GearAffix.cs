using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class GearAffix
    {
        public GearAffix(AffixDef def, int tier, double value, Gems.GemDef? support = null)
        {
            Def = def;
            Tier = tier;
            Value = value;
            Support = support;
        }

        public AffixDef Def { get; }
        public int Tier { get; }
        public double Value { get; }
        public Gems.GemDef? Support { get; }

        public void AddModifiers(List<Modifier> modifiers)
        {
            for (var i = 0; i < Def.Stats.Count; i++)
            {
                var stat = Def.Stats[i];
                modifiers.Add(new Modifier(stat.Stat, stat.Kind, Def.StatValue(i, Tier, Value), stat.Tags));
            }
        }
    }
}
