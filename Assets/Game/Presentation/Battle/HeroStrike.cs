using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class HeroStrike
    {
        private const int Flights = 2;
        private const int Bursts = 3;
        private const float FlightEase = 0.85f;
        private const float TierGrowth = 0.25f;

        public const float FlightSeconds = 0.15f;

        private static readonly Vector2 LaunchFromFeetPx = new Vector2(40f, 13f);

        private readonly SpriteRenderer _slash;
        private readonly SpriteRenderer[] _crescents = new SpriteRenderer[Flights];
        private readonly SpriteRenderer[] _bursts = new SpriteRenderer[Bursts];
        private readonly float[] _flightAge = new float[Flights];
        private readonly float[] _burstAge = new float[Bursts];
        private readonly int[] _flightTier = new int[Flights];
        private readonly float _scale;
        private readonly Vector3 _from;
        private readonly Vector3 _groundFrom;
        private readonly float _groundY;
        private readonly float _pixel;
        private readonly float _depth;
        private Color _color = Color.white;
        private float _opacity = 1f;
        private float _dim = 1f;
        private StrikeArt _art;

        public HeroStrike(string name, Vector2 feet, float scale, int flightSortingOrder, int burstSortingOrder, float depth, Transform parent)
        {
            _depth = depth;
            _scale = scale;
            _art = HeroModel.Strike(HeroModel.DefaultLook);
            _pixel = scale / StripSpace.PixelsPerUnit;
            _groundY = feet.y;
            _from = Deep(feet + LaunchFromFeetPx * _pixel);
            _groundFrom = Deep(feet + new Vector2(LaunchFromFeetPx.x * _pixel, 0f));
            _slash = Create(name + " slash", _art.Slash, Deep(feet), scale, flightSortingOrder, parent);
            for (var i = 0; i < Flights; i++)
            {
                _crescents[i] = Create(name + " crescent " + i, _art.Flight, _from, scale, flightSortingOrder, parent);
                _flightAge[i] = float.MaxValue;
            }
            for (var i = 0; i < Bursts; i++)
            {
                _bursts[i] = Create(name + " burst " + i, _art.Burst, _from, scale, burstSortingOrder, parent);
                _burstAge[i] = float.MaxValue;
            }
        }

        public float Opacity => _opacity;

        public bool SlashShown => _slash.enabled;

        public int Flying => Count(_flightAge, FlightSeconds);

        public int Bursting => Count(_burstAge, _art.Burst.Duration);

        public StrikeArt Art => _art;

        public void Use(StrikeArt art)
        {
            if (ReferenceEquals(_art, art))
                return;
            _art = art;
            for (var i = 0; i < Flights; i++)
            {
                _flightAge[i] = float.MaxValue;
                _crescents[i].enabled = false;
            }
            for (var i = 0; i < Bursts; i++)
            {
                _burstAge[i] = float.MaxValue;
                _bursts[i].enabled = false;
            }
        }

        public void UseOpacity(float opacity)
        {
            _opacity = Mathf.Clamp01(opacity);
            Recolor();
        }

        public void UseDim(float dim)
        {
            if (_dim == dim)
                return;
            _dim = Mathf.Clamp01(dim);
            Recolor();
        }

        public void ShowSlash(float attackAge)
        {
            var clip = _art.Slash;
            var shown = Visible && attackAge >= HeroModel.StrikeSeconds && attackAge < clip.Duration;
            if (_slash.enabled != shown)
                _slash.enabled = shown;
            if (!shown)
                return;
            var sprite = clip.SpriteAt(attackAge);
            if (_slash.sprite != sprite)
                _slash.sprite = sprite;
        }

        public void Launch(int tier = 1)
        {
            var slot = Oldest(_flightAge);
            _flightAge[slot] = 0f;
            _flightTier[slot] = tier;
            Resize(_crescents[slot], tier);
        }

        public void Animate(float delta, Vector2 target)
        {
            var clipSpeed = _art.Flight.Duration / FlightSeconds;
            var from = _art.RunsOnGround ? _groundFrom : _from;
            var flightTarget = Deep(_art.RunsOnGround ? new Vector2(target.x, _groundY) : target);
            var burstTarget = Deep(_art.BurstsOnGround ? new Vector2(target.x, _groundY) : target);
            for (var i = 0; i < Flights; i++)
            {
                if (_flightAge[i] >= FlightSeconds)
                    continue;
                _flightAge[i] += delta;
                if (_flightAge[i] >= FlightSeconds)
                {
                    var slot = Oldest(_burstAge);
                    _burstAge[slot] = 0f;
                    _bursts[slot].transform.position = burstTarget;
                    Resize(_bursts[slot], _flightTier[i]);
                }
                else
                {
                    var progress = Mathf.Pow(_flightAge[i] / FlightSeconds, FlightEase);
                    var position = Vector3.Lerp(from, flightTarget, progress);
                    position.y += Mathf.Sin(progress * Mathf.PI) * _art.ArcPx * _pixel;
                    _crescents[i].transform.position = position;
                }
                Show(_crescents[i], _art.Flight, _flightAge[i] * clipSpeed);
            }
            for (var i = 0; i < Bursts; i++)
            {
                if (_burstAge[i] >= _art.Burst.Duration)
                    continue;
                Show(_bursts[i], _art.Burst, _burstAge[i]);
                _burstAge[i] += delta;
                if (_burstAge[i] >= _art.Burst.Duration)
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

        private void Resize(SpriteRenderer renderer, int tier)
        {
            var size = _scale * (1f + TierGrowth * Mathf.Max(0, tier - 1));
            renderer.transform.localScale = new Vector3(size, size, 1f);
        }

        private void Recolor()
        {
            _color.a = _opacity * _dim;
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
