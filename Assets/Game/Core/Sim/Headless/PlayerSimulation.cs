using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class PlayerSimulation
    {
        public const double DefaultStuckMinutes = 20;

        private const int ActEveryTicks = 60 * GameLoop.TicksPerSecond;
        private const double SecondsPerDay = 24 * 3600;

        private readonly GameData _data;
        private readonly BotProfile _profile;
        private readonly HeroClass _heroClass;
        private readonly ulong _seed;
        private readonly double _stuckMinutes;
        private BigNumber _goldSeen = BigNumber.Zero;
        private BigNumber _goldTotal = BigNumber.Zero;

        public PlayerSimulation(GameData data, BotProfile profile, HeroClass heroClass = HeroClass.Viking, ulong seed = 1, double stuckMinutes = DefaultStuckMinutes)
        {
            _data = data;
            _profile = profile;
            _heroClass = heroClass;
            _seed = seed;
            _stuckMinutes = stuckMinutes;
        }

        public GameState? LastState { get; private set; }

        public IReadOnlyList<DayRecord> Run(int days, double activeHoursPerDay)
        {
            var state = new GameState(_seed) { Class = _heroClass };
            var hero = new StatSheet();
            var loop = new GameLoop(_data, new BotInput(_profile, _seed ^ 0x5EEDUL), state, hero);
            var bot = new PlayerBot(_data, loop, _stuckMinutes);
            var offline = new OfflineCalculator(_data, hero);
            var activeTicks = (long)System.Math.Round(activeHoursPerDay * 3600 * GameLoop.TicksPerSecond);
            var offlineSeconds = System.Math.Max(0, SecondsPerDay - activeHoursPerDay * 3600);
            var records = new List<DayRecord>(days);
            _goldSeen = BigNumber.Zero;
            _goldTotal = BigNumber.Zero;
            var awaySeconds = 0.0;

            for (var day = 1; day <= days; day++)
            {
                var goldBefore = _goldTotal;
                var itemsBefore = state.Drops.ItemsFound;
                loop.SetLocalDay(day);
                Act(bot, state, awaySeconds);
                for (var tick = 1L; tick <= activeTicks; tick++)
                {
                    loop.Tick();
                    loop.Drops.Flush();
                    loop.Events.Clear();
                    if (tick % ActEveryTicks == 0)
                        Act(bot, state, ActEveryTicks * GameLoop.TickSeconds);
                }
                loop.ApplyOffline(offline.Simulate(state, offlineSeconds));
                awaySeconds = offlineSeconds;
                CountGold(state);
                records.Add(Record(day, state, _goldTotal - goldBefore, state.Drops.ItemsFound - itemsBefore));
            }
            LastState = state;
            return records;
        }

        private void Act(PlayerBot bot, GameState state, double seconds)
        {
            CountGold(state);
            bot.Act(seconds);
        }

        private void CountGold(GameState state)
        {
            var earned = state.RunGoldEarned;
            _goldTotal += earned < _goldSeen ? earned : earned - _goldSeen;
            _goldSeen = earned;
        }

        private static string CommonTrees(GameState state)
        {
            var parts = new List<string>();
            foreach (var tree in state.CommonTrees)
            {
                if (tree != null)
                    parts.Add(tree.TreeId + ":" + tree.PointsSpent.ToString(CultureInfo.InvariantCulture));
            }
            return parts.Count == 0 ? "-" : string.Join("+", parts);
        }

        private static DayRecord Record(int day, GameState state, BigNumber goldToday, long itemsToday)
        {
            var levels = 0;
            foreach (var level in state.Artifacts.Values)
                levels += level;
            var ascendancy = state.Ascendancy;
            return new DayRecord(day, state.Stage, state.HighestStage, state.RecordStage, state.HeroLevel, state.TreePointsSpent, CommonTrees(state),
                ascendancy.Subclass ?? "-", ascendancy.Allocated.Count, state.Prestiges, state.Artifacts.Count, levels, state.EchoEarned, goldToday,
                itemsToday, state.Drops.DailyItems, state.OwnedGems.Count, state.Drops.Shards, state.Drops.ShardsFound, state.Drops.UncutFound);
        }
    }
}
