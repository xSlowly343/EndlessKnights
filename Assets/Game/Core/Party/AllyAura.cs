using Game.Core.Content.Gems;

namespace Game.Core.Party
{
    public sealed class AllyAura
    {
        public GemDef? Gem { get; set; }
        public double Strength { get; set; }
        public int TicksLeft { get; set; }
        public bool Suppressed { get; set; }

        public bool Active => Gem != null && TicksLeft > 0;

        public void Clear()
        {
            Gem = null;
            Strength = 0;
            TicksLeft = 0;
            Suppressed = false;
        }
    }
}
