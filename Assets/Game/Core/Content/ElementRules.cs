using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public enum Element
    {
        Fire,
        Air,
        Earth,
        Lightning,
        Water,
        Corrosion
    }

    public sealed class ElementRules
    {
        public const int Count = 6;
        public const int None = -1;

        private static readonly string[] Ids = { "fire", "air", "earth", "lightning", "water", "corrosion" };

        public static readonly StatId[] Conversion = Stats("Conversion");
        public static readonly StatId[] Damage = Stats("Damage");
        public static readonly StatId[] Pen = Stats("Pen");

        private readonly Dictionary<string, (int Resist, int Weak)> _biomes = new Dictionary<string, (int, int)>();

        public double Resist { get; private set; }
        public double Weak { get; private set; }
        public double MinResist { get; private set; } = -0.5;
        public double BurnShare { get; private set; }
        public double BurnSeconds { get; private set; } = 3;
        public double AirShare { get; private set; }
        public double EarthShred { get; private set; }
        public double EarthMax { get; private set; }
        public double LightningChance { get; private set; }
        public double LightningPower { get; private set; }
        public double WaterSeconds { get; private set; }
        public double WaterMax { get; private set; }
        public double CorrosionChance { get; private set; }

        public static string IdOf(Element element) => Ids[(int)element];

        public (int Resist, int Weak) Of(string biome) =>
            _biomes.TryGetValue(biome, out var pair) ? pair : (None, None);

        public double Resistance(int element, int resisted, int weak) =>
            element == resisted ? Resist : element == weak ? -Weak : 0;

        private static StatId[] Stats(string suffix)
        {
            var stats = new StatId[Count];
            for (var i = 0; i < Count; i++)
                stats[i] = new StatId(Ids[i] + suffix);
            return stats;
        }

        private static int Parse(ContentNode node)
        {
            var id = node.AsString();
            var index = System.Array.IndexOf(Ids, id);
            if (index < 0)
                throw node.Error($"unknown element {id}");
            return index;
        }

        internal static ElementRules Read(ContentNode node)
        {
            var effects = node.Field("effects");
            var rules = new ElementRules
            {
                Resist = node.Field("resist").AsNumber(0, 1),
                Weak = node.Field("weak").AsNumber(0, 1),
                MinResist = -node.Field("minResist").AsNumber(0, 1),
                BurnShare = effects.Field("fire").Field("share").AsNumber(0, 10),
                BurnSeconds = effects.Field("fire").Field("seconds").AsNumber(0.1, 60),
                AirShare = effects.Field("air").Field("share").AsNumber(0, 1),
                EarthShred = effects.Field("earth").Field("perHit").AsNumber(0, 1),
                EarthMax = effects.Field("earth").Field("max").AsNumber(0, 1),
                LightningChance = effects.Field("lightning").Field("chance").AsNumber(0, 1),
                LightningPower = effects.Field("lightning").Field("power").AsNumber(0, 10),
                WaterSeconds = effects.Field("water").Field("perHit").AsNumber(0, 10),
                WaterMax = effects.Field("water").Field("max").AsNumber(0, 600),
                CorrosionChance = effects.Field("corrosion").Field("chance").AsNumber(0, 1)
            };
            foreach (var biome in node.Field("biomes").Items())
            {
                var id = biome.Field("biome").AsString();
                var resist = Parse(biome.Field("resist"));
                var weak = Parse(biome.Field("weak"));
                if (resist == weak)
                    throw biome.Error($"{id} cannot resist and be weak to one element");
                rules._biomes[id] = (resist, weak);
            }
            return rules;
        }
    }
}
