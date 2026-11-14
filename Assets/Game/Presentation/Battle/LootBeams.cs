using Game.Core.Content.Items;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class LootBeams
    {
        private const int PoolSize = 4;
        private const int Layers = 3;
        private const float ArtScale = 3f;
        private const float RiseSeconds = 0.15f;
        private const float FadeSeconds = 0.4f;
        private const float StepPx = 24f;

        private readonly SpriteRenderer[,] _renderers = new SpriteRenderer[PoolSize, Layers];
        private readonly HeroClip?[,] _clips = new HeroClip?[PoolSize, Layers];
        private readonly float[] _ages = new float[PoolSize];
        private readonly float[] _seconds = new float[PoolSize];
        private readonly SheetPlayer _opening;
        private readonly Vector2 _openingAt;
        private int _next;

        private LootBeams(Vector2 ground, Vector2 openingAt, int sortingOrder, Transform scene)
        {
            _openingAt = openingAt;
            for (var i = 0; i < PoolSize; i++)
            {
                var position = ground - new Vector2(StripSpace.Units(StepPx) * i, 0f);
                for (var layer = 0; layer < Layers; layer++)
                {
                    _renderers[i, layer] = ShapeSprites.Create("Loot " + i + " " + layer, ShapeSprites.Square, Color.white, position, Vector2.one * ArtScale, sortingOrder + layer, scene);
                    _renderers[i, layer].enabled = false;
                }
            }
            _opening = new SheetPlayer("Loot opening", ArtScale, sortingOrder + Layers, scene);
        }

        public static LootBeams Create(Vector2 ground, Vector2 openingAt, int sortingOrder, Transform scene) => new LootBeams(ground, openingAt, sortingOrder, scene);

        public void Show(ItemRarity rarity, bool set, bool unique)
        {
            var beam = LootArt.Beam(rarity);
            if (beam == null)
                return;
            var slot = _next;
            _next = (_next + 1) % PoolSize;
            _clips[slot, 0] = beam;
            _clips[slot, 1] = unique ? LootArt.Sheet("uniqueMark") : set ? LootArt.Sheet("setMark") : null;
            _clips[slot, 2] = LootArt.Sheet("bundle");
            _ages[slot] = 0f;
            _seconds[slot] = LootArt.BeamSeconds(rarity);
        }

        public void OpenChest() => _opening.Play(LootArt.Sheet("chestOpen"), _openingAt, Color.white);

        public void OpenLetter() => _opening.Play(LootArt.Sheet("letterOpen"), _openingAt, Color.white);

        public void Animate(float delta)
        {
            _opening.Animate(delta);
            for (var i = 0; i < PoolSize; i++)
            {
                if (_seconds[i] <= 0f)
                    continue;
                _ages[i] += delta;
                var done = _ages[i] >= _seconds[i];
                var rise = Mathf.Clamp01(_ages[i] / RiseSeconds);
                var alpha = Mathf.Clamp01((_seconds[i] - _ages[i]) / FadeSeconds);
                for (var layer = 0; layer < Layers; layer++)
                {
                    var renderer = _renderers[i, layer];
                    var clip = done ? null : _clips[i, layer];
                    if (renderer.enabled != (clip != null))
                        renderer.enabled = clip != null;
                    if (clip == null)
                        continue;
                    var sprite = clip.SpriteAt(_ages[i]);
                    if (renderer.sprite != sprite)
                        renderer.sprite = sprite;
                    renderer.transform.localScale = new Vector3(ArtScale, ArtScale * (layer == 2 ? 1f : rise), 1f);
                    renderer.color = new Color(1f, 1f, 1f, alpha);
                }
                if (done)
                    _seconds[i] = 0f;
            }
        }
    }
}
