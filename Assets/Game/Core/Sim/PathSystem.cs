using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class PathSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly CombatSystem _combat;
        private readonly ResourceSystem _resource;
        private readonly SkillSystem _skills;

        private long _seenKills;

        public PathSystem(GameState state, StatSheet hero, CombatSystem combat, ResourceSystem resource, SkillSystem skills)
        {
            _seenKills = state.Kills;
            _state = state;
            _hero = hero;
            _combat = combat;
            _resource = resource;
            _skills = skills;
        }

        public long IdleHits { get; private set; }
        public long CompanionHits { get; private set; }
        public long ClickHits { get; private set; }
        public long DoomReleases { get; private set; }
        public long Echoes { get; private set; }
        public long RaisedFromKills { get; private set; }

        public void Tick(in EffectiveInput input)
        {
            var dealt = _combat.LastTickDamage;
            ReleaseDoom();
            IdleStrikes(input);
            Companion();
            ClickStrikes(input);
            HolyFire();
            CurseStrikes();
            Echo(dealt);
            RaiseFromKills();
        }

        private void RaiseFromKills()
        {
            var kills = _state.Kills - _seenKills;
            _seenKills = _state.Kills;
            if (kills <= 0)
                return;
            RiseOnTheirOwn(kills);
            var rules = _combat.Paths;
            var skeleton = _hero.Value(CombatStats.SkeletonOnKill, 0, StatTags.Minion);
            var beast = _hero.Value(CombatStats.BeastOnKill, 0, StatTags.Minion);
            for (var i = 0; i < kills && i < MinionSystem.Capacity; i++)
            {
                Raise(rules.SkeletonOnKill, skeleton);
                Raise(rules.BeastOnKill, beast);
            }
        }

        private void RiseOnTheirOwn(long kills)
        {
            var rules = _combat.Skeletons;
            if (!_state.Is(HeroClass.Gravedigger) || rules.Rise == null)
                return;
            _state.KillsTowardRise += (int)System.Math.Min(kills, MinionSystem.Capacity);
            var every = RiseEvery(rules);
            while (_state.KillsTowardRise >= every)
            {
                _state.KillsTowardRise -= every;
                _skills.Minions.Rise(rules.Rise, rules.RiseMax);
            }
        }

        private int RiseEvery(Content.MinionRules rules)
        {
            var every = rules.RiseEvery + _hero.Value(CombatStats.RiseEvery, 0, StatTags.Minion);
            if (_state.ResourceFill >= 1)
                every += _hero.Value(CombatStats.RiseEveryAtFullResource, 0, StatTags.Minion);
            return System.Math.Max(rules.RiseEveryMin, (int)System.Math.Round(every));
        }

        private void Raise(Content.Gems.SummonEffect summon, double chance)
        {
            if (chance <= 0 || (chance < 1 && _state.Rng.NextDouble() >= chance))
                return;
            _skills.Minions.Place(summon, StatTags.Skill);
            RaisedFromKills++;
        }

        private void ReleaseDoom()
        {
            if (_state.DoomTicks <= 0 || --_state.DoomTicks > 0)
                return;
            var doom = _state.Doom;
            _state.Doom = BigNumber.Zero;
            var multiplier = (1 + System.Math.Max(0, _hero.Value(CombatStats.DoomMultiplier, 0))) * System.Math.Max(0, _hero.Value(CombatStats.LoopShare, 1));
            if (doom.Sign <= 0)
                return;
            DoomReleases++;
            _combat.DirectHit(doom * multiplier);
        }

        private void IdleStrikes(in EffectiveInput input)
        {
            var perSecond = _hero.Value(CombatStats.IdleHitsPerSecond, 0, StatTags.Idle);
            if (perSecond <= 0 || input.HasInput)
            {
                _state.IdleHitCarry = 0;
                return;
            }
            var hits = TakeHits(_state.IdleHitCarry + perSecond * GameLoop.TickSeconds, out var carry);
            _state.IdleHitCarry = carry;
            for (var i = 0; i < hits; i++)
            {
                _combat.SkillHit(_combat.BaseKeyDamage, 1, StatTags.Idle);
                IdleHits++;
            }
        }

        private void Companion()
        {
            var perSecond = _hero.Value(CombatStats.CompanionHitsPerSecond, 0, StatTags.Minion);
            if (perSecond <= 0)
            {
                _state.CompanionHitCarry = 0;
                return;
            }
            var speed = System.Math.Max(0, _hero.Value(CombatStats.MinionSpeed, 1, StatTags.Minion));
            var hits = TakeHits(_state.CompanionHitCarry + perSecond * speed * GameLoop.TickSeconds, out var carry);
            _state.CompanionHitCarry = carry;
            var multiplier = System.Math.Max(0, _hero.Value(CombatStats.CompanionMultiplier, 1, StatTags.Minion));
            for (var i = 0; i < hits; i++)
            {
                _combat.SkillHit(_combat.BaseKeyDamage, multiplier, StatTags.Skill | StatTags.Minion);
                CompanionHits++;
            }
        }

        private void ClickStrikes(in EffectiveInput input)
        {
            var multiplier = _hero.Value(CombatStats.ClickHitMultiplier, 0, StatTags.Click);
            if (multiplier <= 0 || input.Clicks <= 0)
            {
                _state.ClickHitCarry = 0;
                return;
            }
            var hits = TakeHits(_state.ClickHitCarry + input.Clicks, out var carry);
            _state.ClickHitCarry = carry;
            for (var i = 0; i < hits; i++)
            {
                _combat.SkillHit(_combat.BaseKeyDamage, multiplier * _state.FocusMultiplier, StatTags.Click);
                ClickHits++;
            }
        }

        private void HolyFire()
        {
            var perPoint = _hero.Value(CombatStats.ResourceGainDamage, 0, StatTags.Skill);
            var gained = _resource.GainedThisTick;
            if (perPoint > 0 && gained > 0)
                _combat.SkillHit(_combat.BaseKeyDamage, perPoint * gained, StatTags.Skill);
        }

        private void CurseStrikes()
        {
            var perSecond = _hero.Value(CombatStats.CurseHitsPerSecond, 0, StatTags.DoT);
            if (perSecond <= 0 || _state.Tick % GameLoop.TicksPerSecond != 0)
                return;
            var curses = _skills.ActiveCurses;
            if (curses > 0)
                _combat.SkillHit(_combat.BaseKeyDamage, curses * perSecond * System.Math.Max(0, _hero.Value(CombatStats.CurseDamage, 1)), StatTags.Skill | StatTags.DoT);
        }

        private void Echo(BigNumber dealt)
        {
            var every = _hero.Value(CombatStats.EchoEverySeconds, 0);
            if (every <= 0)
            {
                _state.EchoDamage = BigNumber.Zero;
                _state.EchoTicks = 0;
                return;
            }
            _state.EchoDamage += dealt;
            if (++_state.EchoTicks < System.Math.Max(1, (int)System.Math.Round(every * GameLoop.TicksPerSecond)))
                return;
            var echo = _state.EchoDamage * System.Math.Max(0, _hero.Value(CombatStats.EchoShare, 0));
            _state.EchoDamage = BigNumber.Zero;
            _state.EchoTicks = 0;
            if (echo.Sign <= 0)
                return;
            Echoes++;
            _combat.DirectHit(echo);
        }

        private static int TakeHits(double amount, out double carry)
        {
            var whole = System.Math.Floor(amount + 1e-9);
            carry = System.Math.Max(0, amount - whole);
            return (int)whole;
        }
    }
}
