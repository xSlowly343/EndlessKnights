using System;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Presentation.Battle;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class BattleDriver : MonoBehaviour
    {
        private TrustedDay? _day;
        private GameLoop _loop = null!;
        private BattleView _view = null!;
        private SkillBarView _skillBar = null!;
        private StripHintView _hint = null!;
        private SkillEffects _effects = null!;
        private readonly ShowQueue _shows = new ShowQueue(ShowRules.OwnSkills);
        private GameData _data = null!;
        private int _bossTotalTicks = 1;
        private bool _riftLoading;

        public event Action<long>? LeveledUp;

        public GameLoop Loop => _loop;
        public SkillBarView SkillBar => _skillBar;
        public StripHintView Hint => _hint;
        public BattleView View => _view;

        public void UseTrustedDay(TrustedDay day) => _day = day;

        public static GameState NewRun() => new GameState((ulong)DateTime.UtcNow.Ticks) { Class = HeroClass.Runedancer, ClassChosen = false };

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
            driver._view.ShowTransition(driver._loop.Scene.Transition);
            return driver;
        }

        public OfflineResult? ApplyOffline(double awaySeconds)
        {
            if (awaySeconds <= 0)
                return null;
            _loop.ReturnHomeIfAway(awaySeconds);
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
            var clock = Game.Platform.Windows.Power.SystemClock.Read();
            _loop.SetLocalDay(_day != null ? _day.Today(clock) : LocalDay.Of(DateTimeOffset.Now));
            _loop.SetServerTime(_day != null ? _day.UtcMs(clock) : clock.WallUnixMs);
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
                        OfferShow(gameEvent.Gem);
                        break;
                    case GameEventKind.BossFailed:
                        _view.Retreat();
                        break;
                    case GameEventKind.RiftGiftOffered:
                        _view.ShowNotice(Game.Core.Text.Texts.Get("rift.gift.offered"));
                        break;
                    case GameEventKind.RiftFeatureOpened:
                        _view.ShowNotice(TransitionTexts.FeatureOpened((Feature)gameEvent.Stage));
                        break;
                    case GameEventKind.LevelUp:
                        LeveledUp?.Invoke(gameEvent.Stage);
                        break;
                }
            }
            events.Clear();
            PlayShows();
        }

        public void ShowAllyCast(GemDef gem, ShowSize size) => _effects.CastAlly(gem, _view.StrikeTarget, size);

        private void OfferShow(GemDef gem)
        {
            if (gem.Skill?.Effect is AuraEffect)
                _effects.Cast(gem, _view.StrikeTarget);
            else
                _shows.Offer(gem, 0, SkillShowKind.CastByHand(_loop.State.Skills, gem), Time.unscaledTime);
        }

        private void PlayShows()
        {
            var now = (double)Time.unscaledTime;
            while (_shows.TryTake(now, out var cue))
                Show(cue);
            _view.DimStrikes(_shows.UrgentBusy(now));
        }

        private void Show(ShowCue cue)
        {
            _effects.Cast(cue.Gem, _view.StrikeTarget, cue.Size);
            if (cue.Size == ShowSize.Full && SkillShowFamilies.Of(cue.Gem) == SkillShowFamily.Strong)
                _view.ShakeHeavy(SkillEffects.HeavyImpactSeconds);
        }

        private void ShowState()
        {
            var state = _loop.State;
            var maxHp = state.EnemyMaxHp;
            var hpFraction = maxHp.IsZero ? 0d : (state.EnemyHp / maxHp).ToDouble();
            var bossFight = state.EnemyType != EnemyType.Normal;
            _view.ShowState(
                state.Stage,
                _data.Stages.WaveAfterKills(state.WaveStage, state.KillsOnStage),
                _data.Stages.WaveCount,
                _data.Stages.WaveFillAfterKills(state.WaveStage, state.KillsOnStage),
                hpFraction,
                state.BossTicksLeft / (double)GameLoop.TicksPerSecond,
                state.BossTicksLeft / (double)_bossTotalTicks,
                bossFight,
                state.Resource,
                _data.Combat.Resources.Max,
                state.Gold);
            _skillBar.Show(state.Skills, state.Resource);
            ShowRift(state);
            ShowAura(state);
        }

        private void ShowRift(GameState state)
        {
            _view.ShowRift(state.Rift.Inside);
            var loading = TransitionLook.ShowsLoading(_loop.Scene.Transition);
            if (loading == _riftLoading)
                return;
            _riftLoading = loading;
            if (loading)
                _view.ShowLoadingLines(RiftLoadingText.Lines(_loop));
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
