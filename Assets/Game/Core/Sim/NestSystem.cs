using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum NestResult
    {
        Done,
        NoEgg,
        NoCoal,
        CoalLimit,
        NoSparks,
        UnknownBiome,
        BiomeTaken,
        NotOffered,
        UnknownCompanion
    }

    public sealed class NestSystem
    {
        private readonly GameState _state;
        private readonly CompanionSystem _companions;
        private readonly CampSystem _camp;
        private readonly StageTable _stages;
        private readonly EventQueue _events;

        public NestSystem(GameState state, CompanionSystem companions, CampSystem camp, StageTable stages, EventQueue events)
        {
            _state = state;
            _companions = companions;
            _camp = camp;
            _stages = stages;
            _events = events;
        }

        private NestRules Rules => _companions.Rules.Nest;
        private CompanionState State => _state.Companions;

        public int Slots => _camp.Slots(Rules.Building) + _companions.NestBonusSlots;

        public string EggCompanion(string biome)
        {
            var companions = _companions.Rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                if (companions[i].Source == "egg" && companions[i].Biome == biome)
                    return companions[i].Id;
            }
            return string.Empty;
        }

        public void NoteGuardians(int firstEvent)
        {
            NoteDrops(firstEvent);
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind != GameEventKind.BossDefeated || gameEvent.Enemy?.Type != EnemyType.Guardian)
                    continue;
                _companions.NoteHost(_companions.State.Day);
                _companions.HostDefeated(gameEvent.Enemy.Biome);
                if (State.EggCooldown > 0)
                    continue;
                if (EggCompanion(gameEvent.Enemy.Biome).Length == 0 || _state.Rng.NextDouble() >= Rules.Chance)
                    continue;
                Lay(gameEvent.Enemy.Biome);
                State.EggCooldown = Rules.CooldownSeconds;
            }
        }

        public void Lay(string biome)
        {
            State.Eggs.Add(new CompanionEgg { Biome = biome, SecondsLeft = Rules.HatchSeconds * System.Math.Max(0.1, 1 - _companions.TreeStat(MenagerieStat.HatchTime) - _companions.HomeOwn(OwnEffectKind.Hatch)) });
            _events.Add(GameEvent.ForStage(GameEventKind.CompanionEggFound, _state.Tick, State.Eggs.Count));
        }

        public void Tick() => Advance(GameLoop.TickSeconds);

        public void Advance(double seconds)
        {
            if (seconds <= 0)
                return;
            State.EggCooldown = System.Math.Max(0, State.EggCooldown - seconds);
            var warm = System.Math.Min(Slots, State.Eggs.Count);
            for (var i = warm - 1; i >= 0; i--)
            {
                var egg = State.Eggs[i];
                egg.SecondsLeft -= seconds;
                if (egg.SecondsLeft <= 0)
                    Hatch(i);
            }
            NoteRewards();
        }

        public void SetDay() => State.CoalToday = 0;

        public NestResult AddCoal(int index)
        {
            if (index < 0 || index >= System.Math.Min(Slots, State.Eggs.Count))
                return NestResult.NoEgg;
            if (State.CoalToday + Rules.CoalSeconds > Rules.CoalSecondsPerDay + 1e-9)
                return NestResult.CoalLimit;
            if (!_camp.Take(Rules.Coal, 1))
                return NestResult.NoCoal;
            State.CoalToday += Rules.CoalSeconds;
            Warm(index, Rules.CoalSeconds);
            return NestResult.Done;
        }

        public NestResult FriendWarm(int index)
        {
            if (index < 0 || index >= State.Eggs.Count)
                return NestResult.NoEgg;
            Warm(index, Rules.FriendWarmSeconds);
            return NestResult.Done;
        }

        private void Warm(int index, double seconds)
        {
            State.Eggs[index].SecondsLeft -= seconds;
            if (State.Eggs[index].SecondsLeft <= 0 && index < Slots)
                Hatch(index);
        }

        public NestResult HatchNow(int index)
        {
            if (index < 0 || index >= State.Eggs.Count)
                return NestResult.NoEgg;
            Hatch(index);
            return NestResult.Done;
        }

        private void Hatch(int index)
        {
            var egg = State.Eggs[index];
            State.Eggs.RemoveAt(index);
            State.Hatched++;
            var companion = EggCompanion(egg.Biome);
            if (_companions.GrantRolled(companion))
            {
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionHatched, _state.Tick, State.Hatched));
                return;
            }
            if (companion.Length > 0)
            {
                _companions.RepeatAfterStars(companion);
                _companions.Progress(_companions.SparkHeir(companion)).OwnSparks++;
            }
            State.Sparks++;
            _events.Add(GameEvent.ForStage(GameEventKind.CompanionSpark, _state.Tick, State.Sparks));
        }

        public NestResult UseSpark(string companion)
        {
            if (!_companions.Known(companion))
                return NestResult.UnknownCompanion;
            if (State.Sparks <= 0)
                return NestResult.NoSparks;
            State.Sparks--;
            _companions.Gain(companion, Rules.SparkExperience);
            return NestResult.Done;
        }

        public NestResult ChoiceEgg(string biome)
        {
            var companion = EggCompanion(biome);
            if (companion.Length == 0)
                return NestResult.UnknownBiome;
            if (_companions.Known(companion) || HasEgg(biome))
                return NestResult.BiomeTaken;
            if (State.Sparks < Rules.SparksForChoice)
                return NestResult.NoSparks;
            State.Sparks -= Rules.SparksForChoice;
            Lay(biome);
            return NestResult.Done;
        }

        private bool HasEgg(string biome)
        {
            for (var i = 0; i < State.Eggs.Count; i++)
            {
                if (State.Eggs[i].Biome == biome)
                    return true;
            }
            return false;
        }

        public void NoteRewards()
        {
            var rules = Rules;
            for (var i = 0; i < rules.Trophies.Count; i++)
            {
                var trophy = rules.Trophies[i];
                if (_state.Museum.Done.Contains(trophy.Collection) && !_companions.Known(trophy.Companion))
                    Reward(trophy.Companion);
            }
            if (_stages.Heart.Length > 0 && _state.RecordStage >= _stages.HeartFromStage && !_companions.Known(rules.HeartCompanion))
                Reward(rules.HeartCompanion);
            if (_state.Tick % UnlockCheckTicks == 0)
                CheckUnlocks();
        }

        public const int UnlockCheckTicks = 50;
        public System.Func<string, int>? GuildLevel { get; set; }

        public void CheckUnlocks()
        {
            var companions = _companions.Rules.Companions;
            for (var i = 0; i < companions.Count; i++)
            {
                var unlock = companions[i].Unlock;
                if (unlock != null && Reached(unlock) && !_companions.Known(companions[i].Id))
                    Reward(companions[i].Id);
            }
        }

        private bool Reached(CompanionUnlock unlock) => unlock.Kind switch
        {
            UnlockKind.Guild => GuildLevel != null && GuildLevel(unlock.Guild) >= unlock.Target,
            UnlockKind.Clan => _state.ClanCamp.Level >= unlock.Target,
            UnlockKind.Hideout => _state.Hideout.Tier >= unlock.Target,
            UnlockKind.Prestige => _state.Prestiges >= unlock.Target,
            UnlockKind.Achievements => _state.Achievements.Unlocked.Count >= unlock.Target,
            UnlockKind.ClassLevel => _state.ClassChosen && _state.HeroLevel >= unlock.Target && _state.ClassName == unlock.Class,
            _ => false
        };

        public void NoteDrops(int firstEvent)
        {
            var companions = _companions.Rules.Companions;
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind != GameEventKind.BossDefeated || gameEvent.Enemy == null)
                    continue;
                for (var c = 0; c < companions.Count; c++)
                {
                    var unlock = companions[c].Unlock;
                    if (unlock == null || unlock.Kind != UnlockKind.Drop || unlock.Enemy != gameEvent.Enemy.Id)
                        continue;
                    if (!_companions.Known(companions[c].Id) && _state.Rng.NextDouble() < unlock.Chance && _companions.GrantRolled(companions[c].Id))
                        _events.Add(GameEvent.ForStage(GameEventKind.CompanionHatched, _state.Tick, 0));
                }
            }
        }

        private void Reward(string companion)
        {
            if (_companions.Grant(companion))
                _events.Add(GameEvent.ForStage(GameEventKind.CompanionHatched, _state.Tick, 0));
        }

        public List<string> ShopOffers()
        {
            var rules = Rules;
            var pool = new List<string>();
            foreach (var def in _companions.Rules.Companions)
            {
                if ((Contains(rules.ShopSources, def.Source) || def.ShopPrice > 0) && !_companions.Known(def.Id))
                    pool.Add(def.Id);
            }
            var week = System.Math.Max(0, _companions.State.Day) / rules.ShopDays;
            var offers = new List<string>();
            var seed = (ulong)week * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            while (offers.Count < rules.ShopOffers && pool.Count > 0)
            {
                seed ^= seed >> 33;
                seed *= 0xFF51AFD7ED558CCDUL;
                seed ^= seed >> 33;
                var index = (int)(seed % (ulong)pool.Count);
                offers.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return offers;
        }

        public long ShopPriceOf(string companion)
        {
            var def = _companions.Rules.Companion(companion);
            return def != null && def.ShopPrice > 0 ? def.ShopPrice : Rules.ShopPrice;
        }

        public NestResult ShopBought(string companion)
        {
            if (!ShopOffers().Contains(companion))
                return NestResult.NotOffered;
            return _companions.Grant(companion) ? NestResult.Done : NestResult.UnknownCompanion;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }
}
