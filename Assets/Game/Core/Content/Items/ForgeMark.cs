using System.Collections.Generic;

namespace Game.Core.Content.Items
{
    public sealed class ForgeMark
    {
        public const int SlainCapacity = 8;

        public double Temper { get; set; }
        public string Brand { get; set; } = string.Empty;
        public List<string> Slain { get; } = new List<string>(SlainCapacity);
        public double Scale => 1 + Temper;
        public bool Empty => Temper <= 0 && Brand.Length == 0 && Slain.Count == 0;
    }
}
