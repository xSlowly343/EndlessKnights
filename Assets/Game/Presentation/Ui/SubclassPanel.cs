using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class SubclassPanel
    {
        public const int MaxChoices = 3;

        private const float InsetPx = 10f;
        private const float HeaderHeightPx = 52f;
        private const float HintHeightPx = 120f;
        private const float RadiusPx = 10f;
        private const float ChoiceHeightPx = 84f;
        private const float ChoiceButtonWidthPx = 92f;
        private const float ChoiceButtonHeightPx = 30f;

        private static readonly Color FieldColor = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color PlateColor = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color DimText = new Color32(0xAE, 0xB9, 0xD6, 0xFF);
        private static readonly Color PathColor = new Color32(0xE0, 0x57, 0x4A, 0xFF);
        private static readonly Color SkillColor = new Color32(0xA9, 0xC4, 0xFF, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly RectTransform _field;
        private readonly RectTransform _header;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _points;
        private readonly RectTransform _chooser;
        private readonly TextMeshProUGUI _chooserTitle;
        private readonly List<ChoiceRow> _choices = new List<ChoiceRow>();
        private readonly RectTransform _hint;
        private readonly TextMeshProUGUI _hintText;
        private readonly List<SubclassDef> _offered = new List<SubclassDef>();
        private GameLoop? _loop;
        private Vector2 _fieldPx = new Vector2(PanelGeometry.WidthPx, PanelGeometry.FullHeightPx);

        private SubclassPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Subclass", content));
            _field = UiElements.Fill("Field", _root, FieldColor).rectTransform;
            UiElements.TopEdge(_field, _fieldPx.y);
            _header = UiElements.Fill("Header", _field, PlateColor, RadiusPx).rectTransform;
            UiElements.Anchor(_header, new Vector2(0f, 1f), new Vector2(InsetPx, -InsetPx), new Vector2(_fieldPx.x - 2f * InsetPx - 0f, HeaderHeightPx));
            _name = Label("Name", _header, theme.BoldFont, 16f, PathColor, TextAlignmentOptions.MidlineLeft);
            Line(_name.rectTransform, 6f, 22f);
            _points = Label("Points", _header, theme.TextFont, 11f, DimText, TextAlignmentOptions.MidlineLeft);
            Line(_points.rectTransform, 28f, 16f);
            _chooser = UiElements.Stretch(UiElements.Rect("Chooser", _field), InsetPx);
            _chooserTitle = Label("ChooserTitle", _chooser, theme.BoldFont, 16f, theme.Gold, TextAlignmentOptions.MidlineLeft);
            _chooserTitle.textWrappingMode = TextWrappingModes.Normal;
            for (var i = 0; i < MaxChoices; i++)
                _choices.Add(BuildChoice(i));
            _hint = BuildHint(out _hintText);
            _root.gameObject.SetActive(false);
        }

        public static SubclassPanel Create(RectTransform content, UiTheme theme) => new SubclassPanel(content, theme);

        public bool IsShown => _root.gameObject.activeSelf;
        public bool IsChoosing => _chooser.gameObject.activeSelf;
        public string HintText => _hintText.text;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            Refresh();
        }

        public void Show(bool shown, float widthPx, float contentPx)
        {
            _root.gameObject.SetActive(shown);
            _fieldPx = new Vector2(Mathf.Max(1f, widthPx - 2f * PanelCanvas.BorderPx), Mathf.Max(1f, contentPx - 2f * PanelCanvas.BorderPx));
            UiElements.TopEdge(_field, _fieldPx.y);
            _header.sizeDelta = new Vector2(_fieldPx.x - 2f * InsetPx, HeaderHeightPx);
            if (shown)
                Refresh();
        }

        public Vector2 WorldPointOfChoice(int index) => _choices[index].Button.Rect.TransformPoint(_choices[index].Button.Rect.rect.center);

        public bool TryPress(Vector2 worldPoint)
        {
            if (!IsShown || _loop == null)
                return false;
            if (IsChoosing)
                return TryChoose(worldPoint);
            return UiElements.Contains(_field, worldPoint);
        }

        public void Refresh()
        {
            if (_loop == null)
                return;
            var catalog = _loop.Data.Ascendancy;
            var state = _loop.State.Ascendancy;
            var path = state.Subclass != null && catalog.TryGet(state.Subclass, out var chosen) ? chosen : null;
            _chooser.gameObject.SetActive(path == null);
            _header.gameObject.SetActive(path != null);
            _hint.gameObject.SetActive(path != null);
            if (path == null)
            {
                ShowChooser();
                return;
            }
            _name.text = SubclassView.Name(path);
            var skill = SubclassView.PathSkill(_loop.Data.Gems, path);
            _points.text = SubclassView.Level(state, catalog) + (skill == null ? string.Empty : " · " + skill);
            _hintText.text = SubclassView.Blurb(path) + "\n" + Texts.Get("subclass.hint");
        }

        private bool TryChoose(Vector2 worldPoint)
        {
            for (var i = 0; i < _offered.Count && i < _choices.Count; i++)
            {
                if (!_choices[i].Button.Back.gameObject.activeSelf || !_choices[i].Button.Contains(worldPoint))
                    continue;
                _loop!.ChooseSubclass(_offered[i].Id);
                Refresh();
                return true;
            }
            return UiElements.Contains(_field, worldPoint);
        }

        private void ShowChooser()
        {
            var catalog = _loop!.Data.Ascendancy;
            var unlocked = _loop.State.Ascendancy.Unlocked(catalog);
            _offered.Clear();
            _offered.AddRange(catalog.SubclassesOf(_loop.State.ClassName));
            _chooserTitle.text = unlocked ? Texts.Get("subclass.pick") : SubclassView.Locked(catalog);
            var titleHeight = unlocked ? 24f : 40f;
            UiElements.TopEdge(_chooserTitle.rectTransform, titleHeight);
            for (var i = 0; i < _choices.Count; i++)
            {
                var row = _choices[i];
                var shown = i < _offered.Count;
                row.Back.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var path = _offered[i];
                row.Name.text = SubclassView.Name(path);
                row.Blurb.text = SubclassView.Blurb(path);
                row.Skill.text = SubclassView.PathSkill(_loop.Data.Gems, path) ?? string.Empty;
                row.Button.Show(unlocked);
                UiElements.TopEdge(row.Back.rectTransform, ChoiceHeightPx);
                row.Back.rectTransform.anchoredPosition = new Vector2(0f, -(titleHeight + InsetPx + i * (ChoiceHeightPx + InsetPx)));
            }
        }

        private ChoiceRow BuildChoice(int index)
        {
            var back = UiElements.Fill("Choice " + index, _chooser, PlateColor, RadiusPx);
            var name = Label("Name", back.rectTransform, _theme.BoldFont, 15f, PathColor, TextAlignmentOptions.MidlineLeft);
            Line(name.rectTransform, 8f, 20f, ChoiceButtonWidthPx + 2f * InsetPx);
            var blurb = Label("Blurb", back.rectTransform, _theme.TextFont, 12f, DimText, TextAlignmentOptions.TopLeft);
            blurb.textWrappingMode = TextWrappingModes.Normal;
            Line(blurb.rectTransform, 30f, 32f, ChoiceButtonWidthPx + 2f * InsetPx);
            var skill = Label("Skill", back.rectTransform, _theme.TextFont, 11f, SkillColor, TextAlignmentOptions.MidlineLeft);
            Line(skill.rectTransform, 62f, 16f, ChoiceButtonWidthPx + 2f * InsetPx);
            var button = UiElements.Raise(UiElements.Fill("Choose", back.rectTransform, _theme.Gold, 6f));
            UiElements.Anchor(button.rectTransform, new Vector2(1f, 0.5f), new Vector2(-InsetPx, 0f), new Vector2(ChoiceButtonWidthPx, ChoiceButtonHeightPx));
            var label = UiElements.Label("ChooseText", button.rectTransform, _theme.BoldFont, 13f, _theme.TextOnGold, TextAlignmentOptions.Center);
            label.text = Texts.Get("subclass.choose");
            UiElements.Stretch(label.rectTransform);
            return new ChoiceRow(back, name, blurb, skill, new UiButton(button, label));
        }

        private RectTransform BuildHint(out TextMeshProUGUI text)
        {
            var back = UiElements.Fill("Hint", _field, PlateColor, RadiusPx);
            var rect = UiElements.BottomEdge(back.rectTransform, HintHeightPx, InsetPx);
            rect.anchoredPosition = new Vector2(0f, InsetPx);
            text = Label("HintText", rect, _theme.TextFont, 12f, DimText, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            UiElements.Stretch(text.rectTransform, 8f);
            return rect;
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_FontAsset? font, float sizePx, Color color, TextAlignmentOptions alignment)
        {
            var label = UiElements.Label(name, parent, font, sizePx, color, alignment);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static void Line(RectTransform rect, float topPx, float heightPx, float rightPx = 12f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, -topPx - heightPx);
            rect.offsetMax = new Vector2(-rightPx, -topPx);
        }

        private readonly struct ChoiceRow
        {
            public readonly Image Back;
            public readonly TextMeshProUGUI Name;
            public readonly TextMeshProUGUI Blurb;
            public readonly TextMeshProUGUI Skill;
            public readonly UiButton Button;

            public ChoiceRow(Image back, TextMeshProUGUI name, TextMeshProUGUI blurb, TextMeshProUGUI skill, UiButton button)
            {
                Back = back;
                Name = name;
                Blurb = blurb;
                Skill = skill;
                Button = button;
            }
        }
    }
}
