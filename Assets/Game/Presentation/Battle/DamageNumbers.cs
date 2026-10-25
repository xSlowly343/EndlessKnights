using Game.Core.Math;
using Game.Core.Ui;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int PoolSize = 30;
        private const float LifetimeSeconds = 0.6f;
        private const float RisePx = 31f;
        private const float FontSizePx = 18f;
        private const float CritFontSizePx = 24f;
        private const float BoxWidthPx = 160f;
        private const float BoxHeightPx = 32f;
        private const int SortingOrder = 27;
        private const float MergeGrowth = 0.012f;
        private const float MaxMergeScale = 1.5f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color CritColor = new Color32(0xF0, 0xB4, 0x3C, 0xFF);

        private readonly TextMeshProUGUI[] _texts = new TextMeshProUGUI[PoolSize];
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
            var canvas = StripCanvas.Create("DamageNumbers", SortingOrder);
            var numbers = canvas.gameObject.AddComponent<DamageNumbers>();
            var font = UiTheme.Load().NumberFont;
            for (var i = 0; i < PoolSize; i++)
            {
                var text = UiElements.Label("Number", canvas, font, FontSizePx, NormalColor, TextAlignmentOptions.Center);
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.zero;
                text.rectTransform.sizeDelta = new Vector2(BoxWidthPx, BoxHeightPx);
                text.gameObject.SetActive(false);
                numbers._texts[i] = text;
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
            text.text = crit ? Format(amount) + " " + BattleTexts.Crit : Format(amount);
            text.fontSize = crit ? CritFontSizePx : FontSizePx;
            _colors[index] = crit ? CritColor : NormalColor;
            text.color = _colors[index];
            _starts[index] = StripCanvas.PxOf(at);
            _ages[index] = 0f;
            text.rectTransform.anchoredPosition = _starts[index];
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
            text.fontSize = FontSizePx * Mathf.Min(MaxMergeScale, 1f + MergeGrowth * _mergedHits);
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
                var rise = i == _mergedIndex ? RisePx * 0.4f * progress : RisePx * progress;
                text.rectTransform.anchoredPosition = _starts[i] + new Vector2(0f, rise);
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
