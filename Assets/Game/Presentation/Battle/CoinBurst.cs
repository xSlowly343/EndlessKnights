using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class CoinBurst : MonoBehaviour
    {
        private const int PoolSize = 24;
        private const int CoinsPerKill = 5;
        private const float FlightSeconds = 0.55f;
        private const float CoinSize = 0.55f;
        private const float ArcHeight = 2.5f;

        private static readonly Color CoinColor = new Color(1f, 0.82f, 0.2f);

        private readonly SpriteRenderer[] _coins = new SpriteRenderer[PoolSize];
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
                burst._coins[i] = ShapeSprites.Create("Coin", ShapeSprites.Circle, CoinColor, Vector2.zero, Vector2.one * CoinSize, 18, host.transform);
                burst._coins[i].gameObject.SetActive(false);
                burst._ages[i] = FlightSeconds;
            }
            return burst;
        }

        public void Burst(Vector2 from)
        {
            for (var c = 0; c < CoinsPerKill; c++)
            {
                var i = _next;
                _next = (_next + 1) % PoolSize;
                _from[i] = from;
                _spread[i] = new Vector2(Random.Range(-2f, 2f), Random.Range(0.5f, 2f));
                _ages[i] = -c * 0.04f;
                _coins[i].gameObject.SetActive(true);
                _coins[i].transform.position = from;
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
                _coins[i].transform.position = position;
                if (t >= 1f)
                {
                    _coins[i].gameObject.SetActive(false);
                    CoinArrived?.Invoke();
                }
            }
        }
    }
}
