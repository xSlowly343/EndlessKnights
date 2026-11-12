using Game.Core.Content.Gems;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class SkillEffects : MonoBehaviour
    {
        private const float SheetScale = 3f;
        private const float NameSeconds = 0.9f;
        private const float WhiteMix = 0.2f;
        private const float GlowPulseSpeed = 4f;
        private const float GlowAlpha = 0.35f;
        private const float NameRisePx = 10f;
        private const int WaveSortingOrder = 8;
        private const int CastSortingOrder = 21;
        private const int NameSortingOrder = 26;
        private const string SparkSheet = "spark";
        private const string WaveSheet = "wave";

        private static readonly string[] FamilySheets = { "hit", "queue", "auto", "strong" };
        private static readonly Vector2 HeroCentre = StripSpace.AtPx(85f, 117f);
        private static readonly Vector2 HeroFeet = StripSpace.AtPx(85f, 150f);
        private static readonly float GroundY = StripSpace.AtPx(0f, 150f).y;
        private static readonly Vector2 GlowSize = new Vector2(StripSpace.Units(78f), StripSpace.Units(90f));
        private static readonly Vector2 NamePosition = StripSpace.AtPx(240f, 60f);

        private readonly HeroClip?[] _families = new HeroClip?[SkillShowFamilies.Count];
        private SheetPlayer _cast = null!;
        private SheetPlayer _spark = null!;
        private SheetPlayer _wave = null!;
        private HeroClip? _sparkClip;
        private HeroClip? _waveClip;
        private SpriteRenderer _glow = null!;
        private TextMeshProUGUI _name = null!;
        private Vector2 _namePx;
        private Color _color = Color.white;
        private float _nameAge = NameSeconds;
        private Color _glowColor;
        private bool _glowing;

        public static float HeavyImpactSeconds => HeroModel.SkillImpactSeconds(FamilySheets[(int)SkillShowFamily.Strong]);

        public static SkillEffects Create()
        {
            var host = new GameObject("SkillEffects");
            var effects = host.AddComponent<SkillEffects>();
            effects.Build();
            return effects;
        }

        public bool CastShown => _cast.Playing;

        public bool SparkShown => _spark.Playing;

        public bool WaveShown => _wave.Playing;

        public void Cast(GemDef gem, Vector2 target, ShowSize size = ShowSize.Full)
        {
            _color = GemColors.Of(gem.Color);
            var tint = Color.Lerp(_color, Color.white, WhiteMix);
            var ground = new Vector2(target.x, GroundY);
            if (size == ShowSize.Small)
            {
                _spark.Play(_sparkClip, ground, tint);
                return;
            }
            _name.text = gem.NameRu;
            _nameAge = 0f;
            if (gem.Skill?.Effect is AuraEffect)
                _wave.Play(_waveClip, HeroFeet, tint);
            else
                _cast.Play(_families[(int)SkillShowFamilies.Of(gem)], ground, tint);
        }

        public void ShowAura(bool active, GemColor color)
        {
            _glowing = active;
            if (active)
                _glowColor = GemColors.Of(color);
        }

        private void Build()
        {
            for (var family = 0; family < _families.Length; family++)
                _families[family] = HeroModel.SkillSheet(FamilySheets[family]);
            _sparkClip = HeroModel.SkillSheet(SparkSheet);
            _waveClip = HeroModel.SkillSheet(WaveSheet);
            _wave = new SheetPlayer("SkillWave", SheetScale, WaveSortingOrder, transform);
            _cast = new SheetPlayer("SkillCast", SheetScale, CastSortingOrder, transform);
            _spark = new SheetPlayer("SkillSpark", SheetScale, CastSortingOrder, transform);
            _glow = ShapeSprites.Create("AuraGlow", ShapeSprites.Circle, Color.clear, HeroCentre, GlowSize, 9, transform);
            var canvas = StripCanvas.Create("SkillName", NameSortingOrder, transform);
            _name = UiElements.Label("Name", canvas, UiTheme.Load().BoldFont, 14f, Color.white, TextAlignmentOptions.Center);
            _namePx = StripCanvas.PxOf(NamePosition);
            StripCanvas.Place(_name.rectTransform, _namePx.x, StripSpace.BaseHeightPx - _namePx.y, new Vector2(300f, 22f), new Vector2(0.5f, 0.5f));
            _glow.gameObject.SetActive(false);
            _name.gameObject.SetActive(false);
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            _cast.Animate(delta);
            _spark.Animate(delta);
            _wave.Animate(delta);
            AnimateGlow();
            AnimateName(delta);
        }

        private void AnimateGlow()
        {
            if (_glow.gameObject.activeSelf != _glowing)
                _glow.gameObject.SetActive(_glowing);
            if (!_glowing)
                return;
            var color = _glowColor;
            color.a = GlowAlpha * (0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * GlowPulseSpeed));
            _glow.color = color;
        }

        private void AnimateName(float delta)
        {
            _nameAge += delta;
            var visible = _nameAge < NameSeconds;
            if (_name.gameObject.activeSelf != visible)
                _name.gameObject.SetActive(visible);
            if (!visible)
                return;
            var progress = _nameAge / NameSeconds;
            var color = _color;
            color.a = 1f - progress * progress;
            _name.color = color;
            _name.rectTransform.anchoredPosition = _namePx + new Vector2(0f, NameRisePx * progress);
        }
    }
}
