using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class HeroStrike
    {
        private const int Flights = 2;
        private const int Bursts = 3;
        private const float FlightEase = 0.85f;

        private static readonly Vector2 LaunchFromFeetPx = new Vector2(40f, 13f);

        private readonly SpriteRenderer _slash;
        private readonly SpriteRenderer[] _crescents = new SpriteRenderer[Flights];
        private readonly SpriteRenderer[] _bursts = new SpriteRenderer[Bursts];
        private readonly float[] _flightAge = new float[Flights];
        private readonly float[] _burstAge = new float[Bursts];
        private readonly Vector2[] _targets = new Vector2[Flights];
        private readonly Vector3 _from;
        private readonly float _depth;
        private Color _color = Color.white;

        public HeroStrike(string name, Vector2 feet, float scale, int flightSortingOrder, int burstSortingOrder, float depth, Transform parent)
        {
            _depth = depth;
            _from = Deep(feet + LaunchFromFeetPx * (scale / StripSpace.PixelsPerUnit));
            _slash = Create(name + " slash", HeroModel.Slash, Deep(feet), scale, flightSortingOrder, parent);
            for (var i = 0; i < Flights; i++)
            {
                _crescents[i] = Create(name + " crescent " + i, HeroModel.Crescent, _from, scale, flightSortingOrder, parent);
                _flightAge[i] = float.MaxValue;
            }
            for (var i = 0; i < Bursts; i++)
            {
                _bursts[i] = Create(name + " burst " + i, HeroModel.Burst, _from, scale, burstSortingOrder, parent);
                _burstAge[i] = float.MaxValue;
            }
        }

        public float Opacity => _color.a;

        public bool SlashShown => _slash.enabled;

        public int Flying => Count(_flightAge, HeroModel.Crescent.Duration);

        public int Bursting => Count(_burstAge, HeroModel.Burst.Duration);

        public void UseOpacity(float opacity)
        {
            _color.a = Mathf.Clamp01(opacity);
            Recolor();
        }

        public void UseTint(Color tint)
        {
            if (_color.r == tint.r && _color.g == tint.g && _color.b == tint.b)
                return;
            _color = new Color(tint.r, tint.g, tint.b, _color.a);
            Recolor();
        }

        public void ShowSlash(float attackAge)
        {
            var clip = HeroModel.Slash;
            var shown = Visible && attackAge >= HeroModel.StrikeSeconds && attackAge < clip.Duration;
            if (_slash.enabled != shown)
                _slash.enabled = shown;
            if (!shown)
                return;
            var sprite = clip.SpriteAt(attackAge);
            if (_slash.sprite != sprite)
                _slash.sprite = sprite;
        }

        public void Launch(Vector2 target)
        {
            var slot = Oldest(_flightAge);
            _flightAge[slot] = 0f;
            _targets[slot] = target;
        }

        public void Animate(float delta)
        {
            for (var i = 0; i < Flights; i++)
            {
                if (_flightAge[i] >= HeroModel.Crescent.Duration)
                    continue;
                _flightAge[i] += delta;
                if (_flightAge[i] >= HeroModel.Crescent.Duration)
                {
                    var slot = Oldest(_burstAge);
                    _burstAge[slot] = 0f;
                    _bursts[slot].transform.position = Deep(_targets[i]);
                }
                else
                {
                    var progress = Mathf.Pow(_flightAge[i] / HeroModel.Crescent.Duration, FlightEase);
                    _crescents[i].transform.position = Vector3.Lerp(_from, Deep(_targets[i]), progress);
                }
                Show(_crescents[i], HeroModel.Crescent, _flightAge[i]);
            }
            for (var i = 0; i < Bursts; i++)
            {
                if (_burstAge[i] >= HeroModel.Burst.Duration)
                    continue;
                Show(_bursts[i], HeroModel.Burst, _burstAge[i]);
                _burstAge[i] += delta;
                if (_burstAge[i] >= HeroModel.Burst.Duration)
                    _bursts[i].enabled = false;
            }
        }

        private bool Visible => _color.a > 0f;

        private Vector3 Deep(Vector2 point) => new Vector3(point.x, point.y, _depth);

        private void Show(SpriteRenderer renderer, HeroClip clip, float age)
        {
            var shown = Visible && age < clip.Duration;
            if (renderer.enabled != shown)
                renderer.enabled = shown;
            if (!shown)
                return;
            var sprite = clip.SpriteAt(age);
            if (renderer.sprite != sprite)
                renderer.sprite = sprite;
        }

        private void Recolor()
        {
            _slash.color = _color;
            for (var i = 0; i < Flights; i++)
                _crescents[i].color = _color;
            for (var i = 0; i < Bursts; i++)
                _bursts[i].color = _color;
        }

        private SpriteRenderer Create(string name, HeroClip clip, Vector3 position, float scale, int sortingOrder, Transform parent)
        {
            var renderer = ShapeSprites.Create(name, clip.Frame(0), _color, position, Vector2.one * scale, sortingOrder, parent);
            renderer.transform.position = position;
            renderer.enabled = false;
            return renderer;
        }

        private static int Count(float[] ages, float duration)
        {
            var count = 0;
            for (var i = 0; i < ages.Length; i++)
            {
                if (ages[i] < duration)
                    count++;
            }
            return count;
        }

        private static int Oldest(float[] ages)
        {
            var oldest = 0;
            for (var i = 1; i < ages.Length; i++)
            {
                if (ages[i] > ages[oldest])
                    oldest = i;
            }
            return oldest;
        }
    }
}
