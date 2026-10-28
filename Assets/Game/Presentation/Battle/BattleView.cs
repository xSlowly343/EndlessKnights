using Game.Core.Math;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
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
        private const int MaxWaves = 8;
        private const float NoticeSeconds = 8f;
        private const int HudSortingOrder = 22;
        private const int TextSortingOrder = 26;
        private const float RowCentrePx = 20f;
        private const float BottomCentrePx = 166f;
        private const float MarginPx = 10f;
        private const float StageRightPx = 226f;
        private const float StageWidthPx = 140f;
        private const float WavesLeftPx = 234f;
        private const float WaveWidthPx = 22f;
        private const float WaveHeightPx = 6f;
        private const float WaveStepPx = 25f;
        private const float BossMarkPx = 9f;
        private const float TimerMarkLeftPx = 234f;
        private const float TimerLeftPx = 249f;
        private const float TimerWidthPx = 70f;
        private const float TimerHeightPx = 4f;
        private const float TimerTextLeftPx = 325f;
        private const float TimerTextWidthPx = 60f;
        private const float BarRadiusPx = 5f;
        private const float OrbCentreXPx = 21f;
        private const float OrbPx = 26f;
        private const float OrbFillPx = 22f;
        private const float DividerXPx = 40f;
        private const float DividerHeightPx = 18f;
        private const float HpHeightPx = 8f;
        private const float HpLeftPx = 300f;
        private const float HpWidthPx = 136f;
        private const float BossHpLeftPx = 240f;
        private const float BossHpWidthPx = 196f;
        private const float PercentLeftPx = 442f;
        private const float PercentWidthPx = 36f;
        private const float CoinPx = 14f;
        private const float GoldWidthPx = 90f;

        private static readonly Vector2 HeroPosition = StripSpace.AtPx(85f, 117f);
        private static readonly Vector2 HeroSize = new Vector2(StripSpace.Units(50f), StripSpace.Units(66f));
        private static readonly float EnemyX = StripSpace.Units(416f);
        private static readonly float GroundY = StripSpace.AtPx(0f, 150f).y;
        private static readonly float EnemySize = 6f;
        private static readonly float BossSize = 7.4f;
        private static readonly Vector2 CoinTarget = StripSpace.AtPx(MarginPx + CoinPx / 2f, RowCentrePx);
        private static readonly Vector2 NoticePosition = StripSpace.AtPx(240f, 56f);
        private static readonly Vector2 NumbersOrigin = StripSpace.AtPx(384f, 58f);
        private static readonly Vector2 BossNumbersOrigin = StripSpace.AtPx(318f, 52f);
        private static readonly float NumbersSpreadX = StripSpace.Units(14f);
        private static readonly float NumbersSpreadY = StripSpace.Units(3f);
        private static readonly Vector2 AllyNumbersShift = new Vector2(StripSpace.Units(-30f), StripSpace.Units(-34f));
        private static readonly Color HeroColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color EnemyColor = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color BossColor = new Color(0.6f, 0.1f, 0.6f);
        private static readonly Color GuardianColor = new Color(0.2f, 0.6f, 0.25f);
        private static readonly Color TrackColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color HpFillColor = new Color32(0x4F, 0xC2, 0x77, 0xFF);
        private static readonly Color BossHpFillColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color TimerBackColor = new Color32(0x3A, 0x1A, 0x22, 0xFF);
        private static readonly Color TimerFillColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color TimerMarkColor = new Color32(0xFF, 0x8A, 0x8A, 0xFF);
        private static readonly Color TimerTextColor = new Color32(0xFF, 0xD7, 0xD7, 0xFF);
        private static readonly Color TimerBlinkColor = new Color32(0xFF, 0x5A, 0x5A, 0xFF);
        private static readonly Color WaveEmpty = new Color32(0x3A, 0x48, 0x70, 0xFF);
        private static readonly Color WaveCurrent = new Color32(0x8F, 0xA6, 0xE0, 0xFF);
        private static readonly Color WaveFull = new Color32(0xEE, 0xF2, 0xFF, 0xFF);
        private static readonly Color BossMarkColor = new Color32(0x8C, 0x97, 0xB6, 0xFF);
        private static readonly Color OrbTrackColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color OrbRimColor = new Color32(0x2B, 0x4C, 0x8C, 0xFF);
        private static readonly Color DividerColor = new Color32(0x3A, 0x48, 0x70, 0xFF);
        private static readonly Color PercentColor = new Color32(0xD5, 0xDC, 0xEE, 0xFF);
        private static readonly Color RetreatColor = new Color32(0xFF, 0x66, 0x4D, 0xFF);
        private static readonly Color NoticeColor = new Color32(0xB3, 0xFF, 0xB3, 0xFF);
        private static readonly Color GroundColor = new Color(0.18f, 0.35f, 0.23f);

        private Transform _scene = null!;
        private SpriteRenderer _hero = null!;
        private SpriteRenderer _enemy = null!;
        private AllySilhouettes _allies = null!;
        private PartyEnemyMarks _marks = null!;
        private UiTheme _theme = null!;
        private RectTransform _hpTrack = null!;
        private RectTransform _hpFill = null!;
        private UnityEngine.UI.Image _hpFillImage = null!;
        private RectTransform _timer = null!;
        private RectTransform _timerFill = null!;
        private RectTransform _waves = null!;
        private UnityEngine.UI.Image _orbFill = null!;
        private readonly RectTransform[] _waveTracks = new RectTransform[MaxWaves];
        private readonly RectTransform[] _waveFills = new RectTransform[MaxWaves];
        private readonly UnityEngine.UI.Image[] _waveFillImages = new UnityEngine.UI.Image[MaxWaves];
        private RectTransform _bossMark = null!;
        private TextMeshProUGUI _stageText = null!;
        private TextMeshProUGUI _timerText = null!;
        private TextMeshProUGUI _percentText = null!;
        private TextMeshProUGUI _resourceText = null!;
        private TextMeshProUGUI _goldText = null!;
        private TextMeshProUGUI _retreatText = null!;
        private TextMeshProUGUI _noticeText = null!;
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
        private float _shownEnergy;
        private float _targetEnergy;
        private string _shownGold = string.Empty;
        private long _shownStage = -1;
        private bool _shownBoss;
        private int _shownTenths = -1;
        private int _shownPercent = -1;
        private int _shownResource = -1;
        private int _shownWaves = -1;
        private bool _hpIsBoss;

        public string StageText => _stageText.text;
        public string TimerText => _timerText.text;
        public string PercentText => _percentText.text;
        public string ResourceText => _resourceText.text;
        public float OrbFill => _orbFill.fillAmount;
        public bool WavesShown => _waves.gameObject.activeSelf;
        public int WaveSegmentsShown
        {
            get
            {
                var shown = 0;
                for (var i = 0; i < MaxWaves; i++)
                    if (_waveTracks[i].gameObject.activeSelf)
                        shown++;
                return shown;
            }
        }
        public string GoldText => _goldText.text;
        public bool TimerShown => _timer.gameObject.activeSelf;
        public float HpShown => _shownHp;
        public AllySilhouettes Allies => _allies;
        public PartyEnemyMarks PartyMarks => _marks;

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

        public void Relabel()
        {
            _shownStage = -1;
            _shownTenths = -1;
            _shownPercent = -1;
            _shownResource = -1;
            _shownGold = string.Empty;
            _retreatText.text = BattleTexts.Retreat;
        }

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

        public void ShowAllyHit(BigNumber amount, Color color)
        {
            var origin = (_enemyScale > EnemySize ? BossNumbersOrigin : NumbersOrigin) + AllyNumbersShift;
            var spread = new Vector2(Random.Range(-NumbersSpreadX, NumbersSpreadX), Random.Range(0f, NumbersSpreadY));
            _numbers.ShowAlly(amount, color, origin + spread);
        }

        public void ShowState(long stage, int wave, int waves, double waveFill, double hpFraction, double bossSecondsLeft, double bossTimerFraction, bool bossFight,
            double resource, double resourceMax, BigNumber gold)
        {
            if (stage != _shownStage || bossFight != _shownBoss)
            {
                _shownStage = stage;
                _shownBoss = bossFight;
                _stageText.text = BattleTexts.Stage(stage, bossFight);
            }
            _targetHp = (float)hpFraction;
            _targetEnergy = resourceMax > 0 ? (float)System.Math.Clamp(resource / resourceMax, 0, 1) : 0f;
            var shownResource = (int)System.Math.Floor(System.Math.Max(0, resource));
            if (shownResource != _shownResource)
                _resourceText.text = BattleTexts.Whole(_shownResource = shownResource);
            var percent = BattleTexts.PercentOf(hpFraction);
            if (percent != _shownPercent)
                _percentText.text = BattleTexts.Percent(_shownPercent = percent);
            ShowHpBar(bossFight);
            if (_waves.gameObject.activeSelf == bossFight)
                _waves.gameObject.SetActive(!bossFight);
            if (!bossFight)
                ShowWaves(wave, waves, (float)waveFill);
            if (_timer.gameObject.activeSelf != bossFight)
                _timer.gameObject.SetActive(bossFight);
            if (bossFight)
                ShowTimer(bossSecondsLeft, bossTimerFraction);
            var goldText = NumberFormatter.Format(gold, NumberDisplay.Settings);
            if (goldText != _shownGold)
                _goldText.text = _shownGold = goldText;
        }

        private void ShowTimer(double secondsLeft, double fraction)
        {
            var tenths = BattleTexts.Tenths(secondsLeft);
            if (tenths != _shownTenths)
                _timerText.text = BattleTexts.Seconds(_shownTenths = tenths);
            SetFill(_timerFill, TimerWidthPx, (float)fraction);
            var blinking = secondsLeft <= BossBlinkSeconds && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
            _timerText.color = blinking ? TimerBlinkColor : TimerTextColor;
        }

        private void Build()
        {
            _theme = UiTheme.Load();
            _scene = new GameObject("Scene").transform;
            _scene.SetParent(transform, false);
            _hero = ShapeSprites.Create("Hero", ShapeSprites.Square, HeroColor, HeroPosition, HeroSize, 10, _scene);
            _enemy = ShapeSprites.Create("Enemy", ShapeSprites.Circle, EnemyColor, EnemyCentre, Vector2.one * EnemySize, 10, _scene);
            _enemy.gameObject.AddComponent<CircleCollider2D>();
            _hero.gameObject.AddComponent<BoxCollider2D>();
            ShapeSprites.Create("Ground", ShapeSprites.Square, GroundColor, new Vector2(StripSpace.Width / 2f, GroundY), new Vector2(StripSpace.Width, StripSpace.Units(2f)), 9, _scene);
            var hud = StripCanvas.Create("Hud", HudSortingOrder, transform);
            BuildHud(hud);
            _marks = PartyEnemyMarks.Create(hud, _theme);
            var texts = StripCanvas.Create("SceneTexts", TextSortingOrder, _scene);
            _retreatText = Label("Retreat", texts, _theme.BoldFont, 20f, RetreatColor, TextAlignmentOptions.Center);
            StripCanvas.Place(_retreatText.rectTransform, EnemyX * StripSpace.PixelsPerUnit, StripSpace.BaseHeightPx - (GroundY + EnemySize / 2f) * StripSpace.PixelsPerUnit, new Vector2(160f, 28f), new Vector2(0.5f, 0.5f));
            _retreatText.text = BattleTexts.Retreat;
            _retreatText.gameObject.SetActive(false);
            _noticeText = Label("Notice", texts, _theme.TextFont, 13f, NoticeColor, TextAlignmentOptions.Center);
            var notice = StripCanvas.PxOf(NoticePosition);
            StripCanvas.Place(_noticeText.rectTransform, notice.x, StripSpace.BaseHeightPx - notice.y, new Vector2(320f, 20f), new Vector2(0.5f, 0.5f));
            _noticeText.gameObject.SetActive(false);
            _allies = AllySilhouettes.Create(_scene, texts, _theme);
            _numbers = DamageNumbers.Create();
            _numbers.transform.SetParent(transform, false);
            _coins = CoinBurst.Create(CoinTarget);
            _coins.transform.SetParent(transform, false);
            _coins.CoinArrived += () => _goldBumpAge = 0f;
        }

        private void BuildHud(RectTransform hud)
        {
            var left = new Vector2(0f, 0.5f);
            var centre = new Vector2(0.5f, 0.5f);
            BuildGold(hud, left);
            _stageText = Label("Stage", hud, _theme.NumberFont, 16f, _theme.Text, TextAlignmentOptions.MidlineRight);
            StripCanvas.Place(_stageText.rectTransform, StageRightPx - StageWidthPx, RowCentrePx, new Vector2(StageWidthPx, 24f), left);
            BuildWaves(hud, left);
            BuildTimer(hud, left);
            BuildOrb(hud, centre);
            var divider = UiElements.Fill("Divider", hud, DividerColor).rectTransform;
            StripCanvas.Place(divider, DividerXPx, BottomCentrePx, new Vector2(1f, DividerHeightPx), centre);
            _hpFill = Bar("Hp", hud, HpLeftPx, BottomCentrePx, HpWidthPx, HpHeightPx, HpFillColor);
            _hpTrack = (RectTransform)_hpFill.parent;
            _hpFillImage = _hpFill.GetComponent<UnityEngine.UI.Image>();
            _percentText = Label("HpPercent", hud, _theme.TextFont, 11f, PercentColor, TextAlignmentOptions.MidlineLeft);
            StripCanvas.Place(_percentText.rectTransform, PercentLeftPx, BottomCentrePx, new Vector2(PercentWidthPx, 18f), left);
        }

        private void BuildGold(RectTransform hud, Vector2 left)
        {
            var coin = UiElements.Dot("Coin", hud, _theme.Gold);
            StripCanvas.Place(coin.rectTransform, MarginPx, RowCentrePx, Vector2.one * CoinPx, left);
            _goldText = Label("Gold", hud, _theme.NumberFont, 15f, _theme.Gold, TextAlignmentOptions.MidlineLeft);
            StripCanvas.Place(_goldText.rectTransform, MarginPx + CoinPx + 4f, RowCentrePx, new Vector2(GoldWidthPx, 20f), left);
        }

        private void BuildWaves(RectTransform hud, Vector2 left)
        {
            _waves = UiElements.Rect("Waves", hud);
            UiElements.Stretch(_waves);
            for (var i = 0; i < MaxWaves; i++)
            {
                _waveTracks[i] = UiElements.Fill("Wave " + i, _waves, WaveEmpty, WaveHeightPx / 2f).rectTransform;
                StripCanvas.Place(_waveTracks[i], WavesLeftPx + i * WaveStepPx, RowCentrePx, new Vector2(WaveWidthPx, WaveHeightPx), left);
                _waveFillImages[i] = UiElements.Fill("Fill", _waveTracks[i], WaveCurrent, WaveHeightPx / 2f);
                _waveFills[i] = _waveFillImages[i].rectTransform;
                FillFromLeft(_waveFills[i], WaveWidthPx, WaveHeightPx);
            }
            _bossMark = UiElements.Dot("BossMark", _waves, BossMarkColor).rectTransform;
            StripCanvas.Place(_bossMark, WavesLeftPx, RowCentrePx, Vector2.one * BossMarkPx, left);
        }

        private void BuildTimer(RectTransform hud, Vector2 left)
        {
            _timer = UiElements.Rect("BossTimer", hud);
            UiElements.Stretch(_timer);
            var mark = UiElements.Dot("BossMark", _timer, TimerMarkColor).rectTransform;
            StripCanvas.Place(mark, TimerMarkLeftPx, RowCentrePx, Vector2.one * BossMarkPx, left);
            var track = UiElements.Fill("Track", _timer, TimerBackColor, TimerHeightPx / 2f).rectTransform;
            StripCanvas.Place(track, TimerLeftPx, RowCentrePx, new Vector2(TimerWidthPx, TimerHeightPx), left);
            _timerFill = UiElements.Fill("Fill", track, TimerFillColor, TimerHeightPx / 2f).rectTransform;
            FillFromLeft(_timerFill, TimerWidthPx, TimerHeightPx);
            _timerText = Label("Text", _timer, _theme.NumberFont, 14f, TimerTextColor, TextAlignmentOptions.MidlineLeft);
            StripCanvas.Place(_timerText.rectTransform, TimerTextLeftPx, RowCentrePx, new Vector2(TimerTextWidthPx, 20f), left);
            _timer.gameObject.SetActive(false);
        }

        private void BuildOrb(RectTransform hud, Vector2 centre)
        {
            var rim = UiElements.Dot("ResourceOrb", hud, OrbRimColor).rectTransform;
            StripCanvas.Place(rim, OrbCentreXPx, BottomCentrePx, Vector2.one * OrbPx, centre);
            var track = UiElements.Dot("Track", rim, OrbTrackColor).rectTransform;
            UiElements.Stretch(track, (OrbPx - OrbFillPx) / 2f);
            _orbFill = UiElements.Dot("Fill", rim, _theme.ResourceTint);
            UiElements.Stretch(_orbFill.rectTransform, (OrbPx - OrbFillPx) / 2f);
            _orbFill.type = UnityEngine.UI.Image.Type.Filled;
            _orbFill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            _orbFill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            _orbFill.fillAmount = 0f;
            _resourceText = Label("Amount", rim, _theme.NumberFont, 11f, Color.white, TextAlignmentOptions.Center);
            UiElements.Stretch(_resourceText.rectTransform);
        }

        private static RectTransform Bar(string name, RectTransform hud, float leftPx, float centrePx, float widthPx, float heightPx, Color color)
        {
            var radius = Mathf.Min(BarRadiusPx, heightPx / 2f);
            var track = UiElements.Fill(name, hud, TrackColor, radius).rectTransform;
            StripCanvas.Place(track, leftPx, centrePx, new Vector2(widthPx, heightPx), new Vector2(0f, 0.5f));
            var fill = UiElements.Fill("Fill", track, color, radius).rectTransform;
            FillFromLeft(fill, widthPx, heightPx);
            return fill;
        }

        private static void FillFromLeft(RectTransform fill, float widthPx, float heightPx)
        {
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(widthPx, heightPx);
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_FontAsset? font, float sizePx, Color color, TextAlignmentOptions alignment) =>
            UiElements.Label(name, parent, font, sizePx, color, alignment);

        private void ShowWaves(int wave, int total, float fill)
        {
            total = Mathf.Min(total, MaxWaves);
            for (var i = 0; i < MaxWaves; i++)
            {
                var visible = i < total;
                if (_waveTracks[i].gameObject.activeSelf != visible)
                    _waveTracks[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var done = i < wave;
                _waveFillImages[i].color = done ? WaveFull : WaveCurrent;
                SetFill(_waveFills[i], WaveWidthPx, done ? 1f : i == wave ? fill : 0f);
            }
            if (total == _shownWaves)
                return;
            _shownWaves = total;
            StripCanvas.Place(_bossMark, WavesLeftPx + total * WaveStepPx + 2f, RowCentrePx, Vector2.one * BossMarkPx, new Vector2(0f, 0.5f));
        }

        private void ShowHpBar(bool boss)
        {
            if (boss == _hpIsBoss)
                return;
            _hpIsBoss = boss;
            var width = boss ? BossHpWidthPx : HpWidthPx;
            StripCanvas.Place(_hpTrack, boss ? BossHpLeftPx : HpLeftPx, BottomCentrePx, new Vector2(width, HpHeightPx), new Vector2(0f, 0.5f));
            _hpFillImage.color = boss ? BossHpFillColor : HpFillColor;
        }

        private static void SetFill(RectTransform fill, float fullWidthPx, float fraction)
        {
            var size = fill.sizeDelta;
            size.x = fullWidthPx * Mathf.Clamp01(fraction);
            fill.sizeDelta = size;
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            AnimateBars(delta);
            AnimateEnemy(delta);
            AnimateHero(delta);
            _allies.Animate(Time.unscaledTime);
            _marks.Animate(delta);
            AnimateEffects(delta);
        }

        private void AnimateBars(float delta)
        {
            var blend = 1f - Mathf.Exp(-BarSmoothing * delta);
            _shownHp = _targetHp > _shownHp ? _targetHp : Mathf.Lerp(_shownHp, _targetHp, blend);
            _shownEnergy = Mathf.Lerp(_shownEnergy, _targetEnergy, blend);
            SetFill(_hpFill, _hpIsBoss ? BossHpWidthPx : HpWidthPx, _shownHp);
            _orbFill.fillAmount = _shownEnergy;
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
