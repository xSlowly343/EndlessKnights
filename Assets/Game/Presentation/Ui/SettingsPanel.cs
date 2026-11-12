using System;
using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class SettingsPanel
    {
        public const float FullMinContentPx = 400f;

        private const string IconGroup = "settings";
        private const float PaddingPx = 12f;
        private const float TopPx = 6f;
        private const float GapPx = 4f;
        private const float HeaderPx = 18f;
        private const float RowPx = 44f;
        private const float RowRadiusPx = 8f;
        private const float RowInsetPx = 12f;
        private const float DotPx = 6f;
        private const float IconPx = 20f;
        private const float ControlWidthPx = 196f;
        private const float ControlHeightPx = 26f;
        private const float ToggleWidthPx = 46f;
        private const float ToggleHeightPx = 22f;
        private const float KnobPx = 16f;
        private const float ArrowPx = 26f;
        private const float SegmentGapPx = 2f;
        private const float SegmentTextPx = 11.5f;
        private const float SliderNumberPx = 46f;
        private const float SamplePx = 14f;
        private const float TrackHeightPx = 6f;
        private const float ActionWidthPx = 100f;
        private const float NotePx = 44f;
        private const float AboutPx = 52f;
        private const float ResetWidthPx = 150f;
        private const int MaxSegments = 4;

        private static readonly Color ToggleOff = new Color32(0x14, 0x1A, 0x2B, 0xFF);
        private static readonly Color ArrowFill = new Color32(0x2E, 0x3A, 0x58, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<TextMeshProUGUI> _headers = new List<TextMeshProUGUI>();
        private readonly List<SettingId> _reset = new List<SettingId>();
        private readonly Image _about;
        private readonly TextMeshProUGUI _note;
        private readonly TextMeshProUGUI _version;
        private readonly UiButton _resetTab;
        private IReadOnlyList<SettingGroup> _groups = Array.Empty<SettingGroup>();
        private IReadOnlyList<SettingId> _shown = Array.Empty<SettingId>();
        private GameSettings? _settings;
        private int _tab;
        private float _contentPx = PanelGeometry.FullHeightPx;

        private SettingsPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Settings", content));
            _about = UiElements.Fill("About", _root, theme.Card, RowRadiusPx);
            _version = UiElements.Label("Version", _about.rectTransform, theme.BoldFont, 16f, theme.Gold, TextAlignmentOptions.MidlineLeft);
            UiElements.Stretch(_version.rectTransform).offsetMin = new Vector2(RowInsetPx, 0f);
            for (var i = 0; i < SettingsScreen.MaxGroups; i++)
                _headers.Add(UiElements.Label("Group " + i, _root, theme.BoldFont, 11f, theme.TextDim, TextAlignmentOptions.BottomLeft));
            for (var i = 0; i < SettingsScreen.MaxRows; i++)
                _rows.Add(Row.Create(i, _root, theme));
            _note = UiElements.Label("Note", _root, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.TopLeft);
            _note.textWrappingMode = TextWrappingModes.Normal;
            _resetTab = Row.Control("ResetTab", _root, Vector2.zero, new Vector2(PaddingPx, TopPx), new Vector2(ResetWidthPx, ControlHeightPx), ArrowFill, theme, 6f);
            _root.gameObject.SetActive(false);
        }

        public static SettingsPanel Create(RectTransform content, UiTheme theme) => new SettingsPanel(content, theme);

        public event Action<SettingId>? Changed;

        public bool IsShown => _root.gameObject.activeSelf;
        public int Tab => _tab;
        public int ShownRows => _shown.Count;
        public string VersionText => _version.text;
        public bool ResetShown => _resetTab.Rect.gameObject.activeSelf;

        public void UseVersion(string version)
        {
            _version.text = SettingsScreen.Version(version);
        }

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
            _groups = _settings == null ? Array.Empty<SettingGroup>() : SettingsScreen.Groups(_tab);
            _shown = _settings == null ? Array.Empty<SettingId>() : SettingsScreen.Rows(_tab);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i < _shown.Count)
                    _rows[i].Show(_shown[i], _settings!, _theme);
                else
                    _rows[i].Hide();
            }
            for (var i = 0; i < _headers.Count; i++)
            {
                _headers[i].gameObject.SetActive(i < _groups.Count);
                if (i < _groups.Count)
                    _headers[i].text = SettingsScreen.GroupTitle(_groups[i]).ToUpperInvariant();
            }
            _about.gameObject.SetActive(_tab == SettingsScreen.AboutTab);
            _note.text = SettingsScreen.Note(_tab);
            _resetTab.Show(_settings != null && SettingsScreen.AnyChanged(_tab, _settings));
            _resetTab.Text = SettingsScreen.ResetLabel;
            Arrange();
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown || _settings == null)
                return false;
            if (_resetTab.Contains(worldPoint))
            {
                _reset.Clear();
                SettingsScreen.ResetTab(_tab, _settings, _reset);
                foreach (var id in _reset)
                    Changed?.Invoke(id);
                Refresh();
                return true;
            }
            for (var i = 0; i < _shown.Count; i++)
            {
                var id = _shown[i];
                if (!_rows[i].TryClick(worldPoint, id, _settings, out var changed))
                    continue;
                if (changed)
                    Changed?.Invoke(id);
                Refresh();
                return true;
            }
            return false;
        }

        public void Press(SettingId id)
        {
            Changed?.Invoke(id);
            Refresh();
        }

        public string ValueOf(int row) => _rows[row].Value;

        public string HintOf(int row) => _rows[row].Hint;

        public bool ChangedMarkOf(int row) => _rows[row].Marked;

        public Vector2 WorldPointOfOption(int row, int option) => _rows[row].OptionPoint(option);

        public Vector2 WorldPointOfSlider(int row, float fraction) => _rows[row].SliderPoint(fraction);

        public Vector2 WorldPointOfReset => Row.Centre(_resetTab.Rect);

        public Vector2 WorldPointOfControl(int row) => _rows[row].ControlPoint;

        public Vector2 WorldPointOfNext(int row) => _rows[row].NextPoint;

        public Vector2 WorldPointOfPrevious(int row) => _rows[row].PreviousPoint;

        private void Arrange()
        {
            var width = PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;
            var aboutRoom = _tab == SettingsScreen.AboutTab ? AboutPx + GapPx : 0f;
            var noteRoom = _note.text.Length > 0 ? NotePx + GapPx : 0f;
            var natural = _shown.Count * (RowPx + GapPx) + _groups.Count * (HeaderPx + GapPx);
            var room = _contentPx - TopPx - aboutRoom - noteRoom - GapPx - ControlHeightPx - TopPx;
            var scale = natural > 0f ? Mathf.Min(1f, room / natural) : 1f;
            var y = TopPx;
            if (aboutRoom > 0f)
            {
                UiElements.Anchor(_about.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(width, AboutPx));
                y += aboutRoom;
            }
            var row = 0;
            for (var g = 0; g < _groups.Count; g++)
            {
                var headerPx = HeaderPx * scale;
                UiElements.Anchor(_headers[g].rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx + RowInsetPx, -y), new Vector2(width - RowInsetPx, headerPx));
                y += headerPx + GapPx * scale;
                for (var i = 0; i < _groups[g].Rows.Count; i++)
                {
                    var height = RowPx * scale;
                    _rows[row].Place(new Vector2(PaddingPx, -y), new Vector2(width, height));
                    y += height + GapPx * scale;
                    row++;
                }
            }
            UiElements.Anchor(_note.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -(y + GapPx)), new Vector2(width, NotePx));
        }

        private sealed class Row
        {
            private readonly UiButton _card;
            private readonly Image _dot;
            private readonly Image _icon;
            private readonly TextMeshProUGUI _label;
            private readonly TextMeshProUGUI _hint;
            private readonly Image _toggle;
            private readonly Image _knob;
            private readonly UiButton _previous;
            private readonly UiButton _next;
            private readonly TextMeshProUGUI _value;
            private readonly UiButton _action;
            private readonly RectTransform _segmentBar;
            private readonly List<UiButton> _segments = new List<UiButton>();
            private readonly TextMeshProUGUI _number;
            private readonly Image _track;
            private readonly Image _fill;
            private readonly Image _handle;
            private readonly Image _sample;
            private SettingKind _kind;
            private int _options;
            private string _text = string.Empty;

            private Row(int index, RectTransform parent, UiTheme theme)
            {
                var back = UiElements.Fill("Row " + index, parent, theme.Card, RowRadiusPx);
                var row = back.rectTransform;
                _card = new UiButton(back, null);
                var left = new Vector2(0f, 0.5f);
                var right = new Vector2(1f, 0.5f);
                _dot = UiElements.Dot("Changed", row, theme.Gold);
                UiElements.Anchor(_dot.rectTransform, left, new Vector2(3f, 0f), Vector2.one * DotPx);
                _icon = UiElements.Icon("Icon", row, null, theme.TextDim);
                UiElements.Anchor(_icon.rectTransform, left, new Vector2(RowInsetPx, 0f), Vector2.one * IconPx);
                _label = UiElements.Label("Label", row, theme.TextFont, 14f, theme.Text, TextAlignmentOptions.BottomLeft);
                _label.overflowMode = TextOverflowModes.Ellipsis;
                _hint = UiElements.Label("Hint", row, theme.TextFont, 10.5f, theme.TextDim, TextAlignmentOptions.TopLeft);
                _hint.overflowMode = TextOverflowModes.Ellipsis;
                _toggle = UiElements.Raise(UiElements.Fill("Toggle", row, theme.Success, ToggleHeightPx / 2f));
                UiElements.Anchor(_toggle.rectTransform, right, new Vector2(-RowInsetPx, 0f), new Vector2(ToggleWidthPx, ToggleHeightPx));
                _knob = UiElements.Dot("Knob", _toggle.rectTransform, Color.white);
                _next = Control("Next", row, right, new Vector2(-RowInsetPx, 0f), Vector2.one * ArrowPx, ArrowFill, theme, 6f);
                _next.Text = "›";
                _value = UiElements.Label("Value", row, theme.TextFont, 13f, theme.Text, TextAlignmentOptions.Center);
                _value.overflowMode = TextOverflowModes.Ellipsis;
                UiElements.Anchor(_value.rectTransform, right, new Vector2(-(RowInsetPx + ArrowPx), 0f), new Vector2(ControlWidthPx - 2f * ArrowPx, ControlHeightPx));
                _previous = Control("Previous", row, right, new Vector2(-(RowInsetPx + ControlWidthPx - ArrowPx), 0f), Vector2.one * ArrowPx, ArrowFill, theme, 6f);
                _previous.Text = "‹";
                _action = Control("Action", row, right, new Vector2(-RowInsetPx, 0f), new Vector2(ActionWidthPx, ControlHeightPx), theme.Accent, theme, 6f);
                _segmentBar = UiElements.Rect("Segments", row);
                UiElements.Anchor(_segmentBar, right, new Vector2(-RowInsetPx, 0f), new Vector2(ControlWidthPx, ControlHeightPx));
                for (var i = 0; i < MaxSegments; i++)
                {
                    var segment = Control("Segment " + i, _segmentBar, left, Vector2.zero, Vector2.one * ControlHeightPx, ToggleOff, theme, 6f);
                    segment.Label!.fontSize = SegmentTextPx;
                    _segments.Add(segment);
                }
                _number = UiElements.Label("Number", row, theme.NumberFont != null ? theme.NumberFont : theme.TextFont, 13f, theme.Text, TextAlignmentOptions.MidlineRight);
                UiElements.Anchor(_number.rectTransform, right, new Vector2(-RowInsetPx, 0f), new Vector2(SliderNumberPx, ControlHeightPx));
                _track = UiElements.Fill("Track", row, ToggleOff, TrackHeightPx / 2f);
                UiElements.Anchor(_track.rectTransform, right, new Vector2(-(RowInsetPx + SliderNumberPx), 0f), new Vector2(ControlWidthPx - SliderNumberPx - SamplePx - 8f, TrackHeightPx));
                _fill = UiElements.Fill("Fill", _track.rectTransform, theme.Accent, TrackHeightPx / 2f);
                _handle = UiElements.Dot("Handle", _track.rectTransform, Color.white);
                _sample = UiElements.Dot("Sample", row, theme.Gold);
                UiElements.Anchor(_sample.rectTransform, right, new Vector2(-(RowInsetPx + ControlWidthPx - SamplePx), 0f), Vector2.one * SamplePx);
                back.gameObject.SetActive(false);
            }

            public string Value => _text;
            public string Hint => _hint.text;
            public bool Marked => _dot.gameObject.activeSelf;

            public Vector2 ControlPoint => Centre(_kind switch
            {
                SettingKind.Toggle => _toggle.rectTransform,
                SettingKind.Action => _action.Rect,
                SettingKind.Segments => _segmentBar,
                SettingKind.Slider => _track.rectTransform,
                _ => _value.rectTransform
            });

            public Vector2 NextPoint => Centre(_next.Rect);
            public Vector2 PreviousPoint => Centre(_previous.Rect);

            public Vector2 OptionPoint(int option) => Centre(_segments[option].Rect);

            public Vector2 SliderPoint(float fraction)
            {
                var rect = _track.rectTransform.rect;
                return _track.rectTransform.TransformPoint(new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, fraction), rect.center.y));
            }

            public static Row Create(int index, RectTransform parent, UiTheme theme) => new Row(index, parent, theme);

            public void Show(SettingId id, GameSettings settings, UiTheme theme)
            {
                _card.Show(true);
                _kind = SettingsScreen.KindOf(id);
                _label.text = SettingsScreen.Label(id);
                _hint.text = SettingsScreen.HintLine(id, settings);
                _dot.gameObject.SetActive(!SettingsScreen.IsDefault(id, settings));
                UiElements.Use(_icon, UiIcons.Get(IconGroup, SettingsScreen.IconKey(id)));
                PlaceTexts();
                var alpha = (float)SettingsScreen.PreviewAlpha(id, settings);
                _toggle.gameObject.SetActive(_kind == SettingKind.Toggle);
                _previous.Show(_kind == SettingKind.Choice);
                _next.Show(_kind == SettingKind.Choice);
                _value.gameObject.SetActive(_kind == SettingKind.Choice);
                _action.Show(_kind == SettingKind.Action);
                _segmentBar.gameObject.SetActive(_kind == SettingKind.Segments);
                _track.gameObject.SetActive(_kind == SettingKind.Slider);
                _number.gameObject.SetActive(_kind == SettingKind.Slider);
                _sample.gameObject.SetActive(_kind == SettingKind.Slider && alpha >= 0f);
                _text = SettingsScreen.Value(id, settings);
                switch (_kind)
                {
                    case SettingKind.Toggle:
                        ShowToggle(SettingsScreen.IsOn(id, settings), theme);
                        break;
                    case SettingKind.Action:
                        _action.Text = _text;
                        break;
                    case SettingKind.Segments:
                        ShowSegments(id, settings, theme);
                        break;
                    case SettingKind.Slider:
                        ShowSlider((float)SettingsScreen.Fraction(id, settings), alpha, theme);
                        break;
                    default:
                        _value.text = _text;
                        break;
                }
            }

            public void Hide() => _card.Show(false);

            public void Place(Vector2 positionPx, Vector2 sizePx) => UiElements.Anchor(_card.Rect, new Vector2(0f, 1f), positionPx, sizePx);

            public bool TryClick(Vector2 worldPoint, SettingId id, GameSettings settings, out bool changed)
            {
                changed = false;
                switch (_kind)
                {
                    case SettingKind.Toggle:
                        if (!_card.Contains(worldPoint))
                            return false;
                        changed = SettingsScreen.Change(id, settings, 1);
                        return true;
                    case SettingKind.Action:
                        if (!_action.Contains(worldPoint))
                            return false;
                        changed = SettingsScreen.Change(id, settings, 1);
                        return true;
                    case SettingKind.Segments:
                        for (var i = 0; i < _options; i++)
                        {
                            if (!_segments[i].Contains(worldPoint))
                                continue;
                            changed = SettingsScreen.Select(id, settings, i);
                            return true;
                        }
                        return false;
                    case SettingKind.Slider:
                        return TryClickSlider(worldPoint, id, settings, out changed);
                    default:
                        var step = _previous.Contains(worldPoint) ? -1 : _next.Contains(worldPoint) || UiElements.Contains(_value.rectTransform, worldPoint) ? 1 : 0;
                        if (step == 0)
                            return false;
                        changed = SettingsScreen.Change(id, settings, step);
                        return true;
                }
            }

            private bool TryClickSlider(Vector2 worldPoint, SettingId id, GameSettings settings, out bool changed)
            {
                changed = false;
                var track = _track.rectTransform;
                if (!_card.Contains(worldPoint))
                    return false;
                var local = (Vector2)track.InverseTransformPoint(worldPoint);
                var rect = track.rect;
                if (local.x < rect.xMin - KnobPx || local.x > rect.xMax + KnobPx)
                    return false;
                changed = SettingsScreen.SetFraction(id, settings, Mathf.InverseLerp(rect.xMin, rect.xMax, local.x));
                return true;
            }

            private void PlaceTexts()
            {
                var leftPx = _icon.enabled ? RowInsetPx + IconPx + 8f : RowInsetPx;
                var rightPx = -(RowInsetPx + ControlWidthPx + 8f);
                var label = _label.rectTransform;
                label.anchorMin = new Vector2(0f, 0.5f);
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(leftPx, 0f);
                label.offsetMax = new Vector2(rightPx, -4f);
                var hint = _hint.rectTransform;
                hint.anchorMin = Vector2.zero;
                hint.anchorMax = new Vector2(1f, 0.5f);
                hint.offsetMin = new Vector2(leftPx, 2f);
                hint.offsetMax = new Vector2(rightPx, -1f);
            }

            private void ShowToggle(bool on, UiTheme theme)
            {
                _toggle.color = on ? theme.Success : ToggleOff;
                _knob.color = on ? Color.white : theme.TextDim;
                var inset = (ToggleHeightPx - KnobPx) / 2f;
                UiElements.Anchor(_knob.rectTransform, new Vector2(on ? 1f : 0f, 0.5f), new Vector2(on ? -inset : inset, 0f), Vector2.one * KnobPx);
            }

            private void ShowSegments(SettingId id, GameSettings settings, UiTheme theme)
            {
                _options = Mathf.Min(SettingsScreen.OptionCount(id), _segments.Count);
                var selected = SettingsScreen.Selected(id, settings);
                var width = (ControlWidthPx - SegmentGapPx * (_options - 1)) / Mathf.Max(1, _options);
                for (var i = 0; i < _segments.Count; i++)
                {
                    var segment = _segments[i];
                    segment.Show(i < _options);
                    if (i >= _options)
                        continue;
                    UiElements.Anchor(segment.Rect, new Vector2(0f, 0.5f), new Vector2(i * (width + SegmentGapPx), 0f), new Vector2(width, ControlHeightPx));
                    segment.Text = SettingsScreen.OptionLabel(id, i);
                    segment.Back.color = i == selected ? theme.Accent : ToggleOff;
                    segment.Label!.color = i == selected ? Color.white : theme.TextDim;
                }
            }

            private void ShowSlider(float fraction, float alpha, UiTheme theme)
            {
                _number.text = _text;
                var fill = _fill.rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(fraction, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                var handle = _handle.rectTransform;
                handle.anchorMin = new Vector2(fraction, 0.5f);
                handle.anchorMax = handle.anchorMin;
                handle.pivot = new Vector2(0.5f, 0.5f);
                handle.anchoredPosition = Vector2.zero;
                handle.sizeDelta = Vector2.one * KnobPx;
                if (alpha < 0f)
                    return;
                var color = theme.Gold;
                color.a = alpha;
                _sample.color = color;
            }

            public static UiButton Control(string name, RectTransform parent, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, UiTheme theme, float radiusPx)
            {
                var back = UiElements.Raise(UiElements.Fill(name, parent, color, radiusPx));
                UiElements.Anchor(back.rectTransform, anchor, positionPx, sizePx);
                var label = UiElements.Label(name + "Text", back.rectTransform, theme.BoldFont, 13f, Color.white, TextAlignmentOptions.Center);
                UiElements.Stretch(label.rectTransform);
                return new UiButton(back, label);
            }

            public static Vector2 Centre(RectTransform rect) => rect.TransformPoint(rect.rect.center);
        }
    }
}
