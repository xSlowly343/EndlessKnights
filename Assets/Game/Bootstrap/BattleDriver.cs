using System;
using Game.Core.Clock;
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
        private SkillBarView _skillBar = null!;
        private StripHintView _hint = null!;
        private SkillEffects _effects = null!;
        private GameData _data = null!;
        private int _bossTotalTicks = 1;

        public event Action<long>? LeveledUp;

        public GameLoop Loop => _loop;
        public SkillBarView SkillBar => _skillBar;
        public StripHintView Hint => _hint;
        public BattleView View => _view;

        public static GameState NewRun() => new GameState((ulong)DateTime.UtcNow.Ticks) { Class = HeroClass.Viking };

        public static BattleDriver Attach(GameObject host, GameData data, IInputSource input, GameState? state = null)
        {
            var driver = host.AddComponent<BattleDriver>();
            driver._data = data;
            driver._loop = new GameLoop(data, input, state ?? NewRun(), new StatSheet());
            driver._loop.Book.GrantStartingGems();
            driver._view = BattleView.Create();
            driver._skillBar = SkillBarView.Create();
            driver._hint = StripHintView.Create();
            driver._effects = SkillEffects.Create();
            driver._skillBar.SlotClicked += driver._loop.RequestCast;
            driver._bossTotalTicks = System.Math.Max(1, driver._loop.State.BossTicksLeft);
            driver._view.ShowEnemy(driver._loop.State.EnemyType == EnemyType.Boss, driver._loop.State.EnemyType == EnemyType.Guardian);
            return driver;
        }

        public OfflineResult? ApplyOffline(double awaySeconds)
        {
            if (awaySeconds <= 0)
                return null;
            var result = new OfflineCalculator(_data, _loop.Hero).Simulate(_loop.State, awaySeconds);
            _loop.ApplyOffline(result);
            return result;
        }

        public void UseData(GameData data)
        {
            _data = data;
            _loop.UseData(data);
        }

        private void Update()
        {
            _loop.SetLocalDay(LocalDay.Of(DateTimeOffset.Now));
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
                        _bossTotalTicks = System.Math.Max(1, _loop.State.BossTicksLeft);
                        _view.ShowEnemy(_loop.State.EnemyType == EnemyType.Boss, _loop.State.EnemyType == EnemyType.Guardian);
                        break;
                    case GameEventKind.SkillCast when gameEvent.Gem != null:
                        _effects.Cast(gameEvent.Gem);
                        break;
                    case GameEventKind.BossFailed:
                        _view.Retreat();
                        break;
                    case GameEventKind.LevelUp:
                        LeveledUp?.Invoke(gameEvent.Stage);
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
                _data.Stages.WaveAfterKills(state.Stage, state.KillsOnStage),
                _data.Stages.WaveCount,
                _data.Stages.WaveFillAfterKills(state.Stage, state.KillsOnStage),
                hpFraction,
                state.BossTicksLeft / (double)GameLoop.TicksPerSecond,
                state.BossTicksLeft / (double)_bossTotalTicks,
                bossFight,
                state.Resource,
                _data.Combat.Resources.Max,
                state.Gold);
            _skillBar.Show(state.Skills, state.Resource);
            ShowAura(state);
        }

        private void ShowAura(GameState state)
        {
            for (var i = 0; i < state.Skills.Count; i++)
            {
                var slot = state.Skills[i];
                if (!slot.AuraApplied)
                    continue;
                _effects.ShowAura(true, slot.Skill.Color);
                return;
            }
            _effects.ShowAura(false, default);
        }
    }
}
