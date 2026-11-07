using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum DigResult
    {
        Done,
        UnknownBiome,
        NotEnoughBones,
        AlreadyBuilt
    }

    public sealed class DigSystem
    {
        public const string Activity = "dig";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private DigRules _rules;

        public DigSystem(DigRules rules, GameState state, CampSystem camp, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _events = events;
        }

        public DigRules Rules => _rules;
        public DigState State => _state.Digs;
        public bool Open => _camp.Open(_rules.Building);
        public string CurrentSite => State.Sites.Count > 0 ? State.Sites[0] : string.Empty;
        public double BrushNeeded => _rules.BrushMeters * System.Math.Max(0.1, 1 - _state.Perk(MuseumPerk.LighterBrush)) * _state.PeacefulBonus.TimeFactor;

        public void UseRules(DigRules rules) => _rules = rules;

        public void NoteGuardians(int firstEvent)
        {
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind != GameEventKind.BossDefeated || gameEvent.Enemy?.Type != EnemyType.Guardian)
                    continue;
                var site = _rules.Site(gameEvent.Enemy.Biome);
                if (site != null && State.Sites.Count < _rules.MaxSites)
                    State.Sites.Add(site.Biome);
            }
        }

        public void Tick()
        {
            if (!_peaceful.InCamp || _peaceful.Activity != Activity || !Open || State.Sites.Count == 0)
                return;
            var work = _peaceful.Work(Activity);
            var clicks = work.Clicks;
            var meters = System.Math.Max(0, work.MouseMeters);
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            switch (State.Phase)
            {
                case DigPhase.Idle:
                    Begin();
                    Brush(meters);
                    break;
                case DigPhase.Brushing:
                    Brush(meters);
                    break;
                case DigPhase.Exposed:
                    if (clicks > 0)
                        Extract();
                    break;
            }
        }

        public long Bones(string biome)
        {
            var site = _rules.Site(biome);
            return site == null ? 0 : _camp.Count(site.Bone);
        }

        public DigResult CheckAssemble(string biome)
        {
            var site = _rules.Site(biome);
            if (site == null)
                return DigResult.UnknownBiome;
            if (State.Skeletons.Contains(biome))
                return DigResult.AlreadyBuilt;
            if (_camp.Count(site.Bone) < _rules.SkeletonBones)
                return DigResult.NotEnoughBones;
            return DigResult.Done;
        }

        public DigResult Assemble(string biome)
        {
            var result = CheckAssemble(biome);
            if (result != DigResult.Done)
                return result;
            _camp.Take(_rules.Site(biome)!.Bone, _rules.SkeletonBones);
            State.Skeletons.Add(biome);
            return DigResult.Done;
        }

        public bool DigNow()
        {
            if (!Open || State.Sites.Count == 0)
                return false;
            if (State.Phase == DigPhase.Idle)
                Begin();
            Extract();
            return true;
        }

        private void Begin()
        {
            State.Phase = DigPhase.Brushing;
            State.Brushed = 0;
            State.Quality = 1;
        }

        private void Brush(double meters)
        {
            if (meters <= 0)
                return;
            if (meters / GameLoop.TickSeconds > _rules.CrackMetersPerSecond)
            {
                State.Quality = System.Math.Max(_rules.MinQuality, State.Quality - _rules.CrackLoss);
                State.Cracks++;
                _events.Add(GameEvent.ForStage(GameEventKind.FindCracked, _state.Tick, 0));
            }
            State.Brushed += meters;
            if (State.Brushed >= BrushNeeded)
                State.Phase = DigPhase.Exposed;
        }

        private void Extract()
        {
            var site = _rules.Site(CurrentSite);
            State.Sites.RemoveAt(0);
            State.Phase = DigPhase.Idle;
            if (site == null)
                return;
            if (State.Quality < 1 && _state.PeacefulBonus.RollMastery(_state.Rng))
                State.Quality = System.Math.Min(1, State.Quality + _rules.CrackLoss);
            var rolled = _rules.BonesMin + _state.Rng.NextDouble() * (_rules.BonesMax - _rules.BonesMin);
            var bones = System.Math.Max(1, (int)System.Math.Round(rolled * State.Quality * (1 + _state.Perk(MuseumPerk.MoreBones)) * _state.PeacefulBonus.YieldFactor));
            _camp.Add(site.Bone, bones);
            if (_state.Rng.NextDouble() < _rules.ShardChance * State.Quality * _state.PeacefulBonus.LuckFactor)
                _state.Drops.Shards += _rules.ShardCount;
            if (_state.Rng.NextDouble() < _rules.DustChance * State.Quality * _state.PeacefulBonus.LuckFactor)
                _state.Drops.Dust += _rules.DustCount;
            State.Dug++;
            State.LastSite = site.Biome;
            State.LastBones = bones;
            _events.Add(GameEvent.ForStage(GameEventKind.FindDug, _state.Tick, bones));
        }
    }
}
