using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public enum PartyTrialDamage
    {
        Keys,
        Mouse,
        Skills,
        Minions
    }

    public enum PartyTrialOutcome
    {
        Idle,
        Running,
        Cleared,
        Failed
    }

    public struct PartyTrialHit
    {
        public PartyTrialHit(double keys, double mouse, double skills, double minions, bool typing, double armorPen = 0, double shred = 0)
        {
            ArmorPen = armorPen;
            Shred = shred;
            Keys = keys;
            Mouse = mouse;
            Skills = skills;
            Minions = minions;
            Typing = typing;
        }

        public double Keys;
        public double Mouse;
        public double Skills;
        public double Minions;
        public bool Typing;
        public double ArmorPen;
        public double Shred;

        public readonly double Of(PartyTrialDamage kind) => kind switch
        {
            PartyTrialDamage.Keys => Keys,
            PartyTrialDamage.Mouse => Mouse,
            PartyTrialDamage.Skills => Skills,
            _ => Minions
        };
    }

    public sealed class PartyTrialFight
    {
        public const int Kinds = 4;
        public const int NoKind = -1;

        private readonly double[] _shields = new double[Kinds];
        private readonly double[] _window = new double[Kinds];
        private readonly double[] _kindFactor = new double[Kinds];
        private readonly double[] _bank = new double[Kinds];
        private PartyTrialDef? _trial;
        private PartyTrialRules _rules = PartyTrialRules.None;
        private PartyTrialComplicationDef? _complication;
        private DefenseRules _defense = new DefenseRules();
        private double _power;
        private double _globalFactor;
        private long _quietTicks;
        private long _chainQuietTicks;
        private long _chainStepTicks;
        private int _lastLeader = NoKind;

        public PartyTrialOutcome Outcome { get; private set; }
        public PartyTrialDef? Trial => _trial;
        public PartyTrialComplicationDef? Complication => _complication;
        public int Tier { get; private set; }
        public int Fighters { get; private set; }
        public long Tick { get; private set; }
        public long TotalTicks { get; private set; }
        public double HpLeft { get; private set; }
        public double HpFull { get; private set; }
        public double SkillShieldLeft { get; private set; }
        public int Holder { get; private set; }
        public double Bank => _bank[0] + _bank[1] + _bank[2] + _bank[3];
        public int Resisted { get; private set; } = NoKind;
        public int Chain { get; private set; }
        public int Wave { get; private set; }
        public double Armor { get; private set; }
        public int WavesCleared { get; private set; }
        public double SecondsLeft => System.Math.Max(0, TotalTicks - Tick) * GameLoop.TickSeconds;
        public bool Running => Outcome == PartyTrialOutcome.Running;

        public double ShieldLeft(PartyTrialDamage kind) => _shields[(int)kind];

        public int ResultTier
        {
            get
            {
                if (Outcome != PartyTrialOutcome.Cleared)
                    return 0;
                return _trial != null && _trial.Rule == PartyTrialRule.Marathon ? WavesCleared : Tier;
            }
        }

        public bool PulseOpen => _trial != null && _trial.Rule == PartyTrialRule.Pulse
            && Tick % Ticks(_trial.EverySeconds) < Ticks(_trial.WindowSeconds);

        public double ChainFactor => _trial == null ? 1
            : Chain == 0 ? _trial.ChainBase : System.Math.Min(_trial.ChainMax, 1 + _trial.ChainStep * Chain);

        public void Start(PartyTrialRules rules, DefenseRules defense, PartyTrialDef trial, int tier, int fighters, double power, bool night)
        {
            _rules = rules;
            _defense = defense;
            _trial = trial;
            Tier = trial.Rule == PartyTrialRule.Marathon ? 1 : System.Math.Max(1, tier);
            Fighters = System.Math.Max(1, fighters);
            _power = System.Math.Max(0, power);
            _complication = rules.ComplicationOf(trial, Tier);
            Tick = 0;
            TotalTicks = Ticks(trial.Seconds);
            Outcome = PartyTrialOutcome.Running;
            Holder = 0;
            System.Array.Clear(_bank, 0, Kinds);
            Resisted = NoKind;
            Chain = 0;
            _quietTicks = 0;
            _chainQuietTicks = 0;
            _chainStepTicks = 0;
            _lastLeader = NoKind;
            System.Array.Clear(_window, 0, Kinds);
            for (var i = 0; i < Kinds; i++)
                _shields[i] = trial.Rule == PartyTrialRule.Shields ? trial.ShieldShare : 0;
            Wave = 1;
            WavesCleared = 0;
            HpFull = trial.Rule == PartyTrialRule.Marathon ? rules.WaveHp(1) : 1;
            HpLeft = HpFull;
            Armor = rules.ArmorShare(Tier) * defense.K;
            SkillShieldLeft = Is(PartyTrialComplication.SkillShield) ? _complication!.Share : 0;
            SetFactors(night);
        }

        public void Stop()
        {
            if (Running)
                Outcome = PartyTrialOutcome.Failed;
        }

        public double Advance(PartyTrialHit[] hits, int count)
        {
            if (!Running || _trial == null)
                return 0;
            var dealt = Strike(hits, System.Math.Min(count, hits.Length));
            NoteQuiet(dealt);
            Tick++;
            Finish();
            return dealt;
        }

        private double Strike(PartyTrialHit[] hits, int count)
        {
            var trial = _trial!;
            if (trial.Rule == PartyTrialRule.Choir && !AllTyping(hits, count))
                return 0;
            if (trial.Rule == PartyTrialRule.Chain)
                CountChain(hits, count);
            if (trial.Rule == PartyTrialRule.Relay)
                return Relay(hits, count);
            var dealt = 0.0;
            for (var kind = 0; kind < Kinds; kind++)
            {
                var amount = 0.0;
                for (var i = 0; i < count; i++)
                    amount += Landed(hits[i], kind);
                dealt += Hit(kind, amount * Factor(kind));
            }
            if (trial.Rule == PartyTrialRule.Mirror)
                Mirror(hits, count);
            return dealt;
        }

        private double Relay(PartyTrialHit[] hits, int count)
        {
            var trial = _trial!;
            var dealt = 0.0;
            var passTicks = Ticks(trial.PassSeconds);
            if (Tick > 0 && Tick % passTicks == 0)
            {
                Holder = (int)(Tick / passTicks % Fighters);
                for (var kind = 0; kind < Kinds; kind++)
                {
                    dealt += Hit(kind, _bank[kind] * trial.BankShare);
                    _bank[kind] = 0;
                }
            }
            for (var i = 0; i < count; i++)
            {
                for (var kind = 0; kind < Kinds; kind++)
                {
                    var amount = Landed(hits[i], kind) * Factor(kind);
                    if (i == Holder)
                        dealt += Hit(kind, amount);
                    else
                        _bank[kind] += amount;
                }
            }
            return dealt;
        }

        private double Landed(in PartyTrialHit hit, int kind) => System.Math.Max(0, hit.Of((PartyTrialDamage)kind)) * ArmorFactor(hit);

        public double ArmorFactor(in PartyTrialHit hit)
        {
            if (!(Armor > 0))
                return 1;
            var shred = System.Math.Min(_defense.ShredCap, System.Math.Max(0, hit.Shred));
            var pen = System.Math.Min(_defense.ArmorPenCap, System.Math.Max(0, hit.ArmorPen));
            return _defense.K / (Armor * (1 - shred) * (1 - pen) + _defense.K);
        }

        private double Hit(int kind, double amount)
        {
            if (amount <= 0)
                return 0;
            if (_trial!.Rule == PartyTrialRule.Shields && AnyShield())
            {
                var taken = System.Math.Min(_shields[kind], amount);
                _shields[kind] -= taken;
                return taken;
            }
            if (SkillShieldLeft > 0)
            {
                if (kind != (int)PartyTrialDamage.Skills)
                    return 0;
                var taken = System.Math.Min(SkillShieldLeft, amount);
                SkillShieldLeft -= taken;
                return taken;
            }
            var dealt = System.Math.Min(HpLeft, amount);
            HpLeft -= dealt;
            return dealt;
        }

        private double Factor(int kind) => _kindFactor[kind] * _globalFactor * RuleFactor(kind);

        private double RuleFactor(int kind)
        {
            var trial = _trial!;
            switch (trial.Rule)
            {
                case PartyTrialRule.Silence:
                    return kind == (int)PartyTrialDamage.Keys ? 0 : 1;
                case PartyTrialRule.Mirror:
                    return kind == Resisted ? 1 - trial.Resist : 1;
                case PartyTrialRule.Pulse:
                    return PulseOpen ? trial.Inside : trial.Outside;
                case PartyTrialRule.Chain:
                    return ChainFactor;
                default:
                    return 1;
            }
        }

        private void Mirror(PartyTrialHit[] hits, int count)
        {
            for (var i = 0; i < count; i++)
            {
                for (var kind = 0; kind < Kinds; kind++)
                    _window[kind] += System.Math.Max(0, hits[i].Of((PartyTrialDamage)kind));
            }
            if ((Tick + 1) % Ticks(_trial!.WindowSeconds) != 0)
                return;
            var best = NoKind;
            for (var kind = 0; kind < Kinds; kind++)
            {
                if (_window[kind] > 0 && (best == NoKind || _window[kind] > _window[best]))
                    best = kind;
            }
            Resisted = best;
            System.Array.Clear(_window, 0, Kinds);
        }

        private void CountChain(PartyTrialHit[] hits, int count)
        {
            var trial = _trial!;
            _chainStepTicks++;
            var leader = Leader(hits, count);
            if (leader != NoKind && _lastLeader != NoKind && leader != _lastLeader && _chainStepTicks >= Ticks(trial.StepSeconds))
            {
                Chain++;
                _chainStepTicks = 0;
                _chainQuietTicks = 0;
            }
            else if (++_chainQuietTicks >= Ticks(trial.KeepSeconds))
            {
                Chain = 0;
                _chainQuietTicks = 0;
            }
            if (leader != NoKind)
                _lastLeader = leader;
        }

        private static int Leader(PartyTrialHit[] hits, int count)
        {
            var leader = NoKind;
            var best = 0.0;
            for (var i = 0; i < count; i++)
            {
                var total = 0.0;
                for (var kind = 0; kind < Kinds; kind++)
                    total += System.Math.Max(0, hits[i].Of((PartyTrialDamage)kind));
                if (total > best)
                {
                    best = total;
                    leader = i;
                }
            }
            return leader;
        }

        private void NoteQuiet(double dealt)
        {
            _quietTicks = dealt > 0 ? 0 : _quietTicks + 1;
            if (!Is(PartyTrialComplication.Regeneration) || _quietTicks < Ticks(_complication!.AfterSeconds))
                return;
            HpLeft = System.Math.Min(HpFull, HpLeft + HpFull * _complication.PerSecond * GameLoop.TickSeconds);
        }

        private void Finish()
        {
            var trial = _trial!;
            if (trial.Rule == PartyTrialRule.Marathon)
            {
                FinishWave(trial);
                return;
            }
            if (HpLeft <= 1e-12)
                Outcome = PartyTrialOutcome.Cleared;
            else if (Tick >= TotalTicks)
                Outcome = PartyTrialOutcome.Failed;
        }

        private void FinishWave(PartyTrialDef trial)
        {
            var waveTicks = Ticks(trial.WaveSeconds);
            if (HpLeft <= 1e-12 && WavesCleared < Wave)
                WavesCleared = Wave;
            if (Tick % waveTicks != 0)
                return;
            if (WavesCleared < Wave)
            {
                Outcome = WavesCleared > 0 ? PartyTrialOutcome.Cleared : PartyTrialOutcome.Failed;
                return;
            }
            if (Wave >= trial.Waves)
            {
                Outcome = PartyTrialOutcome.Cleared;
                return;
            }
            Wave++;
            HpFull = _rules.WaveHp(Wave);
            HpLeft = HpFull;
            Armor = _rules.ArmorShare(Wave) * _defense.K;
        }

        private void SetFactors(bool night)
        {
            for (var i = 0; i < Kinds; i++)
                _kindFactor[i] = 1;
            _globalFactor = _power;
            if (_complication == null)
                return;
            if (_complication.Kind == PartyTrialComplication.Carapace)
                _kindFactor[(int)PartyTrialDamage.Keys] = _complication.Keys;
            else if (_complication.Kind == PartyTrialComplication.Shadow)
                _kindFactor[(int)PartyTrialDamage.Mouse] = _complication.Mouse;
            else if (_complication.Kind == PartyTrialComplication.Night)
                _globalFactor *= night ? _complication.Night : _complication.Day;
        }

        private bool Is(PartyTrialComplication kind) => _complication != null && _complication.Kind == kind;

        private bool AnyShield()
        {
            for (var i = 0; i < Kinds; i++)
            {
                if (_shields[i] > 1e-12)
                    return true;
            }
            return false;
        }

        private static bool AllTyping(PartyTrialHit[] hits, int count)
        {
            if (count == 0)
                return false;
            for (var i = 0; i < count; i++)
            {
                if (!hits[i].Typing)
                    return false;
            }
            return true;
        }

        private static long Ticks(double seconds) => System.Math.Max(1, (long)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
