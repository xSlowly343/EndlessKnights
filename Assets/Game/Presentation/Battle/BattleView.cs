using Game.Core.Math;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class BattleView : MonoBehaviour
    {
        private const float PopSeconds = 0.12f;
        private const float DeathSeconds = 0.2f;
        private const float FlashSeconds = 0.05f;
        private const float SwingSeconds = 0.12f;
        private const float ShakeUnits = 0.18f;
        private const float CritShakeSeconds = 0.12f;
        private const float CritShakeUnits = 0.25f;
        private const float BossBlinkSeconds = 10f;
        private const float BarSmoothing = 18f;
        private const float GoldBumpSeconds = 0.15f;
        private const float RetreatSeconds = 1.2f;
        private const int MaxDots = 10;
        private const float NoticeSeconds = 8f;

        private static readonly Vector2 HeroPosition = StripSpace.AtPx(85f, 117f);
        private static readonly Vector2 HeroSize = new Vector2(StripSpace.Units(50f), StripSpace.Units(66f));
        private static readonly float EnemyX = StripSpace.Units(416f);
        private static readonly float GroundY = StripSpace.AtPx(0f, 150f).y;
        private static readonly float EnemySize = 6f;
        private static readonly float BossSize = 7.4f;
        private static readonly Vector2 HpBarSize = new Vector2(StripSpace.Units(200f), StripSpace.Units(10f));
        private static readonly Vector2 HpBarCentre = StripSpace.AtPx(260f, 165f);
        private static readonly Vector2 TimerBarSize = new Vector2(StripSpace.Units(70f), StripSpace.Units(3f));
        private static readonly Vector2 TimerBarCentre = StripSpace.AtPx(365f, 32f);
        private static readonly Vector2 TimerTextPosition = StripSpace.AtPx(330f, 17f);
        private static readonly float FocusBarLeft = StripSpace.Units(10f);
        private static readonly float FocusBarWidth = StripSpace.Units(96f);
        private static readonly Vector2 FocusBarCentre = StripSpace.AtPx(58f, 163f);
        private static readonly Vector2 FocusBarSize = new Vector2(FocusBarWidth, StripSpace.Units(8f));
        private static readonly Vector2 EnergyBarCentre = StripSpace.AtPx(58f, 173f);
        private static readonly Vector2 EnergyBarSize = new Vector2(FocusBarWidth, StripSpace.Units(4f));
        private static readonly Vector2 StagePosition = StripSpace.AtPx(10f, 8f);
        private static readonly Vector2 FirstDotPosition = StripSpace.AtPx(82f, 20f);
        private static readonly float DotStep = StripSpace.Units(9f);
        private static readonly float DotSize = StripSpace.Units(6f);
        private static readonly Vector2 GoldPosition = StripSpace.AtPx(10f, 31f);
        private static readonly Vector2 NoticePosition = StripSpace.AtPx(240f, 56f);
        private static readonly Vector2 NumbersOrigin = StripSpace.AtPx(290f, 72f);
        private static readonly Vector2 BossNumbersOrigin = StripSpace.AtPx(230f, 72f);
        private static readonly float NumbersSpreadX = StripSpace.Units(21f);
        private static readonly float NumbersSpreadY = StripSpace.Units(3f);
        private static readonly Color HeroColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color EnemyColor = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color BossColor = new Color(0.6f, 0.1f, 0.6f);
        private static readonly Color GuardianColor = new Color(0.2f, 0.6f, 0.25f);
        private static readonly Color BarBackColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color HpFillColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color TimerFillColor = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color FocusColor = new Color(1f, 0.75f, 0.2f);
        private static readonly Color EnergyColor = new Color(0.3f, 0.75f, 1f);
        private static readonly Color DotEmpty = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color DotFull = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color GroundColor = new Color(0.18f, 0.35f, 0.23f);

        private Transform _scene = null!;
        private SpriteRenderer _hero = null!;
        private SpriteRenderer _enemy = null!;
        private SpriteRenderer _hpFill = null!;
        private SpriteRenderer _timerBack = null!;
        private SpriteRenderer _timerFill = null!;
        private SpriteRenderer _focusFill = null!;
        private SpriteRenderer _energyFill = null!;
        private readonly SpriteRenderer[] _dots = new SpriteRenderer[MaxDots];
        private TextMesh _stageText = null!;
        private TextMesh _timerText = null!;
        private TextMesh _goldText = null!;
        private TextMesh _retreatText = null!;
        private TextMesh _noticeText = null!;
        private float _noticeAge = NoticeSeconds;
        private DamageNumbers _numbers = null!;
        private CoinBurst _coins = null!;
        private Color _enemyColor = EnemyColor;
        private float _enemyScale = EnemySize;
        private float _popAge = PopSeconds;
        private float _deathAge = DeathSeconds;
        private float _flashAge = FlashSeconds;
        private float _swingAge = SwingSeconds;
        private float _critShakeAge = CritShakeSeconds;
        private float _goldBumpAge = GoldBumpSeconds;
        private float _retreatAge = RetreatSeconds;
        private float _shownHp = 1f;
        private float _targetHp = 1f;
        private float _shownFocus;
        private float _targetFocus;
        private float _shownEnergy;
        private float _targetEnergy;
        private string _shownGold = string.Empty;

        public static BattleView Create()
        {
            var host = new GameObject("Battle");
            var view = host.AddComponent<BattleView>();
            view.Build();
            return view;
        }

        private Vector2 EnemyCentre => new Vector2(EnemyX, GroundY + _enemyScale / 2f);

        public void ShowEnemy(bool boss, bool guardian)
        {
            _enemyColor = guardian ? GuardianColor : boss ? BossColor : EnemyColor;
            _enemyScale = boss || guardian ? BossSize : EnemySize;
            _enemy.color = _enemyColor;
            _popAge = 0f;
            _deathAge = DeathSeconds;
            _shownHp = 1f;
            _targetHp = 1f;
        }

        public void KillEnemy()
        {
            _deathAge = 0f;
            _coins.Burst(EnemyCentre);
        }

        public void Retreat() => _retreatAge = 0f;

        public void ShowNotice(string text)
        {
            _noticeText.text = text;
            _noticeAge = 0f;
        }

        public void Hit(BigNumber amount, bool crit)
        {
            _flashAge = 0f;
            _swingAge = 0f;
            if (crit)
                _critShakeAge = 0f;
            var origin = _enemyScale > EnemySize ? BossNumbersOrigin : NumbersOrigin;
            var spread = new Vector2(Random.Range(-NumbersSpreadX, NumbersSpreadX), Random.Range(0f, NumbersSpreadY));
            _numbers.Show(amount, crit, origin + spread);
        }

        public void ShowState(long stage, int killsOnStage, int enemiesPerStage, double hpFraction, double bossSecondsLeft, double bossTimerFraction, bool bossFight,
            double focusFraction, double energyFraction, BigNumber gold)
        {
            _stageText.text = bossFight ? $"Этап {stage} · БОСС" : $"Этап {stage}";
            _targetHp = (float)hpFraction;
            _targetFocus = (float)focusFraction;
            _targetEnergy = (float)energyFraction;
            ShowDots(bossFight ? 0 : killsOnStage, bossFight ? 0 : enemiesPerStage);
            _timerBack.gameObject.SetActive(bossFight);
            _timerFill.gameObject.SetActive(bossFight);
            _timerText.gameObject.SetActive(bossFight);
            if (bossFight)
            {
                SetFill(_timerFill, TimerBarSize.x, (float)bossTimerFraction, TimerBarCentre.x - TimerBarSize.x / 2f);
                _timerText.text = $"{bossSecondsLeft:0.0} с";
                var blinking = bossSecondsLeft <= BossBlinkSeconds && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
                _timerText.color = blinking ? Color.red : Color.white;
                _timerFill.color = blinking ? Color.red : TimerFillColor;
            }
            var goldText = NumberFormatter.Format(gold, NumberFormatSettings.Default);
            if (goldText != _shownGold)
                _goldText.text = _shownGold = goldText;
        }

        private void Build()
        {
            _scene = new GameObject("Scene").transform;
            _scene.SetParent(transform, false);
            _hero = ShapeSprites.Create("Hero", ShapeSprites.Square, HeroColor, HeroPosition, HeroSize, 10, _scene);
            _enemy = ShapeSprites.Create("Enemy", ShapeSprites.Circle, EnemyColor, EnemyCentre, Vector2.one * EnemySize, 10, _scene);
            _enemy.gameObject.AddComponent<CircleCollider2D>();
            _hero.gameObject.AddComponent<BoxCollider2D>();
            ShapeSprites.Create("Ground", ShapeSprites.Square, GroundColor, new Vector2(StripSpace.Width / 2f, GroundY), new Vector2(StripSpace.Width, StripSpace.Units(2f)), 9, _scene);
            ShapeSprites.Create("HpBack", ShapeSprites.Square, BarBackColor, HpBarCentre, HpBarSize, 11, _scene);
            _hpFill = ShapeSprites.Create("HpFill", ShapeSprites.Square, HpFillColor, HpBarCentre, HpBarSize, 12, _scene);
            _timerBack = ShapeSprites.Create("TimerBack", ShapeSprites.Square, BarBackColor, TimerBarCentre, TimerBarSize, 11, _scene);
            _timerFill = ShapeSprites.Create("TimerFill", ShapeSprites.Square, TimerFillColor, TimerBarCentre, TimerBarSize, 12, _scene);
            ShapeSprites.Create("FocusBack", ShapeSprites.Square, BarBackColor, FocusBarCentre, FocusBarSize, 11, _scene);
            _focusFill = ShapeSprites.Create("Focus", ShapeSprites.Square, FocusColor, FocusBarCentre, FocusBarSize, 12, _scene);
            _energyFill = ShapeSprites.Create("Energy", ShapeSprites.Square, EnergyColor, EnergyBarCentre, EnergyBarSize, 12, _scene);
            for (var i = 0; i < MaxDots; i++)
                _dots[i] = ShapeSprites.Create("Dot", ShapeSprites.Circle, DotEmpty, FirstDotPosition + new Vector2(i * DotStep, 0f), Vector2.one * DotSize, 12, _scene);
            _stageText = StripText.Create("Stage", string.Empty, StagePosition, 0.18f, TextAnchor.UpperLeft, Color.white, _scene, 15);
            _timerText = StripText.Create("BossTimer", string.Empty, TimerTextPosition, 0.15f, TextAnchor.MiddleLeft, Color.white, _scene, 15);
            _goldText = StripText.Create("Gold", string.Empty, GoldPosition, 0.15f, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f), _scene, 15);
            _retreatText = StripText.Create("Retreat", "Отступление", EnemyCentre, 0.25f, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f), _scene, 25);
            _retreatText.gameObject.SetActive(false);
            _noticeText = StripText.Create("Notice", string.Empty, NoticePosition, 0.16f, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.7f), _scene, 30);
            _noticeText.gameObject.SetActive(false);
            _numbers = DamageNumbers.Create();
            _coins = CoinBurst.Create(GoldPosition + new Vector2(StripSpace.Units(8f), 0f));
            _coins.CoinArrived += () => _goldBumpAge = 0f;
        }

        private void ShowDots(int kills, int total)
        {
            for (var i = 0; i < MaxDots; i++)
            {
                var visible = i < total;
                _dots[i].gameObject.SetActive(visible);
                if (visible)
                    _dots[i].color = i < kills ? DotFull : DotEmpty;
            }
        }

        private static void SetFill(SpriteRenderer fill, float fullWidth, float fraction, float left)
        {
            var width = fullWidth * Mathf.Clamp01(fraction);
            var transform = fill.transform;
            transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), transform.localScale.y, 1f);
            transform.position = new Vector3(left + width / 2f, transform.position.y, transform.position.z);
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            AnimateBars(delta);
            AnimateEnemy(delta);
            AnimateHero(delta);
            AnimateEffects(delta);
        }

        private void AnimateBars(float delta)
        {
            var blend = 1f - Mathf.Exp(-BarSmoothing * delta);
            _shownHp = _targetHp > _shownHp ? _targetHp : Mathf.Lerp(_shownHp, _targetHp, blend);
            _shownFocus = Mathf.Lerp(_shownFocus, _targetFocus, blend);
            _shownEnergy = Mathf.Lerp(_shownEnergy, _targetEnergy, blend);
            SetFill(_hpFill, HpBarSize.x, _shownHp, HpBarCentre.x - HpBarSize.x / 2f);
            SetFill(_focusFill, FocusBarWidth, _shownFocus, FocusBarLeft);
            SetFill(_energyFill, FocusBarWidth, _shownEnergy, FocusBarLeft);
        }

        private void AnimateEnemy(float delta)
        {
            _popAge += delta;
            _deathAge += delta;
            _flashAge += delta;

            var scale = _enemyScale;
            if (_popAge < PopSeconds)
                scale *= PopCurve(_popAge / PopSeconds);
            var alpha = 1f;
            if (_deathAge < DeathSeconds)
            {
                var progress = _deathAge / DeathSeconds;
                alpha = 1f - progress;
                scale *= 1f + 0.3f * progress;
            }
            _enemy.transform.localScale = new Vector3(scale, scale, 1f);
            var flashing = _flashAge < FlashSeconds;
            var color = flashing ? Color.white : _enemyColor;
            color.a = alpha;
            _enemy.color = color;
            var shake = flashing ? ShakeUnits : 0f;
            _enemy.transform.position = EnemyCentre + new Vector2(shake, 0f);
        }

        private void AnimateHero(float delta)
        {
            _swingAge += delta;
            var swing = _swingAge < SwingSeconds ? Mathf.Sin(_swingAge / SwingSeconds * Mathf.PI) : 0f;
            _hero.transform.rotation = Quaternion.Euler(0f, 0f, -25f * swing);
            _hero.transform.position = HeroPosition + new Vector2(0.6f * swing, 0f);
        }

        private void AnimateEffects(float delta)
        {
            _critShakeAge += delta;
            _scene.localPosition = _critShakeAge < CritShakeSeconds
                ? new Vector3(Random.Range(-CritShakeUnits, CritShakeUnits), Random.Range(-CritShakeUnits, CritShakeUnits), 0f)
                : Vector3.zero;

            _goldBumpAge += delta;
            var bump = _goldBumpAge < GoldBumpSeconds ? 1f + 0.25f * Mathf.Sin(_goldBumpAge / GoldBumpSeconds * Mathf.PI) : 1f;
            _goldText.transform.localScale = new Vector3(bump, bump, 1f);

            _noticeAge += delta;
            var noticing = _noticeAge < NoticeSeconds;
            _noticeText.gameObject.SetActive(noticing);
            if (noticing)
            {
                var color = _noticeText.color;
                color.a = Mathf.Clamp01((NoticeSeconds - _noticeAge) / 1.5f);
                _noticeText.color = color;
            }

            _retreatAge += delta;
            var retreating = _retreatAge < RetreatSeconds;
            _retreatText.gameObject.SetActive(retreating);
            if (retreating)
            {
                var color = _retreatText.color;
                color.a = 1f - _retreatAge / RetreatSeconds;
                _retreatText.color = color;
            }
        }

        private static float PopCurve(float t) => t < 0.6f ? Mathf.Lerp(0.8f, 1.05f, t / 0.6f) : Mathf.Lerp(1.05f, 1f, (t - 0.6f) / 0.4f);
    }
}
