using Game.Core.Content;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Battle
{
    public sealed class EnemyStatusView
    {
        private const int Loops = 11;
        private const int CurseSlot = 7;
        private const int Flashes = 3;
        private const int OutlineSides = 4;
        private const int BiomeBadges = 2;
        private const float LargeBodyFromPx = 120f;
        private const float EyesShare = 0.34f;
        private const float RuneLiftPx = 12f;
        private const float BadgePx = 18f;
        private const float BadgeStepPx = 20f;
        private const float BadgeRightPx = 376f;
        private const float BadgeRowPx = 40f;
        private const float ArrowPx = 9f;
        private const float FadeBlinkSeconds = 0.25f;
        private const float OutlinePulseSeconds = 0.67f;
        private const float WeakenedBlend = 0.55f;
        private const int MaxNumber = 100;

        private static readonly string[] PoisonKeys = { "poison1", "poison2", "poison3" };
        private static readonly string[] BurnTopKeys = { "burnTop1", "burnTop2", "burnTop3" };
        private static readonly string[] FractureKeys = { "fracture1", "fracture2", "fracture3", "fracture4" };
        private static readonly string[] EarthKeys = { "earth1", "earth2", "earth3" };
        private static readonly string[] WaterKeys = { "water1", "water2" };
        private static readonly string[] FlashKeys = { "lightning", "air", "corrosion", "fractureChip", "markOn" };
        private static readonly string[] IconNames = { "poison", "burn", "ember", "curse", "mark", "stun", "blind", "fracture", "vulnerable", "earthShred", "waterTime", "weakened" };
        private static readonly string[] ElementIcons = { "fire", "air", "earth", "lightning", "water", "corrosion" };
        private static readonly string[] Stacks = Numbers(string.Empty);
        private static readonly string[] Percents = Numbers("%");
        private static readonly Color CurseColor = new Color32(0xB0, 0x7C, 0xFF, 0xFF);
        private static readonly Color VulnerableColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color WeakenedColor = new Color32(0xA8, 0xA4, 0xA6, 0xFF);
        private static readonly Vector2[] Sides = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        private readonly SpriteRenderer _enemy;
        private readonly SpriteMask _mask;
        private readonly SpriteRenderer[] _loops = new SpriteRenderer[Loops];
        private readonly StatusClip?[] _loopClips = new StatusClip?[Loops];
        private readonly SpriteRenderer[] _flashes = new SpriteRenderer[Flashes];
        private readonly StatusClip?[] _flashClips = new StatusClip?[Flashes];
        private readonly float[] _flashAges = new float[Flashes];
        private readonly SpriteRenderer[] _outline = new SpriteRenderer[OutlineSides];
        private readonly Image[] _badges = new Image[EnemyStatusShow.MaxBadges + BiomeBadges];
        private readonly Image[] _arrows = new Image[BiomeBadges];
        private readonly TextMeshProUGUI[] _numbers = new TextMeshProUGUI[EnemyStatusShow.MaxBadges];
        private readonly int[] _shownNumbers = new int[EnemyStatusShow.MaxBadges];
        private readonly bool[] _badgeFading = new bool[EnemyStatusShow.MaxBadges];
        private StatusOutline _outlineShown;
        private bool _runeLifted;
        private bool _large;
        private float _time;

        private EnemyStatusView(SpriteRenderer enemy, Transform scene, RectTransform hud, UiTheme theme, int sortingOrder)
        {
            _enemy = enemy;
            var maskHost = new GameObject("Status mask");
            maskHost.transform.SetParent(enemy.transform, false);
            _mask = maskHost.AddComponent<SpriteMask>();
            _mask.sprite = enemy.sprite;
            for (var i = 0; i < OutlineSides; i++)
            {
                _outline[i] = ShapeSprites.Create("Status outline " + i, enemy.sprite, Color.white, Vector2.zero, Vector2.one, sortingOrder - 1, scene);
                _outline[i].enabled = false;
            }
            for (var i = 0; i < Loops; i++)
                _loops[i] = Layer("Status " + i, sortingOrder + 1, scene);
            for (var i = 0; i < Flashes; i++)
                _flashes[i] = Layer("Status flash " + i, sortingOrder + 2, scene);
            var centre = new Vector2(0.5f, 0.5f);
            for (var i = 0; i < _badges.Length; i++)
            {
                _badges[i] = UiElements.Icon("Enemy status " + i, hud, null, Color.white);
                StripCanvas.Place(_badges[i].rectTransform, BadgeRightPx - i * BadgeStepPx, BadgeRowPx, Vector2.one * BadgePx, centre);
                _badges[i].gameObject.SetActive(false);
            }
            for (var i = 0; i < EnemyStatusShow.MaxBadges; i++)
            {
                _numbers[i] = UiElements.Label("Stacks", _badges[i].rectTransform, theme.BoldFont, 9f, Color.white, TextAlignmentOptions.BottomRight);
                UiElements.Stretch(_numbers[i].rectTransform, -4f);
                _shownNumbers[i] = -1;
            }
            for (var i = 0; i < BiomeBadges; i++)
            {
                _arrows[i] = UiElements.Icon("Arrow", _badges[EnemyStatusShow.MaxBadges + i].rectTransform, null, Color.white);
                UiElements.Anchor(_arrows[i].rectTransform, new Vector2(1f, 0f), Vector2.zero, Vector2.one * ArrowPx);
            }
        }

        public static EnemyStatusView Create(SpriteRenderer enemy, Transform scene, RectTransform hud, UiTheme theme, int sortingOrder) =>
            new EnemyStatusView(enemy, scene, hud, theme, sortingOrder);

        public Color Tint(Color colour, EnemyStatusShow? show) =>
            show != null && show.Has(EnemyStatus.Weakened) ? Color.Lerp(colour, WeakenedColor, WeakenedBlend) : colour;

        public void Show(EnemyStatusShow show, long tick)
        {
            var slot = 0;
            Use(slot++, show.Has(EnemyStatus.Poison) ? PoisonKeys[show.Tier(EnemyStatus.Poison) - 1] : null);
            Use(slot++, show.Has(EnemyStatus.Burn) ? "burnBody" : null);
            Use(slot++, show.Has(EnemyStatus.Burn) ? BurnTopKeys[show.Tier(EnemyStatus.Burn) - 1] : null);
            Use(slot++, show.ShowsBody(EnemyStatus.Ember) ? "ember" : null);
            Use(slot++, show.Has(EnemyStatus.Fracture) ? FractureKeys[show.Tier(EnemyStatus.Fracture) - 1] : null);
            Use(slot++, show.Has(EnemyStatus.Stun) ? "stun" : null);
            Use(slot++, show.Has(EnemyStatus.Blind) ? "blind" : null);
            Use(slot++, show.Has(EnemyStatus.Curse) ? "curse" : null);
            Use(slot++, show.Has(EnemyStatus.Mark) ? "mark" : null);
            Use(slot++, show.Has(EnemyStatus.EarthShred) ? EarthKeys[show.Tier(EnemyStatus.EarthShred) - 1] : null);
            Use(slot, show.Has(EnemyStatus.WaterTime) ? WaterKeys[show.Tier(EnemyStatus.WaterTime) - 1] : null);
            _runeLifted = show.Has(EnemyStatus.Stun);
            _outlineShown = show.Outline;
            while (show.TryTakeFlash(tick, out var flash))
                Play(flash);
            ShowBadges(show);
        }

        public void Animate(float delta, float bodyUnits, float alpha)
        {
            _time += delta;
            _large = bodyUnits * StripSpace.PixelsPerUnit >= LargeBodyFromPx;
            if (_mask.sprite != _enemy.sprite)
                _mask.sprite = _enemy.sprite;
            var scale = _large ? StatusArt.LargeScale : StatusArt.SmallScale;
            var centre = (Vector2)_enemy.transform.position;
            var half = bodyUnits / 2f;
            for (var i = 0; i < Loops; i++)
            {
                var clip = _loopClips[i];
                var renderer = _loops[i];
                if (renderer.enabled != (clip != null))
                    renderer.enabled = clip != null;
                if (clip == null)
                    continue;
                Place(renderer, clip, clip.Clip.SpriteAt(_time), centre, half, scale, alpha);
            }
            for (var i = 0; i < Flashes; i++)
            {
                var clip = _flashClips[i];
                if (clip == null)
                    continue;
                _flashAges[i] += delta;
                if (_flashAges[i] >= clip.Clip.Duration)
                {
                    _flashClips[i] = null;
                    _flashes[i].enabled = false;
                    continue;
                }
                Place(_flashes[i], clip, clip.Clip.SpriteAt(_flashAges[i]), centre, half, scale, alpha);
            }
            AnimateOutline(centre, scale, alpha);
            AnimateBadges();
        }

        private void Place(SpriteRenderer renderer, StatusClip clip, Sprite sprite, Vector2 centre, float half, float scale, float alpha)
        {
            if (renderer.sprite != sprite)
                renderer.sprite = sprite;
            var mask = clip.Masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            if (renderer.maskInteraction != mask)
                renderer.maskInteraction = mask;
            var lift = clip.Anchor switch
            {
                StatusAnchor.Feet => -half,
                StatusAnchor.Top => half,
                StatusAnchor.Head => half,
                StatusAnchor.Eyes => half - 2f * half * EyesShare,
                _ => 0f
            };
            if (_runeLifted && renderer == _loops[CurseSlot])
                lift += StripSpace.Units(RuneLiftPx * scale);
            var transform = renderer.transform;
            transform.position = centre + new Vector2(0f, lift);
            if (transform.localScale.x != scale)
                transform.localScale = new Vector3(scale, scale, 1f);
            if (renderer.color.a != alpha)
                renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private void AnimateOutline(Vector2 centre, float scale, float alpha)
        {
            var shown = _outlineShown != StatusOutline.None && alpha > 0f;
            var colour = _outlineShown == StatusOutline.Curse ? CurseColor : VulnerableColor;
            colour.a = alpha * (0.7f + 0.3f * Mathf.Sin(_time / OutlinePulseSeconds * Mathf.PI * 2f));
            var step = StripSpace.Units(scale);
            for (var i = 0; i < OutlineSides; i++)
            {
                var renderer = _outline[i];
                if (renderer.enabled != shown)
                    renderer.enabled = shown;
                if (!shown)
                    continue;
                if (renderer.sprite != _enemy.sprite)
                    renderer.sprite = _enemy.sprite;
                renderer.transform.position = centre + Sides[i] * step;
                renderer.transform.localScale = _enemy.transform.localScale;
                renderer.color = colour;
            }
        }

        private void Use(int slot, string? key) => _loopClips[slot] = key == null ? null : StatusArt.Clip(key, _large);

        private void Play(StatusFlash flash)
        {
            var clip = StatusArt.Clip(FlashKeys[(int)flash], _large);
            if (clip == null)
                return;
            var slot = 0;
            for (var i = 0; i < Flashes; i++)
            {
                if (_flashClips[i] == null)
                {
                    slot = i;
                    break;
                }
                if (_flashAges[i] > _flashAges[slot])
                    slot = i;
            }
            _flashClips[slot] = clip;
            _flashAges[slot] = 0f;
            _flashes[slot].enabled = true;
        }

        private void ShowBadges(EnemyStatusShow show)
        {
            var shown = 0;
            for (; shown < show.BadgeCount; shown++)
            {
                ref readonly var badge = ref show.Badge(shown);
                var icon = StatusArt.Icon(IconNames[(int)badge.Status]);
                var image = _badges[shown];
                if (image.sprite != icon)
                    UiElements.Use(image, icon);
                if (!image.gameObject.activeSelf)
                    image.gameObject.SetActive(true);
                _badgeFading[shown] = badge.Fading;
                var number = badge.Kind == StatusNumber.None ? -1 : Mathf.Min(MaxNumber, badge.Number) + (badge.Kind == StatusNumber.Percent ? MaxNumber + 1 : 0);
                if (_shownNumbers[shown] == number)
                    continue;
                _shownNumbers[shown] = number;
                _numbers[shown].text = number < 0 ? string.Empty : number > MaxNumber ? Percents[number - MaxNumber - 1] : Stacks[number];
            }
            for (var i = shown; i < EnemyStatusShow.MaxBadges; i++)
            {
                _badgeFading[i] = false;
                if (_badges[i].gameObject.activeSelf)
                    _badges[i].gameObject.SetActive(false);
            }
            ShowBiome(0, show.Resists, "resists", shown);
            ShowBiome(1, show.WeakTo, "weakTo", shown + (show.Resists == ElementRules.None ? 0 : 1));
        }

        private void ShowBiome(int index, int element, string arrow, int place)
        {
            var image = _badges[EnemyStatusShow.MaxBadges + index];
            var shown = element != ElementRules.None;
            if (image.gameObject.activeSelf != shown)
                image.gameObject.SetActive(shown);
            if (!shown)
                return;
            var icon = StatusArt.Icon(ElementIcons[element]);
            if (image.sprite != icon)
                UiElements.Use(image, icon);
            var arrowIcon = StatusArt.Icon(arrow);
            if (_arrows[index].sprite != arrowIcon)
                UiElements.Use(_arrows[index], arrowIcon);
            var x = BadgeRightPx - place * BadgeStepPx;
            if (image.rectTransform.anchoredPosition.x != x)
                StripCanvas.Place(image.rectTransform, x, BadgeRowPx, Vector2.one * BadgePx, new Vector2(0.5f, 0.5f));
        }

        private void AnimateBadges()
        {
            var dim = Mathf.Repeat(_time, FadeBlinkSeconds * 2f) < FadeBlinkSeconds;
            for (var i = 0; i < EnemyStatusShow.MaxBadges; i++)
            {
                var alpha = _badgeFading[i] && dim ? 0.35f : 1f;
                if (_badges[i].color.a != alpha)
                    _badges[i].color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private static SpriteRenderer Layer(string name, int sortingOrder, Transform scene)
        {
            var renderer = ShapeSprites.Create(name, ShapeSprites.Square, Color.white, Vector2.zero, Vector2.one, sortingOrder, scene);
            renderer.enabled = false;
            return renderer;
        }

        private static string[] Numbers(string suffix)
        {
            var result = new string[MaxNumber + 1];
            for (var i = 0; i <= MaxNumber; i++)
                result[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
            return result;
        }
    }
}
