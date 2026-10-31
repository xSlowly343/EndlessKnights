using System.Collections.Generic;
using Game.Core.Content.Slates;

namespace Game.Core.Sim
{
    public sealed class SlateItem
    {
        public SlateItem(SlateShape shape, SlateTheme theme, SlateRarity rarity, IReadOnlyList<SlateLine> lines, bool tempered)
        {
            Shape = shape;
            Theme = theme;
            Rarity = rarity;
            Lines = new List<SlateLine>(lines);
            Tempered = tempered;
        }

        public SlateShape Shape { get; }
        public SlateTheme Theme { get; }
        public SlateRarity Rarity { get; }
        public List<SlateLine> Lines { get; }
        public bool Tempered { get; set; }
    }
}
