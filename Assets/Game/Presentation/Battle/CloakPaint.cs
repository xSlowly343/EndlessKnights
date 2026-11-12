using UnityEngine;

namespace Game.Presentation.Battle
{
    public enum CloakPattern
    {
        Plain,
        Gradient,
        Trim,
        Duotone,
        Stripes,
        Stars
    }

    public readonly struct CloakPaint
    {
        public CloakPaint(Color first, Color second, CloakPattern pattern)
        {
            First = first;
            Second = second;
            Pattern = pattern;
        }

        public Color First { get; }
        public Color Second { get; }
        public CloakPattern Pattern { get; }

        public static CloakPattern PatternOf(string name)
        {
            switch (name)
            {
                case "gradient": return CloakPattern.Gradient;
                case "trim": return CloakPattern.Trim;
                case "duotone": return CloakPattern.Duotone;
                case "stripes": return CloakPattern.Stripes;
                case "stars": return CloakPattern.Stars;
                default: return CloakPattern.Plain;
            }
        }
    }
}
