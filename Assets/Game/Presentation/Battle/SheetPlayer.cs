using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class SheetPlayer
    {
        private readonly SpriteRenderer _renderer;
        private HeroClip? _clip;
        private float _age;

        public SheetPlayer(string name, float scale, int sortingOrder, Transform parent)
        {
            _renderer = ShapeSprites.Create(name, ShapeSprites.Square, Color.white, Vector2.zero, Vector2.one * scale, sortingOrder, parent);
            _renderer.enabled = false;
        }

        public bool Playing => _clip != null;

        public void Play(HeroClip? clip, Vector2 position, Color color)
        {
            if (clip == null)
                return;
            _clip = clip;
            _age = 0f;
            _renderer.transform.position = position;
            _renderer.color = color;
            _renderer.sprite = clip.Frame(0);
            _renderer.enabled = true;
        }

        public void Animate(float delta)
        {
            if (_clip == null)
                return;
            _age += delta;
            if (_age >= _clip.Duration)
            {
                _clip = null;
                _renderer.enabled = false;
                return;
            }
            var sprite = _clip.SpriteAt(_age);
            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;
        }
    }
}
