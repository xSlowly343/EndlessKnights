using Game.Core.Settings;

namespace Game.Core.Ui
{
    public readonly struct EffectLevels
    {
        public const double SavingCap = 0.5;
        public const int MaxWeatherLayers = 3;
        public const int MaxBackgroundCreatures = 6;
        public const int MaxGroundCreatures = 3;
        public const double OneLayerUpTo = 0.5;
        public const double TwoLayersUpTo = 0.8;
        public const double ReactionsFrom = 0.25;
        public const double RareScenesFrom = 0.5;
        public const double StripCpuPercent = 2;
        public const double BattleCpuPercent = 2;
        public const double WorldCpuPercent = 2;

        private EffectLevels(double world, double battle, int weatherLayers, int backgroundCreatures, int groundCreatures, bool dayNight, bool reactions, bool rareScenes)
        {
            World = world;
            Battle = battle;
            WeatherLayers = weatherLayers;
            BackgroundCreatures = backgroundCreatures;
            GroundCreatures = groundCreatures;
            DayNight = dayNight;
            Reactions = reactions;
            RareScenes = rareScenes;
        }

        public double World { get; }
        public double Battle { get; }
        public int WeatherLayers { get; }
        public int BackgroundCreatures { get; }
        public int GroundCreatures { get; }
        public bool DayNight { get; }
        public bool Reactions { get; }
        public bool RareScenes { get; }

        public bool WorldShown => WeatherLayers > 0 || BackgroundCreatures > 0 || GroundCreatures > 0 || Reactions || RareScenes;

        public double CpuPercent => StripCpuPercent + BattleCpuPercent * Battle + WorldCpuPercent * WorldShare;

        public static EffectLevels Of(GameSettings settings, bool savingPower)
        {
            var capped = savingPower && settings.BatterySaver;
            var world = Capped(settings.LivingWorld, capped);
            var battle = Capped(settings.BattleEffects, capped);
            var layers = !settings.WeatherShown || world <= 0 ? 0 : world <= OneLayerUpTo ? 1 : world <= TwoLayersUpTo ? 2 : MaxWeatherLayers;
            var background = settings.WildlifeShown ? (int)System.Math.Floor(MaxBackgroundCreatures * world + 1e-9) : 0;
            var ground = settings.WildlifeShown ? (int)System.Math.Floor(MaxGroundCreatures * world + 1e-9) : 0;
            return new EffectLevels(world, battle, layers, background, ground, settings.DayNightShown,
                settings.WorldReactions && world >= ReactionsFrom, settings.RareScenes && world >= RareScenesFrom);
        }

        private double WorldShare
        {
            get
            {
                var parts = WeatherLayers / (double)MaxWeatherLayers * 0.5
                    + (BackgroundCreatures + GroundCreatures) / (double)(MaxBackgroundCreatures + MaxGroundCreatures) * 0.3
                    + (Reactions ? 0.1 : 0) + (RareScenes ? 0.05 : 0) + (DayNight && WorldShown ? 0.05 : 0);
                return System.Math.Min(1, parts);
            }
        }

        private static double Capped(double value, bool capped)
        {
            var clamped = System.Math.Min(1, System.Math.Max(0, value));
            return capped ? System.Math.Min(SavingCap, clamped) : clamped;
        }
    }
}
