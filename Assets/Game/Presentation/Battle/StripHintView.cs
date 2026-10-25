using System;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Battle
{
    public sealed class StripHintView : MonoBehaviour
    {
        public const float CentreXPx = 240f;
        public const float CentreYPx = 52f;

        private const int SortingOrder = 28;
        private const float HeightPx = 24f;
        private const float MaxWidthPx = 440f;
        private const float PaddingPx = 12f;
        private const float CloseWidthPx = 22f;
        private const float TextSizePx = 13f;
        private const float CloseSizePx = 15f;

        private static readonly Color Back = new Color32(0x12, 0x18, 0x28, 0xE6);
        private static readonly Color ConsentBack = new Color32(0x5A, 0x1F, 0x28, 0xE6);

        private RectTransform _pill = null!;
        private Image _back = null!;
        private BoxCollider2D _hit = null!;
        private TextMeshProUGUI _text = null!;
        private TextMeshProUGUI _close = null!;
        private StripHint _shown = StripHint.None;

        public event Action? CloseClicked;
        public event Action? HintClicked;

        public bool IsShown => _pill.gameObject.activeSelf;
        public string Text => _text.text;
        public bool IsCloseShown => _close.gameObject.activeSelf;
        public Vector2 WorldPointOfText => _text.rectTransform.TransformPoint(_text.rectTransform.rect.center);
        public Vector2 WorldPointOfClose => _close.rectTransform.TransformPoint(_close.rectTransform.rect.center);

        public static StripHintView Create(Transform? parent = null)
        {
            var canvas = StripCanvas.Create("StripHint", SortingOrder, parent);
            var view = canvas.gameObject.AddComponent<StripHintView>();
            view.Build(canvas);
            return view;
        }

        public void Show(StripHint hint)
        {
            if (hint.Kind == _shown.Kind && hint.Text == _shown.Text)
                return;
            _shown = hint;
            _pill.gameObject.SetActive(hint.IsShown);
            if (!hint.IsShown)
                return;
            _text.text = hint.Text;
            _close.gameObject.SetActive(hint.CanClose);
            _back.color = hint.Kind == StripHintKind.Consent ? ConsentBack : Back;
            var closeRoom = hint.CanClose ? CloseWidthPx : 0f;
            var textWidth = Mathf.Min(_text.GetPreferredValues(hint.Text).x, MaxWidthPx - 2f * PaddingPx - closeRoom);
            var width = textWidth + 2f * PaddingPx + closeRoom;
            _pill.sizeDelta = new Vector2(width, HeightPx);
            _hit.size = _pill.sizeDelta;
            _text.rectTransform.offsetMin = new Vector2(PaddingPx, 0f);
            _text.rectTransform.offsetMax = new Vector2(-(PaddingPx + closeRoom), 0f);
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || !UiElements.Contains(_pill, worldPoint))
                return false;
            if (IsCloseShown && UiElements.Contains(_close.rectTransform, worldPoint))
                CloseClicked?.Invoke();
            else
                HintClicked?.Invoke();
            return true;
        }

        private void Build(RectTransform canvas)
        {
            var theme = UiTheme.Load();
            _back = UiElements.Fill("Pill", canvas, Back, HeightPx / 2f);
            _pill = StripCanvas.Place(_back.rectTransform, CentreXPx, CentreYPx, new Vector2(MaxWidthPx, HeightPx), new Vector2(0.5f, 0.5f));
            var outline = _back.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.Gold;
            outline.effectDistance = new Vector2(1f, -1f);
            _hit = _pill.gameObject.AddComponent<BoxCollider2D>();
            _pill.gameObject.AddComponent<StripClickable>();
            _text = UiElements.Label("Text", _pill, theme.TextFont, TextSizePx, theme.Text, TextAlignmentOptions.Center);
            _text.overflowMode = TextOverflowModes.Ellipsis;
            UiElements.Stretch(_text.rectTransform);
            _close = UiElements.Label("Close", _pill, theme.BoldFont, CloseSizePx, theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Anchor(_close.rectTransform, new Vector2(1f, 0.5f), new Vector2(-PaddingPx / 2f, 0f), new Vector2(CloseWidthPx, HeightPx));
            _close.text = "×";
            _pill.gameObject.SetActive(false);
        }
    }
}
