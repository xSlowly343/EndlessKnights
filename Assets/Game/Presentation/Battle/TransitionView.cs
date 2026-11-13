using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class TransitionView
    {
        private const float ArtScale = 3f;
        private const float PortalOffsetPx = 26f;
        private const float GroundPx = 150f;
        private const float HeroCentrePx = 85f;
        private const int PortalSortingOrder = 9;
        private const int StepSortingOrder = 12;
        private const int VeilSortingOrder = 20;
        private const int LoadingSortingOrder = 21;

        private readonly SpriteRenderer _portal;
        private readonly SpriteRenderer _step;
        private readonly SpriteRenderer _veil;
        private readonly SpriteRenderer _loading;
        private float _loadingAge;

        private TransitionView(Transform scene)
        {
            _portal = Renderer("Portal", PortalSortingOrder, scene, StripSpace.AtPx(HeroCentrePx + PortalOffsetPx, GroundPx));
            _step = Renderer("Portal step", StepSortingOrder, scene, StripSpace.AtPx(HeroCentrePx, GroundPx));
            _loading = Renderer("Portal loading", LoadingSortingOrder, scene, StripSpace.Centre);
            _veil = ShapeSprites.Create("Portal veil", ShapeSprites.Square, Color.clear, StripSpace.Centre, new Vector2(StripSpace.Width, StripSpace.Height), VeilSortingOrder, scene);
            _veil.enabled = false;
        }

        public static TransitionView Create(Transform scene) => new TransitionView(scene);

        public float HeroAlpha { get; private set; } = 1f;

        public float HeroScale { get; private set; } = 1f;

        public void Show(StripTransition transition, float time, float delta)
        {
            HeroAlpha = (float)TransitionLook.HeroAlpha(transition);
            HeroScale = (float)TransitionLook.HeroScale(transition);
            ShowPortal(transition, time);
            ShowStep(transition);
            ShowVeil(transition);
            ShowLoading(transition, delta);
        }

        private void ShowPortal(StripTransition transition, float time)
        {
            var part = TransitionLook.Portal(transition);
            var clip = TransitionArt.Clip(TransitionLook.PortalPlace(transition), part);
            Use(_portal, clip, part == PortalPart.Idle ? time : (float)transition.PhaseProgress * (clip?.Duration ?? 0f));
        }

        private void ShowStep(StripTransition transition)
        {
            var sheet = transition.Phase == TransitionPhase.Enter ? TransitionSheet.StepIn : TransitionSheet.Arrive;
            var clip = transition.Phase == TransitionPhase.Enter || transition.Phase == TransitionPhase.Arrive ? TransitionArt.Clip(transition.To, sheet) : null;
            Use(_step, clip, (float)transition.PhaseProgress * (clip?.Duration ?? 0f));
        }

        private void ShowVeil(StripTransition transition)
        {
            var alpha = (float)TransitionLook.VeilAlpha(transition);
            var shown = alpha > 0f;
            if (_veil.enabled != shown)
                _veil.enabled = shown;
            if (!shown)
                return;
            var color = TransitionArt.Veil(transition.To);
            color.a *= alpha;
            _veil.color = color;
        }

        private void ShowLoading(StripTransition transition, float delta)
        {
            if (!TransitionLook.ShowsLoading(transition))
            {
                _loadingAge = 0f;
                Use(_loading, null, 0f);
                return;
            }
            var clip = TransitionArt.Clip(transition.To, TransitionSheet.Loading);
            _loadingAge += delta;
            Use(_loading, clip, _loadingAge);
            if (!transition.Loaded && (clip == null || _loadingAge >= clip.Duration))
                transition.MarkLoaded();
        }

        private static void Use(SpriteRenderer renderer, HeroClip? clip, float age)
        {
            var shown = clip != null;
            if (renderer.enabled != shown)
                renderer.enabled = shown;
            if (clip == null)
                return;
            var sprite = clip.SpriteAt(clip.Loops ? age : Mathf.Min(age, clip.Duration - 0.0001f));
            if (renderer.sprite != sprite)
                renderer.sprite = sprite;
        }

        private static SpriteRenderer Renderer(string name, int sortingOrder, Transform scene, Vector2 position)
        {
            var renderer = ShapeSprites.Create(name, ShapeSprites.Square, Color.white, position, Vector2.one * ArtScale, sortingOrder, scene);
            renderer.enabled = false;
            return renderer;
        }
    }
}
