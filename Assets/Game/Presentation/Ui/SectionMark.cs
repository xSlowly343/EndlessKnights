using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class SectionMark
    {
        private const float DotPx = 8f;
        private const float LockWidthPx = 10f;
        private const float LockHeightPx = 12f;
        private const float ShacklePx = 8f;
        private const float LockBodyHeightPx = 7f;
        private const float ConditionSizePx = 10f;
        private const float LockedLabelAlpha = 0.45f;

        private readonly TextMeshProUGUI _label;
        private readonly Image _dot;
        private readonly RectTransform _lock;
        private readonly TextMeshProUGUI _condition;
        private readonly GameObject? _chevron;

        private SectionMark(TextMeshProUGUI label, Image dot, RectTransform lockIcon, TextMeshProUGUI condition, GameObject? chevron)
        {
            _label = label;
            _dot = dot;
            _lock = lockIcon;
            _condition = condition;
            _chevron = chevron;
        }

        public bool DotShown => _dot.gameObject.activeSelf;
        public bool LockShown => _lock.gameObject.activeSelf;
        public string Condition => _condition.text;

        public static SectionMark ForTab(UiButton tab, UiTheme theme)
        {
            var dot = UiElements.Dot("Dot", tab.Rect, theme.Alert);
            UiElements.Anchor(dot.rectTransform, Vector2.one, new Vector2(-10f, -9f), Vector2.one * DotPx);
            var lockIcon = LockIcon(tab.Rect, theme.TextDim);
            UiElements.Anchor(lockIcon, new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(LockWidthPx, LockHeightPx));
            var condition = UiElements.Label("Condition", tab.Rect, theme.TextFont, ConditionSizePx, theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(condition.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(76f, 14f));
            return new SectionMark(tab.Label!, dot, lockIcon, condition, null);
        }

        public static SectionMark ForRow(UiButton row, GameObject chevron, UiTheme theme)
        {
            var middleRight = new Vector2(1f, 0.5f);
            var dot = UiElements.Dot("Dot", row.Rect, theme.Alert);
            UiElements.Anchor(dot.rectTransform, middleRight, new Vector2(-34f, 0f), Vector2.one * DotPx);
            var condition = UiElements.Label("Condition", row.Rect, theme.TextFont, ConditionSizePx + 2f, theme.TextDim, TextAlignmentOptions.MidlineRight);
            UiElements.Anchor(condition.rectTransform, middleRight, new Vector2(-16f, 0f), new Vector2(72f, 20f));
            var lockIcon = LockIcon(row.Rect, theme.TextDim);
            UiElements.Anchor(lockIcon, middleRight, new Vector2(-92f, 0f), new Vector2(LockWidthPx, LockHeightPx));
            return new SectionMark(row.Label!, dot, lockIcon, condition, chevron);
        }

        public void Show(bool dot, long lockStage)
        {
            var locked = lockStage > 0;
            _dot.gameObject.SetActive(dot && !locked);
            _lock.gameObject.SetActive(locked);
            _condition.gameObject.SetActive(locked);
            if (locked)
                _condition.text = PanelTexts.LockedShort(lockStage);
            _label.alpha = locked ? LockedLabelAlpha : 1f;
            _chevron?.SetActive(!locked);
        }

        private static RectTransform LockIcon(RectTransform parent, Color color)
        {
            var icon = UiElements.Rect("Lock", parent);
            var shackle = UiElements.Ring("Shackle", icon, color);
            UiElements.Anchor(shackle.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, Vector2.one * ShacklePx);
            var body = UiElements.Fill("Body", icon, color, 2f);
            UiElements.Anchor(body.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(LockWidthPx, LockBodyHeightPx));
            return icon;
        }
    }
}
