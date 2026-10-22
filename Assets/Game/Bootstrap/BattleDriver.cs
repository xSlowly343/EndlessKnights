using System;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Presentation.Battle;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class BattleDriver : MonoBehaviour
    {
        private GameLoop _loop = null!;
        private BattleView _view = null!;
        private GameData _data = null!;

        public GameLoop Loop => _loop;

        public static BattleDriver Attach(GameObject host, GameData data, IInputSource input)
        {
            var driver = host.AddComponent<BattleDriver>();
            driver._data = data;
            var state = new GameState((ulong)DateTime.UtcNow.Ticks) { Class = HeroClass.Swordsman };
            driver._loop = new GameLoop(data, input, state, new StatSheet());
            driver._view = BattleView.Create();
            return driver;
        }

        public void UseData(GameData data)
        {
            _data = data;
            _loop.UseData(data);
        }

        private void Update()
        {
            _loop.Advance(Time.unscaledDeltaTime);
            ShowEvents();
            ShowState();
        }

        private void ShowEvents()
        {
            var events = _loop.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var gameEvent = events[i];
                switch (gameEvent.Kind)
                {
                    case GameEventKind.Damage:
                        _view.Hit(gameEvent.Amount, gameEvent.IsCrit);
                        break;
                    case GameEventKind.EnemyKilled:
                    case GameEventKind.BossDefeated:
                        _view.KillEnemy();
                        break;
                    case GameEventKind.EnemySpawned:
                    case GameEventKind.BossStarted:
                        _view.ShowEnemy(_loop.State.EnemyType == EnemyType.Boss, _loop.State.EnemyType == EnemyType.Guardian);
                        break;
                }
            }
            events.Clear();
        }

        private void ShowState()
        {
            var state = _loop.State;
            var maxHp = state.EnemyMaxHp;
            var hpFraction = maxHp.IsZero ? 0d : (state.EnemyHp / maxHp).ToDouble();
            var bossFight = state.EnemyType != EnemyType.Normal;
            _view.ShowState(
                state.Stage,
                state.KillsOnStage,
                _data.Stages.EnemiesPerStage,
                hpFraction,
                state.BossTicksLeft / (double)GameLoop.TicksPerSecond,
                bossFight,
                FocusFraction(state.Focus),
                state.Energy / _data.Combat.Energy.Max,
                state.Gold);
        }

        private double FocusFraction(double focus)
        {
            var min = _data.Combat.Focus.Min;
            var range = _loop.Focus.Max - min;
            return range <= 0 ? 0 : (focus - min) / range;
        }
    }
}
