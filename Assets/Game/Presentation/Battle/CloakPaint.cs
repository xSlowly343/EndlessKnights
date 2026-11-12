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
        Stars,
        Pearl,
        Aurora,
        Embers,
        Twinkle,
        Lightning,
        Frost,
        Ripple,
        Thread
    }

    public readonly struct CloakPaint
    {
        public CloakPaint(Color first, Color second, CloakPattern pattern)
            : this(first, second, second, pattern, 1, 0f)
        {
        }

        public CloakPaint(Color first, Color second, Color third, CloakPattern pattern, int frames, float seconds)
        {
            First = first;
            Second = second;
            Third = third;
            Pattern = pattern;
            Frames = frames < 1 ? 1 : frames;
            Seconds = seconds;
        }

        public Color First { get; }
        public Color Second { get; }
        public Color Third { get; }
        public CloakPattern Pattern { get; }
        public int Frames { get; }
        public float Seconds { get; }
        public bool Animated => Frames > 1 && Seconds > 0f;

        public static CloakPattern PatternOf(string name)
        {
            switch (name)
            {
                case "gradient": return CloakPattern.Gradient;
                case "trim": return CloakPattern.Trim;
                case "duotone": return CloakPattern.Duotone;
                case "stripes": return CloakPattern.Stripes;
                case "stars": return CloakPattern.Stars;
                case "pearl": return CloakPattern.Pearl;
                case "aurora": return CloakPattern.Aurora;
                case "embers": return CloakPattern.Embers;
                case "twinkle": return CloakPattern.Twinkle;
                case "lightning": return CloakPattern.Lightning;
                case "frost": return CloakPattern.Frost;
                case "ripple": return CloakPattern.Ripple;
                case "thread": return CloakPattern.Thread;
                default: return CloakPattern.Plain;
            }
        }
    }
}
