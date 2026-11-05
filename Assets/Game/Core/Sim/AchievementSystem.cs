using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class AchievementSystem
    {
        public const int CheckEveryTicks = 10;

        private readonly GameState _state;
        private readonly EventQueue _events;
        private AchievementCatalog _catalog;

        public AchievementSystem(AchievementCatalog catalog, GameState state, EventQueue events)
        {
            _catalog = catalog;
            _state = state;
            _events = events;
        }

        public AchievementCatalog Catalog => _catalog;
        public AchievementState State => _state.Achievements;

        public void UseCatalog(AchievementCatalog catalog) => _catalog = catalog;

        public bool IsUnlocked(string id) => State.Unlocked.Contains(id);

        public double Progress(AchievementDef achievement) => System.Math.Min(achievement.Target, Value(achievement.Counter));

        public void Sent(string id) => State.Unsent.Remove(id);

        public void Tick()
        {
            Count();
            if (_state.Tick % CheckEveryTicks != 0)
                return;
            var all = _catalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var achievement = all[i];
                if (State.Unlocked.Contains(achievement.Id) || Value(achievement.Counter) < achievement.Target)
                    continue;
                State.Unlocked.Add(achievement.Id);
                State.Unsent.Add(achievement.Id);
                _events.Add(GameEvent.ForStage(GameEventKind.AchievementUnlocked, _state.Tick, i));
            }
        }

        public double Value(AchievementCounter counter) => counter switch
        {
            AchievementCounter.Kills or AchievementCounter.Bosses or AchievementCounter.SkillCasts => State.Totals[(int)counter],
            AchievementCounter.RecordStage => _state.RecordStage,
            AchievementCounter.Prestiges => _state.Prestiges,
            AchievementCounter.Keys => _state.TotalInput.Keys,
            AchievementCounter.Clicks => _state.TotalInput.Clicks,
            AchievementCounter.MouseKm => _state.TotalInput.MouseMeters / 1000,
            AchievementCounter.PlayHours => _state.TotalInput.PlaySeconds / 3600,
            AchievementCounter.Trials => _state.Chronicle.Completed.Count,
            AchievementCounter.Artifacts => _state.Artifacts.Count,
            AchievementCounter.Dungeons => _state.Dungeons.Completed,
            AchievementCounter.Salvage => _state.Drops.SalvagedItems,
            _ => 0
        };

        private void Count()
        {
            var achievements = State;
            Track(achievements, AchievementCounter.Kills, _state.Kills);
            Track(achievements, AchievementCounter.Bosses, _state.BossKills);
            Track(achievements, AchievementCounter.SkillCasts, _state.SkillCasts);
        }

        private static void Track(AchievementState achievements, AchievementCounter counter, double now)
        {
            var index = (int)counter;
            if (now > achievements.Last[index])
                achievements.Totals[index] += now - achievements.Last[index];
            achievements.Last[index] = now;
        }
    }
}
