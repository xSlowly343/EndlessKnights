using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class CookingState
    {
        public string Cooking { get; set; } = string.Empty;
        public double Chops { get; set; }
        public double Plates { get; set; }
        public List<string> Meals { get; } = new List<string>();
        public List<string> SharedDishes { get; } = new List<string>();
        public string Active { get; set; } = string.Empty;
        public long ActiveTicksLeft { get; set; }
        public HashSet<string> Book { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public long Cooked { get; set; }
    }
}
