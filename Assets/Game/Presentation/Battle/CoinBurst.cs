using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class CoinBurst : MonoBehaviour
    {
        private const int PoolSize = 32;
        private const int CoinsPerKill = 5;
        private const float FlightSeconds = 0.55f;
        private const float ShapeSize = 0.55f;
        private const float SheetScale = 2f;
        private const float ArcHeight = 2.5f;
        private const float LaunchGapSeconds = 0.04f;

        private static readonly Color CoinColor = new Color(1f, 0.82f, 0.2f);

        private readonly SpriteRenderer[] _pieces = new SpriteRenderer[PoolSize];
        private readonly HeroClip?[] _clips = new HeroClip?[PoolSize];
        private readonly bool[] _countsAsCoin = new bool[PoolSize];
        private readonly Vector2[] _from = new Vector2[PoolSize];
        private readonly Vector2[] _spread = new Vector2[PoolSize];
        private readonly float[] _ages = new float[PoolSize];
        private Vector2 _target;
        private int _next;

        public event System.Action? CoinArrived;

        public static CoinBurst Create(Vector2 target)
        {
            var host = new GameObject("Coins");
            var burst = host.AddComponent<CoinBurst>();
            burst._target = target;
            for (var i = 0; i < PoolSize; i++)
            {
                burst._pieces[i] = ShapeSprites.Create("Coin", ShapeSprites.Circle, CoinColor, Vector2.zero, Vector2.one * ShapeSize, 18, host.transform);
                burst._pieces[i].gameObject.SetActive(false);
                burst._ages[i] = FlightSeconds;
            }
            return burst;
        }

        public void Burst(Vector2 from) => Launch(from, CoinsPerKill, LootArt.Sheet("coin"), CoinColor, true);

        public void BurstGems(Vector2 from, int count, Color colour) => Launch(from, count, LootArt.Sheet("gem"), colour, false);

        public void BurstSparks(Vector2 from, int count) => Launch(from, count, LootArt.Sheet("spark"), Color.white, false);

        private void Launch(Vector2 from, int count, HeroClip? clip, Color colour, bool coin)
        {
            for (var c = 0; c < count; c++)
            {
                var i = _next;
                _next = (_next + 1) % PoolSize;
                _from[i] = from;
                _spread[i] = new Vector2(Random.Range(-2f, 2f), Random.Range(0.5f, 2f));
                _ages[i] = -c * LaunchGapSeconds;
                _clips[i] = clip;
                _countsAsCoin[i] = coin;
                var piece = _pieces[i];
                piece.sprite = clip == null ? ShapeSprites.Circle : clip.Frame(0);
                piece.color = clip == null || !coin ? colour : Color.white;
                var size = clip == null ? ShapeSize : SheetScale;
                piece.transform.localScale = new Vector3(size, size, 1f);
                piece.gameObject.SetActive(true);
                piece.transform.position = from;
            }
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            for (var i = 0; i < PoolSize; i++)
            {
                if (_ages[i] >= FlightSeconds)
                    continue;
                _ages[i] += delta;
                var t = Mathf.Clamp01(_ages[i] / FlightSeconds);
                var start = _from[i] + _spread[i] * 0.6f;
                var position = Vector2.Lerp(start, _target, t * t) + new Vector2(0f, ArcHeight * Mathf.Sin(t * Mathf.PI) * (1f - t));
                var piece = _pieces[i];
                piece.transform.position = position;
                var clip = _clips[i];
                if (clip != null)
                {
                    var sprite = clip.SpriteAt(Mathf.Max(0f, _ages[i]) + i * 0.07f);
                    if (piece.sprite != sprite)
                        piece.sprite = sprite;
                }
                if (t >= 1f)
                {
                    piece.gameObject.SetActive(false);
                    if (_countsAsCoin[i])
                        CoinArrived?.Invoke();
                }
            }
        }
    }
}
