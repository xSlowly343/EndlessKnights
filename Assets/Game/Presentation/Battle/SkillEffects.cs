using Game.Core.Content.Gems;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class SkillEffects : MonoBehaviour
    {
        private const float TrailSeconds = 0.18f;
        private const float RingSeconds = 0.35f;
        private const float WaveSeconds = 0.4f;
        private const float NameSeconds = 0.9f;
        private const float TrailHeightPx = 3f;
        private const float RingStartPx = 20f;
        private const float RingEndPx = 90f;
        private const float WaveAlpha = 0.35f;
        private const float GlowPulseSpeed = 4f;
        private const float GlowAlpha = 0.35f;
        private const float NameRisePx = 10f;

        private static readonly Vector2 HeroCentre = StripSpace.AtPx(85f, 117f);
        private static readonly Vector2 EnemyCentre = StripSpace.AtPx(416f, 108f);
        private static readonly Vector2 GlowSize = new Vector2(StripSpace.Units(78f), StripSpace.Units(90f));
        private static readonly Vector2 WaveCentre = StripSpace.AtPx(240f, 113f);
        private static readonly Vector2 WaveSize = new Vector2(StripSpace.Width, StripSpace.Units(74f));
        private static readonly Vector2 NamePosition = StripSpace.AtPx(240f, 60f);

        private SpriteRenderer _trail = null!;
        private SpriteRenderer _ring = null!;
        private SpriteRenderer _wave = null!;
        private SpriteRenderer _glow = null!;
        private TextMesh _name = null!;
        private Color _color = Color.white;
        private float _trailAge = TrailSeconds;
        private float _ringAge = RingSeconds;
        private float _waveAge = WaveSeconds;
        private float _nameAge = NameSeconds;
        private Color _glowColor;
        private bool _glowing;

        public static SkillEffects Create()
        {
            var host = new GameObject("SkillEffects");
            var effects = host.AddComponent<SkillEffects>();
            effects.Build();
            return effects;
        }

        public void Cast(GemDef gem)
        {
            _color = GemColors.Of(gem.Color);
            _name.text = gem.NameRu;
            _nameAge = 0f;
            if (gem.Skill?.Effect is AuraEffect)
            {
                _waveAge = 0f;
                return;
            }
            _trailAge = 0f;
            _ringAge = 0f;
        }

        public void ShowAura(bool active, GemColor color)
        {
            _glowing = active;
            if (active)
                _glowColor = GemColors.Of(color);
        }

        private void Build()
        {
            var length = EnemyCentre.x - HeroCentre.x;
            var trailCentre = (HeroCentre + EnemyCentre) / 2f;
            var angle = Mathf.Atan2(EnemyCentre.y - HeroCentre.y, length) * Mathf.Rad2Deg;
            _trail = ShapeSprites.Create("SkillTrail", ShapeSprites.Square, Color.clear, trailCentre, new Vector2(length, StripSpace.Units(TrailHeightPx)), 20, transform);
            _trail.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _ring = ShapeSprites.Create("SkillRing", ShapeSprites.Circle, Color.clear, EnemyCentre, Vector2.one * StripSpace.Units(RingStartPx), 21, transform);
            _wave = ShapeSprites.Create("SkillWave", ShapeSprites.Square, Color.clear, WaveCentre, WaveSize, 8, transform);
            _glow = ShapeSprites.Create("AuraGlow", ShapeSprites.Circle, Color.clear, HeroCentre, GlowSize, 9, transform);
            _name = StripText.Create("SkillName", string.Empty, NamePosition, 0.16f, TextAnchor.MiddleCenter, Color.white, transform, 26);
            HideAll();
        }

        private void HideAll()
        {
            _trail.gameObject.SetActive(false);
            _ring.gameObject.SetActive(false);
            _wave.gameObject.SetActive(false);
            _glow.gameObject.SetActive(false);
            _name.gameObject.SetActive(false);
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            AnimateTrail(delta);
            AnimateRing(delta);
            AnimateWave(delta);
            AnimateGlow();
            AnimateName(delta);
        }

        private void AnimateTrail(float delta)
        {
            _trailAge += delta;
            if (!Fade(_trail, _trailAge, TrailSeconds, 1f))
                return;
            var thickness = StripSpace.Units(TrailHeightPx) * (1f - _trailAge / TrailSeconds * 0.6f);
            var scale = _trail.transform.localScale;
            _trail.transform.localScale = new Vector3(scale.x, thickness, 1f);
        }

        private void AnimateRing(float delta)
        {
            _ringAge += delta;
            if (!Fade(_ring, _ringAge, RingSeconds, 0.8f))
                return;
            var size = StripSpace.Units(Mathf.Lerp(RingStartPx, RingEndPx, _ringAge / RingSeconds));
            _ring.transform.localScale = new Vector3(size, size, 1f);
        }

        private void AnimateWave(float delta)
        {
            _waveAge += delta;
            Fade(_wave, _waveAge, WaveSeconds, WaveAlpha);
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
            _name.transform.position = NamePosition + new Vector2(0f, StripSpace.Units(NameRisePx) * progress);
        }

        private bool Fade(SpriteRenderer sprite, float age, float seconds, float startAlpha)
        {
            var visible = age < seconds;
            if (sprite.gameObject.activeSelf != visible)
                sprite.gameObject.SetActive(visible);
            if (!visible)
                return false;
            var color = _color;
            color.a = startAlpha * (1f - age / seconds);
            sprite.color = color;
            return true;
        }
    }
}
