using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class Simulation
    {
        private readonly GameData _data;
        private readonly BotProfile _profile;
        private readonly HeroClass _heroClass;
        private readonly ulong _seed;

        public Simulation(GameData data, BotProfile profile, HeroClass heroClass = HeroClass.Typist, ulong seed = 1)
        {
            _data = data;
            _profile = profile;
            _heroClass = heroClass;
            _seed = seed;
        }

        public SimulationResult Run(double seconds, int stopAfterBossFailsInARow = 0)
        {
            var state = new GameState(_seed) { Class = _heroClass };
            var loop = new GameLoop(_data, new BotInput(_profile, _seed ^ 0x5EEDUL), state, new StatSheet());
            var records = new List<StageRecord>();
            var totalTicks = (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
            var stageStartTick = 0L;
            var bossStartTick = -1L;
            var bossFails = 0;
            var totalBossFails = 0;
            var stuck = false;
            var damage = BigNumber.Zero;

            for (var tick = 0L; tick < totalTicks && !stuck; tick++)
            {
                loop.Tick();
                for (var i = 0; i < loop.Events.Count; i++)
                {
                    var gameEvent = loop.Events[i];
                    switch (gameEvent.Kind)
                    {
                        case GameEventKind.Damage:
                            damage += gameEvent.Amount;
                            break;
                        case GameEventKind.BossStarted:
                            if (bossStartTick < 0)
                                bossStartTick = gameEvent.Tick;
                            break;
                        case GameEventKind.BossFailed:
                            bossFails++;
                            totalBossFails++;
                            stuck = stopAfterBossFailsInARow > 0 && state.BossFailsInARow >= stopAfterBossFailsInARow;
                            break;
                        case GameEventKind.BossDefeated:
                            records.Add(Record(gameEvent, state, stageStartTick, bossStartTick, bossFails));
                            stageStartTick = gameEvent.Tick + 1;
                            bossStartTick = -1;
                            bossFails = 0;
                            break;
                    }
                }
                loop.Events.Clear();
            }

            return new SimulationResult(records, state.Stage, state.HighestStage, state.RunGoldEarned, totalBossFails, state.ElapsedSeconds, stuck, damage);
        }

        private StageRecord Record(in GameEvent defeated, GameState state, long stageStartTick, long bossStartTick, int bossFails)
        {
            var clearedTick = defeated.Tick + 1;
            var bossTick = bossStartTick < 0 ? clearedTick : bossStartTick;
            return new StageRecord(
                defeated.Stage,
                clearedTick * GameLoop.TickSeconds,
                (clearedTick - stageStartTick) * GameLoop.TickSeconds,
                (bossTick - stageStartTick) * GameLoop.TickSeconds,
                (clearedTick - bossTick) * GameLoop.TickSeconds,
                bossFails,
                _data.Stages.EnemyHp(defeated.Stage),
                state.RunGoldEarned);
        }
    }
}
