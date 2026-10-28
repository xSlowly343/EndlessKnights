using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class PartyColors
    {
        public static readonly Color[] Allies =
        {
            new Color32(0x3F, 0x6F, 0xD1, 0xFF),
            new Color32(0x4F, 0xC2, 0x77, 0xFF),
            new Color32(0xB0, 0x7C, 0xFF, 0xFF),
            new Color32(0xFF, 0x9A, 0x3C, 0xFF),
        };

        public static Color Of(int index) => Allies[((index % Allies.Length) + Allies.Length) % Allies.Length];
    }
}
