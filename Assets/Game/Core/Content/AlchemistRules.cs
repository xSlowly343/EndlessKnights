using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class AlchemistRules
    {
        public const int Colors = 3;
        public const int Recipes = 64;

        public static readonly AlchemistRules None = new AlchemistRules();

        private readonly AlchemistMixEffect[][] _mixes = new AlchemistMixEffect[Recipes][];

        private AlchemistRules()
        {
            for (var i = 0; i < Recipes; i++)
                _mixes[i] = System.Array.Empty<AlchemistMixEffect>();
        }

        public int ReagentHits { get; private set; } = 8;
        public int ReagentHitsMin { get; private set; } = 2;
        public double PureBoost { get; private set; } = 2;
        public double ChainBlast { get; private set; } = 3;
        public double QueueBlast { get; private set; } = 5;
        public int LuckyEvery { get; private set; } = 5;
        public double PhilosopherBoost { get; private set; } = 3;
        public double FlaskBoost { get; private set; } = 1.5;
        public double MixEffectSeconds { get; private set; } = 2;
        public int Layers { get; private set; } = 5;
        public int LayerHits { get; private set; } = 30;
        public int LayerHitsMin { get; private set; } = 5;
        public double LayerBlast { get; private set; } = 8;
        public double CoreBonus { get; private set; } = 0.25;
        public double AcidSeconds { get; private set; } = 10;
        public double AcidSoloShare { get; private set; } = 1;
        public double SkillLayerHits { get; private set; } = 5;
        public double SolventEffectSeconds { get; private set; } = 2;
        public int ElixirHits { get; private set; } = 50;
        public int ElixirHitsMin { get; private set; } = 10;
        public double ElixirSeconds { get; private set; } = 8;
        public double PhilosopherSeconds { get; private set; } = 5;
        public double StoneSeconds { get; private set; } = 8;
        public int PhilosopherEvery { get; private set; } = 3;
        public int RedEvery { get; private set; } = 5;
        public double GoldBonus { get; private set; } = 0.5;
        public double SelfPenalty { get; private set; } = 0.25;

        public IReadOnlyList<AlchemistMixEffect> Mix(int recipe) => _mixes[recipe];

        public static int RecipeOf(int red, int blue, int gold) => red * 16 + blue * 4 + gold;

        public static AlchemistRules Read(ContentNode node)
        {
            var mix = node.Field("mix");
            var solvent = node.Field("solvent");
            var elixir = node.Field("elixir");
            var rules = new AlchemistRules
            {
                ReagentHits = mix.Field("reagentHits").AsInt(1, 10_000),
                ReagentHitsMin = mix.Field("reagentHitsMin").AsInt(1, 10_000),
                PureBoost = mix.Field("pureBoost").AsNumber(1, 100),
                ChainBlast = mix.Field("chainBlast").AsNumber(0, 1000),
                QueueBlast = mix.Field("queueBlast").AsNumber(0, 1000),
                LuckyEvery = mix.Field("luckyEvery").AsInt(1, 1000),
                PhilosopherBoost = mix.Field("philosopherBoost").AsNumber(1, 100),
                FlaskBoost = mix.Field("flaskBoost").AsNumber(1, 100),
                MixEffectSeconds = mix.Field("effectSeconds").AsNumber(0.1, 60),
                Layers = solvent.Field("layers").AsInt(1, 20),
                LayerHits = solvent.Field("layerHits").AsInt(1, 10_000),
                LayerHitsMin = solvent.Field("layerHitsMin").AsInt(1, 10_000),
                LayerBlast = solvent.Field("layerBlast").AsNumber(0, 1000),
                CoreBonus = solvent.Field("coreBonus").AsNumber(0, 10),
                AcidSeconds = solvent.Field("acidSeconds").AsNumber(0.1, 600),
                AcidSoloShare = solvent.Field("acidSoloShare").AsNumber(0, 10),
                SkillLayerHits = solvent.Field("skillLayerHits").AsNumber(0, 1000),
                SolventEffectSeconds = solvent.Field("effectSeconds").AsNumber(0.1, 60),
                ElixirHits = elixir.Field("hits").AsInt(1, 10_000),
                ElixirHitsMin = elixir.Field("hitsMin").AsInt(1, 10_000),
                ElixirSeconds = elixir.Field("seconds").AsNumber(0.1, 600),
                PhilosopherSeconds = elixir.Field("philosopherSeconds").AsNumber(0.1, 600),
                StoneSeconds = elixir.Field("stoneSeconds").AsNumber(0.1, 600),
                PhilosopherEvery = elixir.Field("philosopherEvery").AsInt(1, 100),
                RedEvery = elixir.Field("redEvery").AsInt(1, 100),
                GoldBonus = elixir.Field("goldBonus").AsNumber(0, 100),
                SelfPenalty = elixir.Field("selfPenalty").AsNumber(0, 1)
            };
            var recipes = mix.Field("recipes");
            foreach (var key in recipes.FieldNames())
                rules._mixes[ParseRecipe(recipes, key)] = ReadEffects(recipes.Field(key));
            return rules;
        }

        private static int ParseRecipe(ContentNode recipes, string key)
        {
            if (key.Length != 3)
                throw recipes.Field(key).Error($"a recipe is three reagents of r, b and g, not '{key}'");
            int red = 0, blue = 0, gold = 0;
            foreach (var reagent in key)
            {
                if (reagent == 'r')
                    red++;
                else if (reagent == 'b')
                    blue++;
                else if (reagent == 'g')
                    gold++;
                else
                    throw recipes.Field(key).Error($"unknown reagent '{reagent}'");
            }
            return RecipeOf(red, blue, gold);
        }

        private static AlchemistMixEffect[] ReadEffects(ContentNode node)
        {
            var effects = new List<AlchemistMixEffect>();
            foreach (var item in node.Items())
                effects.Add(new AlchemistMixEffect(ReadKind(item.Field("kind")), item.Field("value").AsNumber(0, 1000),
                    item.HasField("seconds") ? item.Field("seconds").AsNumber(0, 600) : 0));
            return effects.ToArray();
        }

        private static AlchemistMixKind ReadKind(ContentNode node) => node.AsString() switch
        {
            "blast" => AlchemistMixKind.Blast,
            "corrode" => AlchemistMixKind.Corrode,
            "resource" => AlchemistMixKind.Resource,
            "stop" => AlchemistMixKind.Stop,
            "crit" => AlchemistMixKind.Crit,
            "gold" => AlchemistMixKind.Gold,
            "cut" => AlchemistMixKind.Cut,
            _ => throw node.Error($"unknown mix effect '{node.AsString()}'")
        };
    }
}
