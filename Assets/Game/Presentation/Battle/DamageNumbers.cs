using Game.Core.Math;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int PoolSize = 30;
        private const float LifetimeSeconds = 0.6f;
        private const float RiseUnits = 2.2f;
        private const float CharacterSize = 0.2f;
        private const float CritScale = 1.6f;
        private const float MergeGrowth = 0.012f;
        private const float MaxMergeScale = 1.5f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);

        private readonly TextMesh[] _texts = new TextMesh[PoolSize];
        private readonly float[] _ages = new float[PoolSize];
        private readonly Vector2[] _starts = new Vector2[PoolSize];
        private readonly Color[] _colors = new Color[PoolSize];
        private readonly NumberMerge _merge = new NumberMerge();
        private BigNumber _mergedAmount;
        private int _mergedHits;
        private int _mergedIndex = -1;
        private int _next;

        public static DamageNumbers Create()
        {
            var host = new GameObject("DamageNumbers");
            var numbers = host.AddComponent<DamageNumbers>();
            for (var i = 0; i < PoolSize; i++)
            {
                numbers._texts[i] = StripText.Create("Number", string.Empty, Vector2.zero, CharacterSize, TextAnchor.MiddleCenter, Color.white, host.transform, 20);
                numbers._texts[i].gameObject.SetActive(false);
                numbers._ages[i] = LifetimeSeconds;
            }
            return numbers;
        }

        public void Show(BigNumber amount, bool crit, Vector2 at)
        {
            if (_merge.ShouldMerge(Time.unscaledTime, crit) && _mergedIndex >= 0 && _ages[_mergedIndex] < LifetimeSeconds)
            {
                Grow(amount);
                return;
            }

            var index = Take();
            var text = _texts[index];
            text.text = Format(amount);
            text.characterSize = crit ? CharacterSize * CritScale : CharacterSize;
            _colors[index] = crit ? CritColor : NormalColor;
            text.color = _colors[index];
            _starts[index] = at;
            _ages[index] = 0f;
            text.transform.position = at;
            text.gameObject.SetActive(true);

            if (!crit)
            {
                _mergedIndex = index;
                _mergedAmount = amount;
                _mergedHits = 1;
            }
        }

        private void Grow(BigNumber amount)
        {
            _mergedAmount += amount;
            _mergedHits++;
            var text = _texts[_mergedIndex];
            text.text = Format(_mergedAmount);
            text.characterSize = CharacterSize * Mathf.Min(MaxMergeScale, 1f + MergeGrowth * _mergedHits);
            _ages[_mergedIndex] = 0f;
        }

        private int Take()
        {
            var index = _next;
            _next = (_next + 1) % PoolSize;
            if (index == _mergedIndex)
            {
                _mergedIndex = -1;
                _merge.Break();
            }
            return index;
        }

        private static string Format(BigNumber amount) => NumberFormatter.Format(amount, NumberFormatSettings.Default);

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            for (var i = 0; i < PoolSize; i++)
            {
                if (_ages[i] >= LifetimeSeconds)
                    continue;
                _ages[i] += delta;
                var progress = Mathf.Clamp01(_ages[i] / LifetimeSeconds);
                var text = _texts[i];
                var rise = i == _mergedIndex ? RiseUnits * 0.4f * progress : RiseUnits * progress;
                text.transform.position = _starts[i] + new Vector2(0f, rise);
                var color = _colors[i];
                color.a = 1f - progress * progress;
                text.color = color;
                if (progress >= 1f)
                {
                    text.gameObject.SetActive(false);
                    if (i == _mergedIndex)
                    {
                        _mergedIndex = -1;
                        _merge.Break();
                    }
                }
            }
        }
    }
}
