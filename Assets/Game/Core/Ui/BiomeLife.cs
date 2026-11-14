using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public enum LifeMood
    {
        Calm,
        Friendly,
        Alarmed,
        Hidden
    }

    public enum LifeReaction
    {
        EnemyFlash,
        BossOut,
        BossDown,
        GuardianCrack,
        PrestigeWave,
        RestVisit
    }

    public sealed class BiomeLife
    {
        public const int MaxBackground = EffectLevels.MaxBackgroundCreatures;
        public const int MaxGround = EffectLevels.MaxGroundCreatures;

        private static readonly int ReactionKinds = System.Enum.GetValues(typeof(LifeReaction)).Length;

        private readonly BiomeLifeRules _rules;
        private readonly bool[] _reactions = new bool[ReactionKinds];
        private readonly double[] _backgroundX = new double[MaxBackground];
        private readonly double[] _groundX = new double[MaxGround];
        private WorldSpan _span;
        private WorldSpan _rare;
        private BiomeLook? _spanLook;
        private bool _reactionsOn;

        public BiomeLife(BiomeLifeRules rules)
        {
            _rules = rules;
            Look = rules.Looks[0];
        }

        public BiomeLook Look { get; private set; }
        public int Weather { get; private set; }
        public int PreviousWeather { get; private set; }
        public double WeatherBlend { get; private set; } = 1;
        public int WeatherLayers { get; private set; }
        public double Strength { get; private set; } = 1;
        public double FrontCover => _rules.FrontCover;
        public bool Special { get; private set; }
        public DayLight Light { get; private set; } = DayLight.Steady(DayPhase.Day);
        public LifeMood Mood { get; private set; }
        public int Sway { get; private set; }
        public int BackgroundCount { get; private set; }
        public int GroundCount { get; private set; }
        public bool RareScene { get; private set; }
        public double RareProgress { get; private set; }

        public string WeatherId => Look.Weathers[Weather];
        public IReadOnlyList<string> Background => Light.IsNight ? Look.NightBackground : Look.DayBackground;
        public IReadOnlyList<string> Ground => Light.IsNight ? Look.NightGround : Look.DayGround;

        public string BackgroundAt(int slot) => Background[(int)((_span.Index + slot) % Background.Count)];

        public string GroundAt(int slot) => Ground[(int)((_span.Index + slot) % Ground.Count)];

        public double BackgroundX(int slot) => _backgroundX[slot];

        public double GroundX(int slot) => _groundX[slot];

        public void Read(GameState state, EffectLevels levels, long serverUtcMs, double serverDayHours)
        {
            var look = state.Rift.Inside ? _rules.RiftLook : _rules.LookOf(state.Biome);
            if (look != _spanLook || !_span.Holds(serverUtcMs))
                Turn(look, serverUtcMs);
            Look = look;
            Weather = _span.Pick;
            PreviousWeather = _span.PreviousPick < 0 ? _span.Pick : _span.PreviousPick;
            var fadeMs = _rules.WeatherFadeSeconds * 1000;
            WeatherBlend = fadeMs <= 0 ? 1 : System.Math.Min(1, (serverUtcMs - _span.StartMs) / fadeMs);
            WeatherLayers = levels.WeatherLayers;
            Light = look.AlwaysNight ? DayLight.Steady(DayPhase.Night) : levels.DayNight ? _rules.LightAt(serverDayHours) : DayLight.Steady(DayPhase.Day);
            _reactionsOn = levels.Reactions;
            var mood = MoodOf(state);
            if (mood == LifeMood.Friendly && Mood != LifeMood.Friendly)
                Note(LifeReaction.RestVisit);
            Mood = mood;
            Strength = levels.World * (state.EnemyType == EnemyType.Normal ? 1 : 1 - _rules.BossDim);
            Sway = _reactionsOn ? _rules.StreakTier(state.InputStreakTicks * GameLoop.TickSeconds) : 0;
            BackgroundCount = Mood == LifeMood.Hidden || Background.Count == 0 ? 0 : levels.BackgroundCreatures;
            GroundCount = Mood == LifeMood.Hidden || Ground.Count == 0 ? 0 : levels.GroundCreatures;
            Special = WeatherLayers > 0 && SpecialNow(look.Special, serverUtcMs);
            var sinceRare = serverUtcMs - _rare.StartMs;
            RareScene = levels.RareScenes && look.Rare.Length > 0 && Mood != LifeMood.Hidden && Mood != LifeMood.Alarmed && sinceRare >= 0 && sinceRare < _rules.RareShowMs;
            RareProgress = RareScene ? sinceRare / (double)_rules.RareShowMs : 0;
        }

        public void Note(in GameEvent gameEvent)
        {
            switch (gameEvent.Kind)
            {
                case GameEventKind.EnemySpawned:
                    Note(LifeReaction.EnemyFlash);
                    break;
                case GameEventKind.BossStarted:
                    Note(gameEvent.Enemy != null && gameEvent.Enemy.Type == EnemyType.Guardian ? LifeReaction.GuardianCrack : LifeReaction.BossOut);
                    break;
                case GameEventKind.GateNeedsLimitFight:
                    Note(LifeReaction.GuardianCrack);
                    break;
                case GameEventKind.BossDefeated:
                    Note(LifeReaction.BossDown);
                    break;
                case GameEventKind.Prestige:
                    Note(LifeReaction.PrestigeWave);
                    break;
            }
        }

        public bool TryTakeReaction(out LifeReaction reaction)
        {
            for (var i = 0; i < ReactionKinds; i++)
            {
                if (!_reactions[i])
                    continue;
                _reactions[i] = false;
                reaction = (LifeReaction)i;
                return true;
            }
            reaction = default;
            return false;
        }

        private void Note(LifeReaction reaction)
        {
            if (_reactionsOn)
                _reactions[(int)reaction] = true;
        }

        private LifeMood MoodOf(GameState state)
        {
            if (!_reactionsOn)
                return LifeMood.Calm;
            if (state.EnemyType == EnemyType.Guardian)
                return LifeMood.Hidden;
            if (state.EnemyType == EnemyType.Boss)
                return LifeMood.Alarmed;
            return state.IdleTicks * GameLoop.TickSeconds >= _rules.RestSeconds ? LifeMood.Friendly : LifeMood.Calm;
        }

        private bool SpecialNow(LifeSpecial? special, long serverUtcMs)
        {
            if (special == null)
                return false;
            if (special.After >= 0 && (_span.PreviousPick != special.After || _span.Pick == special.After || serverUtcMs - _span.StartMs >= special.AfterMs))
                return false;
            if (special.During >= 0 && _span.Pick != special.During)
                return false;
            return special.Phase == null || Light.To == special.Phase.Value;
        }

        private void Turn(BiomeLook look, long serverUtcMs)
        {
            _spanLook = look;
            _span = WorldWeather.At(look, _rules, serverUtcMs);
            _rare = WorldWeather.RareAt(look, _rules, serverUtcMs);
            for (var i = 0; i < MaxBackground; i++)
                _backgroundX[i] = Spread(look, i, MaxBackground, 0);
            for (var i = 0; i < MaxGround; i++)
                _groundX[i] = Spread(look, i, MaxGround, 100);
        }

        private double Spread(BiomeLook look, int slot, int slots, int salt)
        {
            var lane = _rules.FreeWidthPx / slots;
            return lane * (slot + 0.15 + 0.7 * WorldWeather.Scatter(look.Seed, _span.Index, slot + salt));
        }
    }
}
