using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public static class HeroAttributes
    {
        public static readonly IReadOnlyList<string> Names = new[] { "strength", "dexterity", "intelligence" };

        public static StatId Stat(string attribute) => new StatId(attribute);
    }
}
