using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class RiftSkyView
    {
        private const float ArtScale = 3f;
        private const int SkySortingOrder = 1;
        private const int EdgeSortingOrder = 19;
        private const int MotesSortingOrder = 8;
        private const float FadeSeconds = 1.2f;

        private static readonly string[] Parts = { "sky", "edgeLeft", "edgeRight", "motes" };

        private readonly SpriteRenderer[] _renderers = new SpriteRenderer[Parts.Length];
        private float _shown;

        private RiftSkyView(Transform scene)
        {
            var positions = new[]
            {
                new Vector2(StripSpace.Width / 2f, StripSpace.Height),
                new Vector2(0f, StripSpace.Height / 2f),
                new Vector2(StripSpace.Width, StripSpace.Height / 2f),
                StripSpace.Centre
            };
            var orders = new[] { SkySortingOrder, EdgeSortingOrder, EdgeSortingOrder, MotesSortingOrder };
            for (var i = 0; i < Parts.Length; i++)
            {
                _renderers[i] = ShapeSprites.Create("Rift " + Parts[i], ShapeSprites.Square, Color.white, positions[i], Vector2.one * ArtScale, orders[i], scene);
                _renderers[i].enabled = false;
            }
        }

        public static RiftSkyView Create(Transform scene) => new RiftSkyView(scene);

        public void Animate(bool inside, float time, float delta)
        {
            _shown = Mathf.MoveTowards(_shown, inside ? 1f : 0f, delta / FadeSeconds);
            for (var i = 0; i < Parts.Length; i++)
            {
                var clip = _shown > 0f ? TransitionArt.Sky(Parts[i]) : null;
                var renderer = _renderers[i];
                if (renderer.enabled != (clip != null))
                    renderer.enabled = clip != null;
                if (clip == null)
                    continue;
                var sprite = clip.SpriteAt(time);
                if (renderer.sprite != sprite)
                    renderer.sprite = sprite;
                if (renderer.color.a != _shown)
                    renderer.color = new Color(1f, 1f, 1f, _shown);
            }
        }
    }
}
