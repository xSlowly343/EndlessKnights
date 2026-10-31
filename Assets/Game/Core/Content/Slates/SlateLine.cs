using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Slates
{
    public sealed class SlateLine
    {
        public SlateLine(string id, IReadOnlyList<Modifier> modifiers, bool whole, HeroClass? heroClass)
        {
            Id = id;
            Modifiers = modifiers;
            Whole = whole;
            HeroClass = heroClass;
        }

        public string Id { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool Whole { get; }
        public HeroClass? HeroClass { get; }

        public double Scaled(double value, double scale)
        {
            if (!Whole)
                return value * scale;
            var magnitude = System.Math.Max(System.Math.Abs(value), System.Math.Round(System.Math.Abs(value * scale)));
            return System.Math.Sign(value) * magnitude;
        }
    }
}
