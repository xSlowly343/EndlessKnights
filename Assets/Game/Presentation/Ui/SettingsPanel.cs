using System;
using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public sealed class SettingsPanel
    {
        public const float FullMinContentPx = 400f;

        private const float PaddingPx = 12f;
        private const float TopPx = 6f;
        private const float GapPx = 6f;
        private const float RowPx = 40f;
        private const float RowRadiusPx = 8f;
        private const float RowInsetPx = 12f;
        private const float ToggleWidthPx = 64f;
        private const float ControlHeightPx = 26f;
        private const float ArrowPx = 26f;
        private const float ValueWidthPx = 132f;
        private const float ActionWidthPx = 100f;
        private const float NotePx = 32f;

        private static readonly Color ToggleOff = new Color32(0x14, 0x1A, 0x2B, 0xFF);
        private static readonly Color ArrowFill = new Color32(0x2E, 0x3A, 0x58, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<Row> _rows = new List<Row>();
        private readonly TextMeshProUGUI _note;
        private IReadOnlyList<SettingId> _shown = Array.Empty<SettingId>();
        private GameSettings? _settings;
        private int _tab;
        private float _contentPx = PanelGeometry.FullHeightPx;

        private SettingsPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Settings", content));
            for (var i = 0; i < SettingsScreen.MaxRows; i++)
                _rows.Add(Row.Create(i, _root, theme));
            _note = UiElements.Label("Note", _root, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.TopLeft);
            _note.textWrappingMode = TextWrappingModes.Normal;
            _root.gameObject.SetActive(false);
        }

        public static SettingsPanel Create(RectTransform content, UiTheme theme) => new SettingsPanel(content, theme);

        public event Action<SettingId>? Changed;

        public bool IsShown => _root.gameObject.activeSelf;
        public int ShownRows => _shown.Count;

        public void Use(GameSettings settings)
        {
            _settings = settings;
            Refresh();
        }

        public void Show(bool shown, int tab, float contentPx)
        {
            _root.gameObject.SetActive(shown);
            _tab = tab;
            _contentPx = contentPx;
            if (shown)
                Refresh();
        }

        public void Refresh()
        {
            if (!IsShown)
                return;
            _shown = _settings == null ? Array.Empty<SettingId>() : SettingsScreen.Rows(_tab);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i < _shown.Count)
                    _rows[i].Show(_shown[i], _settings!, _theme);
                else
                    _rows[i].Hide();
            }
            _note.text = _tab == 1 ? Game.Core.Text.Texts.Get("settings.soundNote") : string.Empty;
            Arrange();
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || _settings == null)
                return false;
            for (var i = 0; i < _shown.Count; i++)
            {
                var step = _rows[i].StepAt(worldPoint);
                if (step == 0)
                    continue;
                var id = _shown[i];
                if (SettingsScreen.Change(id, _settings, step))
                    Changed?.Invoke(id);
                Refresh();
                return true;
            }
            return false;
        }

        public string ValueOf(int row) => _rows[row].Value;

        public Vector2 WorldPointOfControl(int row) => _rows[row].ControlPoint;

        public Vector2 WorldPointOfNext(int row) => _rows[row].NextPoint;

        public Vector2 WorldPointOfPrevious(int row) => _rows[row].PreviousPoint;

        private void Arrange()
        {
            var width = PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;
            var noteRoom = _note.text.Length > 0 ? NotePx + GapPx : 0f;
            var natural = _shown.Count * (RowPx + GapPx);
            var room = _contentPx - TopPx - noteRoom - GapPx;
            var scale = natural > 0f ? Mathf.Min(1f, room / natural) : 1f;
            var y = TopPx;
            for (var i = 0; i < _shown.Count; i++)
            {
                var height = RowPx * scale;
                _rows[i].Place(new Vector2(PaddingPx, -y), new Vector2(width, height));
                y += height + GapPx * scale;
            }
            UiElements.Anchor(_note.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -(y + GapPx)), new Vector2(width, NotePx));
        }

        private sealed class Row
        {
            private readonly UiButton _card;
            private readonly TextMeshProUGUI _label;
            private readonly UiButton _toggle;
            private readonly UiButton _previous;
            private readonly UiButton _next;
            private readonly TextMeshProUGUI _value;
            private readonly UiButton _action;
            private SettingKind _kind;

            private Row(UiButton card, TextMeshProUGUI label, UiButton toggle, UiButton previous, UiButton next, TextMeshProUGUI value, UiButton action)
            {
                _card = card;
                _label = label;
                _toggle = toggle;
                _previous = previous;
                _next = next;
                _value = value;
                _action = action;
            }

            public string Value => _kind switch
            {
                SettingKind.Toggle => _toggle.Label!.text,
                SettingKind.Action => _action.Label!.text,
                _ => _value.text
            };

            public Vector2 ControlPoint => Centre(_kind == SettingKind.Toggle ? _toggle.Rect : _kind == SettingKind.Action ? _action.Rect : _value.rectTransform);
            public Vector2 NextPoint => Centre(_next.Rect);
            public Vector2 PreviousPoint => Centre(_previous.Rect);

            public static Row Create(int index, RectTransform parent, UiTheme theme)
            {
                var back = UiElements.Fill("Row " + index, parent, theme.Card, RowRadiusPx);
                var card = new UiButton(back, null);
                var label = UiElements.Label("Label", back.rectTransform, theme.TextFont, 14f, theme.Text, TextAlignmentOptions.MidlineLeft);
                label.overflowMode = TextOverflowModes.Ellipsis;
                UiElements.Stretch(label.rectTransform).offsetMin = new Vector2(RowInsetPx, 0f);
                label.rectTransform.offsetMax = new Vector2(-(RowInsetPx + 2f * ArrowPx + ValueWidthPx), 0f);
                var right = new Vector2(1f, 0.5f);
                var toggle = Control("Toggle", back.rectTransform, right, new Vector2(-RowInsetPx, 0f), new Vector2(ToggleWidthPx, ControlHeightPx), theme.Success, theme, ControlHeightPx / 2f);
                var next = Control("Next", back.rectTransform, right, new Vector2(-RowInsetPx, 0f), Vector2.one * ArrowPx, ArrowFill, theme, 6f);
                next.Text = "›";
                var value = UiElements.Label("Value", back.rectTransform, theme.TextFont, 13f, theme.Text, TextAlignmentOptions.Center);
                value.overflowMode = TextOverflowModes.Ellipsis;
                UiElements.Anchor(value.rectTransform, right, new Vector2(-(RowInsetPx + ArrowPx), 0f), new Vector2(ValueWidthPx, ControlHeightPx));
                var previous = Control("Previous", back.rectTransform, right, new Vector2(-(RowInsetPx + ArrowPx + ValueWidthPx), 0f), Vector2.one * ArrowPx, ArrowFill, theme, 6f);
                previous.Text = "‹";
                var action = Control("Action", back.rectTransform, right, new Vector2(-RowInsetPx, 0f), new Vector2(ActionWidthPx, ControlHeightPx), theme.Accent, theme, 6f);
                back.gameObject.SetActive(false);
                return new Row(card, label, toggle, previous, next, value, action);
            }

            public void Show(SettingId id, GameSettings settings, UiTheme theme)
            {
                _card.Show(true);
                _kind = SettingsScreen.KindOf(id);
                _label.text = SettingsScreen.Label(id);
                _toggle.Show(_kind == SettingKind.Toggle);
                _previous.Show(_kind == SettingKind.Choice);
                _next.Show(_kind == SettingKind.Choice);
                _value.gameObject.SetActive(_kind == SettingKind.Choice);
                _action.Show(_kind == SettingKind.Action);
                var text = SettingsScreen.Value(id, settings);
                switch (_kind)
                {
                    case SettingKind.Toggle:
                        var on = SettingsScreen.IsOn(id, settings);
                        _toggle.Text = text;
                        _toggle.Back.color = on ? theme.Success : ToggleOff;
                        _toggle.Label!.color = on ? Color.white : theme.TextDim;
                        break;
                    case SettingKind.Action:
                        _action.Text = text;
                        break;
                    default:
                        _value.text = text;
                        break;
                }
            }

            public void Hide() => _card.Show(false);

            public void Place(Vector2 positionPx, Vector2 sizePx) => UiElements.Anchor(_card.Rect, new Vector2(0f, 1f), positionPx, sizePx);

            public int StepAt(Vector2 worldPoint)
            {
                switch (_kind)
                {
                    case SettingKind.Toggle:
                        return _card.Contains(worldPoint) ? 1 : 0;
                    case SettingKind.Action:
                        return _action.Contains(worldPoint) ? 1 : 0;
                    default:
                        if (_previous.Contains(worldPoint))
                            return -1;
                        return _next.Contains(worldPoint) || UiElements.Contains(_value.rectTransform, worldPoint) ? 1 : 0;
                }
            }

            private static UiButton Control(string name, RectTransform parent, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, UiTheme theme, float radiusPx)
            {
                var back = UiElements.Fill(name, parent, color, radiusPx);
                UiElements.Anchor(back.rectTransform, anchor, positionPx, sizePx);
                var label = UiElements.Label(name + "Text", back.rectTransform, theme.BoldFont, 13f, Color.white, TextAlignmentOptions.Center);
                UiElements.Stretch(label.rectTransform);
                return new UiButton(back, label);
            }

            private static Vector2 Centre(RectTransform rect) => rect.TransformPoint(rect.rect.center);
        }
    }
}
