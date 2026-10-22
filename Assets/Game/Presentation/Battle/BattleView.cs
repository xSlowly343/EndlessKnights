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
        private const float BossBlinkSeconds = 10f;

        private static readonly Vector2 HeroPosition = new Vector2(5f, 6.2f);
        private static readonly Vector2 EnemyPosition = new Vector2(27.5f, 5.6f);
        private static readonly Vector2 HeroSize = new Vector2(2.4f, 3.2f);
        private static readonly float EnemySize = 4.2f;
        private static readonly float BossSize = 5.6f;
        private static readonly Vector2 HpBarSize = new Vector2(7f, 0.45f);
        private static readonly Color HeroColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color EnemyColor = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color BossColor = new Color(0.6f, 0.1f, 0.6f);
        private static readonly Color GuardianColor = new Color(0.2f, 0.6f, 0.25f);
        private static readonly Color HpBackColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color HpFillColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color FocusColor = new Color(1f, 0.75f, 0.2f);
        private static readonly Color EnergyColor = new Color(0.3f, 0.75f, 1f);

        private SpriteRenderer _hero = null!;
        private SpriteRenderer _enemy = null!;
        private SpriteRenderer _hpFill = null!;
        private SpriteRenderer _focusFill = null!;
        private SpriteRenderer _energyFill = null!;
        private TextMesh _stageText = null!;
        private TextMesh _timerText = null!;
        private TextMesh _goldText = null!;
        private DamageNumbers _numbers = null!;
        private Color _enemyColor = EnemyColor;
        private float _enemyScale = EnemySize;
        private float _popAge = PopSeconds;
        private float _deathAge = DeathSeconds;
        private float _flashAge = FlashSeconds;
        private float _swingAge = SwingSeconds;

        public static BattleView Create()
        {
            var host = new GameObject("Battle");
            var view = host.AddComponent<BattleView>();
            view.Build();
            return view;
        }

        public void ShowEnemy(bool boss, bool guardian)
        {
            _enemyColor = guardian ? GuardianColor : boss ? BossColor : EnemyColor;
            _enemyScale = boss || guardian ? BossSize : EnemySize;
            _enemy.color = _enemyColor;
            _popAge = 0f;
            _deathAge = DeathSeconds;
        }

        public void KillEnemy() => _deathAge = 0f;

        public void Hit(BigNumber amount, bool crit)
        {
            _flashAge = 0f;
            _swingAge = 0f;
            var spread = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 1.5f));
            _numbers.Show(amount, crit, EnemyPosition + new Vector2(0f, _enemyScale / 2f) + spread);
        }

        public void ShowState(long stage, int killsOnStage, int enemiesPerStage, double hpFraction, double bossSecondsLeft, bool bossFight,
            double focusFraction, double energyFraction, BigNumber gold)
        {
            _stageText.text = bossFight ? $"Этап {stage} · БОСС" : $"Этап {stage} · {killsOnStage + 1}/{enemiesPerStage}";
            SetFill(_hpFill, HpBarSize.x, (float)hpFraction, EnemyPosition.x - HpBarSize.x / 2f);
            SetFill(_focusFill, StripSpace.Width * 0.3f, (float)focusFraction, StripSpace.Width * 0.35f);
            SetFill(_energyFill, StripSpace.Width * 0.3f, (float)energyFraction, StripSpace.Width * 0.35f);
            _timerText.gameObject.SetActive(bossFight);
            if (bossFight)
            {
                _timerText.text = $"{bossSecondsLeft:0.0} с";
                var blinking = bossSecondsLeft <= BossBlinkSeconds && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
                _timerText.color = blinking ? Color.red : Color.white;
            }
            _goldText.text = NumberFormatter.Format(gold, NumberFormatSettings.Default) + " з.";
        }

        private void Build()
        {
            _hero = ShapeSprites.Create("Hero", ShapeSprites.Square, HeroColor, HeroPosition, HeroSize, 10, transform);
            _enemy = ShapeSprites.Create("Enemy", ShapeSprites.Circle, EnemyColor, EnemyPosition, Vector2.one * EnemySize, 10, transform);
            _enemy.gameObject.AddComponent<CircleCollider2D>();
            _hero.gameObject.AddComponent<BoxCollider2D>();
            var hpY = EnemyPosition.y + BossSize / 2f + 0.5f;
            ShapeSprites.Create("HpBack", ShapeSprites.Square, HpBackColor, new Vector2(EnemyPosition.x, hpY), HpBarSize, 11, transform);
            _hpFill = ShapeSprites.Create("HpFill", ShapeSprites.Square, HpFillColor, new Vector2(EnemyPosition.x, hpY), HpBarSize, 12, transform);
            _focusFill = ShapeSprites.Create("Focus", ShapeSprites.Square, FocusColor, new Vector2(StripSpace.Width / 2f, 1.1f), new Vector2(1f, 0.35f), 12, transform);
            _energyFill = ShapeSprites.Create("Energy", ShapeSprites.Square, EnergyColor, new Vector2(StripSpace.Width / 2f, 0.55f), new Vector2(1f, 0.35f), 12, transform);
            _stageText = StripText.Create("Stage", string.Empty, new Vector2(StripSpace.Width * 0.35f, StripSpace.Height - 0.4f), 0.18f, TextAnchor.UpperLeft, Color.white, transform, 15);
            _timerText = StripText.Create("BossTimer", string.Empty, new Vector2(EnemyPosition.x, hpY + 0.9f), 0.2f, TextAnchor.MiddleCenter, Color.white, transform, 15);
            _goldText = StripText.Create("Gold", string.Empty, new Vector2(StripSpace.Width * 0.35f, StripSpace.Height - 1.6f), 0.16f, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.3f), transform, 15);
            _numbers = DamageNumbers.Create();
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
            AnimateEnemy(delta);
            AnimateHero(delta);
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
            _enemy.transform.position = EnemyPosition + new Vector2(shake, 0f);
        }

        private void AnimateHero(float delta)
        {
            _swingAge += delta;
            var swing = _swingAge < SwingSeconds ? Mathf.Sin(_swingAge / SwingSeconds * Mathf.PI) : 0f;
            _hero.transform.rotation = Quaternion.Euler(0f, 0f, -25f * swing);
            _hero.transform.position = HeroPosition + new Vector2(0.6f * swing, 0f);
        }

        private static float PopCurve(float t) => t < 0.6f ? Mathf.Lerp(0.8f, 1.05f, t / 0.6f) : Mathf.Lerp(1.05f, 1f, (t - 0.6f) / 0.4f);
    }
}
