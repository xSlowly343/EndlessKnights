using Game.Core.Content.Gems;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class GemColors
    {
        public static readonly Color Red = new Color(0.8f, 0.25f, 0.22f);
        public static readonly Color Green = new Color(0.25f, 0.65f, 0.3f);
        public static readonly Color Blue = new Color(0.25f, 0.45f, 0.85f);
        public static readonly Color White = new Color(0.7f, 0.7f, 0.72f);

        public static Color Of(GemColor color) => color switch
        {
            GemColor.Red => Red,
            GemColor.Green => Green,
            GemColor.Blue => Blue,
            _ => White
        };
    }
}
