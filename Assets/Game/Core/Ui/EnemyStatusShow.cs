using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public enum EnemyStatus
    {
        Poison,
        Burn,
        Ember,
        Curse,
        Mark,
        Stun,
        Blind,
        Fracture,
        Vulnerable,
        EarthShred,
        WaterTime,
        Weakened
    }

    public enum StatusOutline
    {
        None,
        Curse,
        Vulnerable
    }

    public enum StatusFlash
    {
        Lightning,
        Air,
        Corrosion,
        FractureChip,
        MarkOn
    }

    public enum StatusNumber
    {
        None,
        Stacks,
        Percent
    }

    public struct StatusBadge
    {
        public EnemyStatus Status;
        public StatusNumber Kind;
        public int Number;
        public bool Fading;
    }

    public struct StatusCaps
    {
        public double PoisonMax;
        public double BurnMax;
        public int FractureMax;
        public double EarthMax;
        public double WaterMax;
    }

    public sealed class EnemyStatusShow
    {
        public const int StatusCount = 12;
        public const int MaxBadges = 6;
        public const int FadingTicks = GameLoop.TicksPerSecond;
        public const int FlashGapTicks = 3;

        private const int FlashCount = 5;
        private const double LowShare = 0.2;
        private const double HighShare = 0.6;
        private const double Third = 1.0 / 3.0;

        private readonly int[] _tiers = new int[StatusCount];
        private readonly bool[] _fading = new bool[StatusCount];
        private readonly long[] _since = new long[StatusCount];
        private readonly StatusBadge[] _badges = new StatusBadge[MaxBadges];
        private readonly bool[] _flashWaiting = new bool[FlashCount];
        private long _stamp;
        private long _enemySpawnTick = -1;
        private long _discharges;
        private long _airCarries;
        private long _corrosionFractures;
        private long _fracturesLaid;
        private long _lastFlashTick = long.MinValue / 2;
        private bool _started;

        public int BadgeCount { get; private set; }
        public StatusOutline Outline { get; private set; }
        public int Resists { get; private set; } = ElementRules.None;
        public int WeakTo { get; private set; } = ElementRules.None;

        public int Tier(EnemyStatus status) => _tiers[(int)status];

        public bool Has(EnemyStatus status) => _tiers[(int)status] > 0;

        public bool Fading(EnemyStatus status) => _fading[(int)status];

        public bool ShowsBody(EnemyStatus status) => Has(status) && (status != EnemyStatus.Ember || !Has(EnemyStatus.Burn));

        public ref readonly StatusBadge Badge(int index) => ref _badges[index];

        public void Read(GameState state, in StatusCaps caps)
        {
            var fresh = !_started || state.EnemySpawnTick != _enemySpawnTick;
            var alive = state.EnemyAlive;
            var marked = Has(EnemyStatus.Mark);
            Set(EnemyStatus.Poison, alive ? ShareTier(state.PoisonStacks, caps.PoisonMax, LowShare, HighShare) : 0, false);
            Set(EnemyStatus.Burn, alive ? ShareTier(state.BurnStacks, caps.BurnMax, Third, 2 * Third) : 0, false);
            Set(EnemyStatus.Ember, alive && state.EmberTicks > 0 ? 1 : 0, Ending(state.EmberTicks));
            Set(EnemyStatus.Curse, alive && state.Whisperer.CurseTicks > 0 ? 1 : 0, Ending(state.Whisperer.CurseTicks));
            Set(EnemyStatus.Mark, alive && state.HunterMarkTicks > 0 ? 1 : 0, Ending(state.HunterMarkTicks));
            Set(EnemyStatus.Stun, alive && state.StunTicks > 0 ? 1 : 0, Ending(state.StunTicks));
            Set(EnemyStatus.Blind, alive && state.BlindTicks > 0 ? 1 : 0, Ending(state.BlindTicks));
            Set(EnemyStatus.Fracture, alive ? FractureTier(state.FractureStacks, caps.FractureMax) : 0, false);
            Set(EnemyStatus.Vulnerable, alive && state.VulnerabilityTicks > 0 ? 1 : 0, Ending(state.VulnerabilityTicks));
            Set(EnemyStatus.EarthShred, alive ? ShareTier(state.EarthShred, caps.EarthMax, Third, 2 * Third) : 0, false);
            Set(EnemyStatus.WaterTime, alive && state.FightingBoss && state.WaterSeconds > 0 ? (state.WaterSeconds * 2 < caps.WaterMax ? 1 : 2) : 0, false);
            Set(EnemyStatus.Weakened, alive && state.EnemyWeakened ? 1 : 0, false);
            Outline = Has(EnemyStatus.Curse) ? StatusOutline.Curse : Has(EnemyStatus.Vulnerable) ? StatusOutline.Vulnerable : StatusOutline.None;
            Resists = alive ? state.EnemyResists : ElementRules.None;
            WeakTo = alive ? state.EnemyWeakTo : ElementRules.None;
            FillBadges(state);
            if (fresh)
                System.Array.Clear(_flashWaiting, 0, FlashCount);
            else
            {
                Offer(StatusFlash.Lightning, alive && state.Discharges > _discharges);
                Offer(StatusFlash.Air, alive && state.AirCarries > _airCarries);
                Offer(StatusFlash.Corrosion, alive && state.CorrosionFractures > _corrosionFractures);
                Offer(StatusFlash.FractureChip, alive && state.FracturesLaid > _fracturesLaid && state.CorrosionFractures == _corrosionFractures);
            }
            Offer(StatusFlash.MarkOn, Has(EnemyStatus.Mark) && (!marked || fresh));
            _started = true;
            _enemySpawnTick = state.EnemySpawnTick;
            _discharges = state.Discharges;
            _airCarries = state.AirCarries;
            _corrosionFractures = state.CorrosionFractures;
            _fracturesLaid = state.FracturesLaid;
        }

        public bool TryTakeFlash(long tick, out StatusFlash flash)
        {
            flash = default;
            if (tick - _lastFlashTick < FlashGapTicks)
                return false;
            for (var i = 0; i < FlashCount; i++)
            {
                if (!_flashWaiting[i])
                    continue;
                _flashWaiting[i] = false;
                _lastFlashTick = tick;
                flash = (StatusFlash)i;
                return true;
            }
            return false;
        }

        private void Offer(StatusFlash flash, bool happened)
        {
            if (happened)
                _flashWaiting[(int)flash] = true;
        }

        private void Set(EnemyStatus status, int tier, bool fading)
        {
            var index = (int)status;
            if (tier > 0 && _tiers[index] == 0)
                _since[index] = ++_stamp;
            _tiers[index] = tier;
            _fading[index] = tier > 0 && fading;
        }

        private void FillBadges(GameState state)
        {
            BadgeCount = 0;
            var after = 0L;
            while (BadgeCount < MaxBadges)
            {
                var next = -1;
                for (var i = 0; i < StatusCount; i++)
                    if (_tiers[i] > 0 && _since[i] > after && (next < 0 || _since[i] < _since[next]))
                        next = i;
                if (next < 0)
                    return;
                after = _since[next];
                _badges[BadgeCount++] = BadgeOf((EnemyStatus)next, state);
            }
        }

        private StatusBadge BadgeOf(EnemyStatus status, GameState state)
        {
            var badge = new StatusBadge { Status = status, Fading = _fading[(int)status] };
            switch (status)
            {
                case EnemyStatus.Poison:
                    badge.Kind = StatusNumber.Stacks;
                    badge.Number = Whole(state.PoisonStacks);
                    break;
                case EnemyStatus.Burn:
                    badge.Kind = StatusNumber.Stacks;
                    badge.Number = Whole(state.BurnStacks);
                    break;
                case EnemyStatus.Fracture:
                    badge.Kind = StatusNumber.Stacks;
                    badge.Number = state.FractureStacks;
                    break;
                case EnemyStatus.EarthShred:
                    badge.Kind = StatusNumber.Percent;
                    badge.Number = System.Math.Max(1, (int)System.Math.Round(state.EarthShred * 100));
                    break;
            }
            return badge;
        }

        private static int Whole(double stacks) => System.Math.Max(1, (int)System.Math.Floor(stacks + 1e-9));

        private static bool Ending(int ticks) => ticks > 0 && ticks <= FadingTicks;

        private static int ShareTier(double value, double max, double low, double high)
        {
            if (!(value > 0))
                return 0;
            if (!(max > 0))
                return 1;
            var share = value / max;
            return share < low - 1e-9 ? 1 : share < high - 1e-9 ? 2 : 3;
        }

        private static int FractureTier(int stacks, int max)
        {
            if (stacks <= 0)
                return 0;
            if (max > 0 && stacks >= max)
                return 4;
            return ShareTier(stacks, max, LowShare + 1e-6, HighShare + 1e-6);
        }
    }
}
