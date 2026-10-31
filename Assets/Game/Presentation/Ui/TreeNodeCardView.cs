using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public enum TreeNodeCardMode
    {
        Tooltip,
        Pinned
    }

    public sealed class TreeNodeCardView
    {
        public const float TooltipWidthPx = 280f;
        public const int MaxStatLines = 6;
        public const int MaxActions = 4;

        private const float RadiusPx = 6f;
        private const float BorderPx = 1f;
        private const float PaddingPx = 12f;
        private const float GapPx = 6f;
        private const float TitlePx = 20f;
        private const float KindPx = 14f;
        private const float StatPx = 17f;
        private const float DescriptionPx = 16f;
        private const float DeltaPx = 16f;
        private const float FooterPx = 16f;
        private const float ActionHeightPx = 26f;
        private const float ActionWidthPx = 92f;
        private const float CloseSizePx = 24f;

        private static readonly Color BodyColor = new Color32(0x0A, 0x0D, 0x16, 0xF7);
        private static readonly Color KeystoneFrame = new Color32(0xE0, 0xA8, 0x4A, 0xFF);
        private static readonly Color NotableFrame = new Color32(0xC9, 0x97, 0x3A, 0xFF);
        private static readonly Color SmallFrame = new Color32(0x4A, 0x56, 0x77, 0xFF);
        private static readonly Color KeystoneHead = new Color32(0x2E, 0x1A, 0x14, 0xFF);
        private static readonly Color NotableHead = new Color32(0x2A, 0x22, 0x14, 0xFF);
        private static readonly Color SmallHead = new Color32(0x1E, 0x26, 0x38, 0xFF);
        private static readonly Color GoldTitle = new Color32(0xF7, 0xD5, 0x8A, 0xFF);
        private static readonly Color PlainTitle = new Color32(0xEE, 0xF2, 0xFF, 0xFF);
        private static readonly Color KindColor = new Color32(0xC9, 0xB4, 0x8A, 0xFF);
        private static readonly Color StatColor = new Color32(0xA9, 0xC4, 0xFF, 0xFF);
        private static readonly Color DimColor = new Color32(0xAE, 0xB9, 0xD6, 0xFF);
        private static readonly Color HintColor = new Color32(0x8C, 0x97, 0xB6, 0xFF);
        private static readonly Color TakenStatus = new Color32(0x8C, 0xE0, 0xA8, 0xFF);
        private static readonly Color OfferedStatus = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color LineColor = new Color32(0x3A, 0x32, 0x20, 0xFF);
        private static readonly Color GainColor = new Color32(0x8C, 0xE0, 0xA8, 0xFF);

        private readonly TreeNodeCardMode _mode;
        private readonly UiTheme _theme;
        private readonly UnityEngine.UI.Image _frame;
        private readonly UnityEngine.UI.Image _head;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _kind;
        private readonly List<TextMeshProUGUI> _stats = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _description;
        private readonly List<TextMeshProUGUI> _deltaNames = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _deltaValues = new List<TextMeshProUGUI>();
        private readonly UnityEngine.UI.Image _topLine;
        private readonly UnityEngine.UI.Image _bottomLine;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _hint;
        private readonly List<UiButton> _actions = new List<UiButton>();

        public TreeNodeCardView(string name, Transform parent, UiTheme theme, TreeNodeCardMode mode)
        {
            _mode = mode;
            _theme = theme;
            _frame = UiElements.Fill(name, parent, NotableFrame, RadiusPx);
            Root = _frame.rectTransform;
            UiElements.Stretch(UiElements.Fill("Body", Root, BodyColor, RadiusPx - BorderPx).rectTransform, BorderPx);
            _head = UiElements.Fill("Head", Root, NotableHead, RadiusPx - BorderPx);
            var centred = mode == TreeNodeCardMode.Tooltip;
            var align = centred ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            _title = Label("Title", theme.BoldFont, 16f, GoldTitle, align);
            _kind = Label("Kind", theme.TextFont, 10f, KindColor, align);
            _kind.fontStyle = FontStyles.UpperCase;
            for (var i = 0; i < MaxStatLines; i++)
                _stats.Add(Label("Stat " + i, theme.TextFont, 13f, StatColor, align));
            _description = Label("Description", theme.TextFont, 12f, KindColor, align);
            _description.fontStyle = FontStyles.Italic;
            for (var i = 0; i < TreeNodeCard.MaxDeltas; i++)
            {
                _deltaNames.Add(Label("Delta " + i, theme.TextFont, 12f, DimColor, TextAlignmentOptions.MidlineLeft));
                _deltaValues.Add(Label("Delta " + i + " Value", theme.TextFont, 12f, DimColor, TextAlignmentOptions.MidlineRight));
            }
            _topLine = UiElements.Fill("Line Top", Root, LineColor);
            _bottomLine = UiElements.Fill("Line Bottom", Root, LineColor);
            _status = Label("Status", theme.TextFont, 11f, HintColor, TextAlignmentOptions.MidlineLeft);
            _hint = Label("Hint", theme.TextFont, 11f, HintColor, TextAlignmentOptions.MidlineRight);
            if (mode == TreeNodeCardMode.Pinned)
                BuildButtons(theme);
            Root.gameObject.SetActive(false);
        }

        public RectTransform Root { get; }
        public UiButton? Close { get; private set; }
        public IReadOnlyList<UiButton> Actions => _actions;
        public float HeightPx { get; private set; }
        public bool IsShown => Root.gameObject.activeSelf;

        public void Hide() => Root.gameObject.SetActive(false);

        public float Show(TreeNodeCard card, TreeNodeType type, TreeNodeLook look, float widthPx, IReadOnlyList<TreeAction> actions, string hint)
        {
            Root.gameObject.SetActive(true);
            _frame.color = FrameOf(type);
            _head.color = HeadOf(type);
            _title.text = card.Name;
            _title.color = type == TreeNodeType.Small ? PlainTitle : GoldTitle;
            _kind.text = card.Kind;
            _status.text = card.Status;
            _status.color = look == TreeNodeLook.Taken ? TakenStatus : look == TreeNodeLook.Offered ? OfferedStatus : HintColor;
            _hint.text = hint;

            var y = BorderPx;
            var headHeight = _mode == TreeNodeCardMode.Tooltip ? TitlePx + KindPx + GapPx * 1.5f : TitlePx + GapPx;
            Place(_head.rectTransform, BorderPx, ref y, headHeight, BorderPx);
            y = BorderPx + GapPx / 2f;
            if (_mode == TreeNodeCardMode.Tooltip)
            {
                Place(_title.rectTransform, PaddingPx, ref y, TitlePx, PaddingPx);
                Place(_kind.rectTransform, PaddingPx, ref y, KindPx, PaddingPx);
            }
            else
            {
                PlaceRow(_title, _kind, y, TitlePx);
                y += TitlePx;
            }
            y = BorderPx + headHeight + GapPx;

            for (var i = 0; i < _stats.Count; i++)
            {
                var shown = i < card.Stats.Count;
                _stats[i].gameObject.SetActive(shown);
                if (shown)
                {
                    _stats[i].text = card.Stats[i];
                    Place(_stats[i].rectTransform, PaddingPx, ref y, StatPx, PaddingPx);
                }
            }

            var showDescription = _mode == TreeNodeCardMode.Pinned && card.Description.Length > 0;
            _description.gameObject.SetActive(showDescription);
            if (showDescription)
            {
                _description.text = card.Description;
                y += GapPx / 2f;
                Place(_description.rectTransform, PaddingPx, ref y, DescriptionPx, PaddingPx);
            }

            var deltaCount = _mode == TreeNodeCardMode.Tooltip ? card.Deltas.Count : 0;
            _topLine.gameObject.SetActive(deltaCount > 0);
            if (deltaCount > 0)
            {
                y += GapPx;
                Place(_topLine.rectTransform, PaddingPx, ref y, 1f, PaddingPx);
                y += GapPx;
            }
            for (var i = 0; i < _deltaNames.Count; i++)
            {
                var shown = i < deltaCount;
                _deltaNames[i].gameObject.SetActive(shown);
                _deltaValues[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var delta = card.Deltas[i];
                _deltaNames[i].text = delta.What;
                _deltaValues[i].text = DeltaText(delta);
                PlaceRow(_deltaNames[i], _deltaValues[i], y, DeltaPx);
                y += DeltaPx;
            }

            y += GapPx;
            Place(_bottomLine.rectTransform, PaddingPx, ref y, 1f, PaddingPx);
            y += GapPx;
            var rowHeight = Mathf.Max(FooterPx, ShowActions(actions) > 0 ? ActionHeightPx : 0f);
            PlaceRow(_status, _hint, y, rowHeight);
            PlaceActions(y, rowHeight);
            y += rowHeight + GapPx;

            HeightPx = y + BorderPx;
            Root.sizeDelta = new Vector2(widthPx, HeightPx);
            return HeightPx;
        }

        private int ShowActions(IReadOnlyList<TreeAction> actions)
        {
            for (var i = 0; i < _actions.Count; i++)
            {
                var shown = i < actions.Count;
                _actions[i].Show(shown);
                if (!shown)
                    continue;
                var primary = actions[i].Kind != TreeActionKind.Refund;
                _actions[i].Text = actions[i].Label;
                _actions[i].Back.color = primary ? _theme.Gold : _theme.Card;
                _actions[i].Label!.color = primary ? _theme.TextOnGold : _theme.Text;
            }
            return Mathf.Min(actions.Count, _actions.Count);
        }

        private void PlaceActions(float y, float rowHeight)
        {
            var shown = 0;
            foreach (var action in _actions)
                if (action.Back.gameObject.activeSelf)
                    shown++;
            var slot = 0;
            foreach (var action in _actions)
            {
                if (!action.Back.gameObject.activeSelf)
                    continue;
                var x = -(PaddingPx + (shown - 1 - slot) * (ActionWidthPx + GapPx));
                UiElements.Anchor(action.Rect, new Vector2(1f, 1f), new Vector2(x, -(y + (rowHeight - ActionHeightPx) / 2f)), new Vector2(ActionWidthPx, ActionHeightPx));
                slot++;
            }
            _hint.gameObject.SetActive(shown == 0 && _hint.text.Length > 0);
            var right = shown == 0 ? PaddingPx : PaddingPx + shown * (ActionWidthPx + GapPx);
            _status.rectTransform.offsetMax = new Vector2(-right, _status.rectTransform.offsetMax.y);
        }

        private static string DeltaText(TreeNodeDelta delta)
        {
            var gain = ColorUtility.ToHtmlStringRGB(GainColor);
            var dim = ColorUtility.ToHtmlStringRGB(HintColor);
            return $"{delta.From}<color=#{dim}> → </color><color=#{gain}><b>{delta.To}</b></color>";
        }

        private void BuildButtons(UiTheme theme)
        {
            var close = UiElements.Fill("Close", Root, Color.clear);
            var cross = UiElements.Label("CloseText", close.rectTransform, theme.BoldFont, 13f, KindColor, TextAlignmentOptions.Center);
            cross.text = "×";
            UiElements.Stretch(cross.rectTransform);
            UiElements.Anchor(close.rectTransform, new Vector2(1f, 1f), new Vector2(-BorderPx - 2f, -BorderPx - (TitlePx + GapPx - CloseSizePx) / 2f), Vector2.one * CloseSizePx);
            Close = new UiButton(close, cross);
            for (var i = 0; i < MaxActions; i++)
            {
                var back = UiElements.Raise(UiElements.Fill("Action " + i, Root, theme.Gold, 5f));
                var label = UiElements.Label("Action " + i + "Text", back.rectTransform, theme.BoldFont, 13f, theme.TextOnGold, TextAlignmentOptions.Center);
                UiElements.Stretch(label.rectTransform);
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 13f;
                _actions.Add(new UiButton(back, label));
            }
        }

        private TextMeshProUGUI Label(string name, TMP_FontAsset? font, float sizePx, Color color, TextAlignmentOptions alignment)
        {
            var label = UiElements.Label(name, Root, font, sizePx, color, alignment);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private void PlaceRow(TextMeshProUGUI left, TextMeshProUGUI right, float y, float heightPx)
        {
            var rightInset = _mode == TreeNodeCardMode.Pinned && left == _title ? PaddingPx + CloseSizePx : PaddingPx;
            var leftY = y;
            Place(left.rectTransform, PaddingPx, ref leftY, heightPx, rightInset);
            var rightY = y;
            Place(right.rectTransform, PaddingPx, ref rightY, heightPx, rightInset);
            if (left == _title)
            {
                var width = _title.GetPreferredValues(_title.text).x;
                right.rectTransform.offsetMin = new Vector2(PaddingPx + width + GapPx * 1.5f, right.rectTransform.offsetMin.y);
                right.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private static void Place(RectTransform line, float left, ref float y, float heightPx, float right)
        {
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = Vector2.one;
            line.pivot = new Vector2(0.5f, 1f);
            line.offsetMin = new Vector2(left, -y - heightPx);
            line.offsetMax = new Vector2(-right, -y);
            y += heightPx;
        }

        private static Color FrameOf(TreeNodeType type) => type switch
        {
            TreeNodeType.Keystone => KeystoneFrame,
            TreeNodeType.Notable or TreeNodeType.Start => NotableFrame,
            _ => SmallFrame
        };

        private static Color HeadOf(TreeNodeType type) => type switch
        {
            TreeNodeType.Keystone => KeystoneHead,
            TreeNodeType.Notable or TreeNodeType.Start => NotableHead,
            _ => SmallHead
        };
    }
}
