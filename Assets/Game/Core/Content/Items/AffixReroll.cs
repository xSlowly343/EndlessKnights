using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Content.Items
{
    public static class AffixReroll
    {
        public static bool CanReroll(IReadOnlyList<GearAffix> affixes, int? locked)
        {
            for (var i = 0; i < affixes.Count; i++)
            {
                if (i != locked && affixes[i].Def.Spreads(affixes[i].Tier))
                    return true;
            }
            return false;
        }

        public static IReadOnlyList<GearAffix> Rerolled(IReadOnlyList<GearAffix> affixes, int? locked, Rng rng)
        {
            var rolled = new GearAffix[affixes.Count];
            for (var i = 0; i < affixes.Count; i++)
                rolled[i] = i == locked ? affixes[i] : Rerolled(affixes[i], rng.NextDouble());
            return rolled;
        }

        private static GearAffix Rerolled(GearAffix affix, double roll) =>
            new GearAffix(affix.Def, affix.Tier, affix.Def.RollValue(affix.Tier, roll), affix.Support, roll);
    }
}
