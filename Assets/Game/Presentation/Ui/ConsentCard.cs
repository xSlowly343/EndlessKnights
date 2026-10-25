using System.Collections.Generic;
using Game.Core.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public sealed class ConsentCard
    {
        public const int CountedLines = 5;
        public const int NotCountedLines = 3;

        private const float FrameRadiusPx = 10f;
        private const float MarginPx = 8f;
        private const float PaddingPx = 16f;
        private const float TitleWidthPx = 380f;
        private const float TitleHeightPx = 30f;
        private const float TitleSizePx = 16f;
        private const float PlateLinePx = 2f;
        private const float HeadingPx = 24f;
        private const float HeadingSizePx = 14f;
        private const float LinePx = 36f;
        private const float LineSizePx = 13f;
        private const float DotPx = 6f;
        private const float DotGapPx = 10f;
        private const float NotePx = 40f;
        private const float NoteSizePx = 12f;
        private const float SectionGapPx = 8f;
        private const float ButtonHeightPx = 36f;
        private const float AcceptWidthPx = 200f;
        private const float DeclineWidthPx = 140f;
        private const float ButtonGapPx = 12f;
        private const float ButtonRadiusPx = 6f;
        private const float ButtonSizePx = 15f;

        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _countedHeading;
        private readonly TextMeshProUGUI _notCountedHeading;
        private readonly List<TextMeshProUGUI> _counted = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _notCounted = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _note;
        private readonly UiButton _accept;
        private readonly UiButton _decline;

        private ConsentCard(RectTransform canvas, UiTheme theme)
        {
            _root = UiElements.Stretch(UiElements.Rect("Consent", canvas));
            UiElements.Stretch(UiElements.Fill("Border", _root, theme.Border, FrameRadiusPx).rectTransform);
            var body = UiElements.Stretch(UiElements.Fill("Body", _root, theme.Background, FrameRadiusPx - PanelCanvas.BorderPx).rectTransform, PanelCanvas.BorderPx);
            var top = new Vector2(0.5f, 1f);
            var plate = Button("Title", body, top, new Vector2(0f, -MarginPx), new Vector2(TitleWidthPx, TitleHeightPx), theme.Plate, theme.BoldFont, TitleSizePx);
            UiElements.BottomEdge(UiElements.Fill("Line", plate.Rect, theme.Gold).rectTransform, PlateLinePx, ButtonRadiusPx);
            _title = plate.Label!;
            var y = MarginPx + TitleHeightPx + SectionGapPx;
            _countedHeading = Heading("Counted", body, theme, theme.Success, ref y);
            for (var i = 0; i < CountedLines; i++)
                _counted.Add(Line("Counted " + i, body, theme, theme.Success, ref y));
            y += SectionGapPx;
            _notCountedHeading = Heading("NotCounted", body, theme, theme.Alert, ref y);
            for (var i = 0; i < NotCountedLines; i++)
                _notCounted.Add(Line("NotCounted " + i, body, theme, theme.Alert, ref y));
            y += SectionGapPx;
            _note = UiElements.Label("Note", body, theme.TextFont, NoteSizePx, theme.TextDim, TextAlignmentOptions.TopLeft);
            _note.textWrappingMode = TextWrappingModes.Normal;
            UiElements.Anchor(_note.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(ContentWidth, NotePx));
            var bottom = new Vector2(0.5f, 0f);
            var pairWidth = AcceptWidthPx + ButtonGapPx + DeclineWidthPx;
            _accept = Button("Accept", body, bottom, new Vector2((AcceptWidthPx - pairWidth) / 2f, MarginPx), new Vector2(AcceptWidthPx, ButtonHeightPx), theme.Success, theme.BoldFont, ButtonSizePx);
            _decline = Button("Decline", body, bottom, new Vector2((pairWidth - DeclineWidthPx) / 2f, MarginPx), new Vector2(DeclineWidthPx, ButtonHeightPx), theme.Card, theme.BoldFont, ButtonSizePx);
            _decline.Label!.color = theme.TextDim;
            _root.gameObject.SetActive(false);
        }

        public static ConsentCard Create(RectTransform canvas, UiTheme theme) => new ConsentCard(canvas, theme);

        public static float NaturalHeightPx =>
            2f * MarginPx + TitleHeightPx + 4f * SectionGapPx + 2f * HeadingPx + (CountedLines + NotCountedLines) * LinePx + NotePx + ButtonHeightPx;

        public bool IsShown => _root.gameObject.activeSelf;
        public string Title => _title.text;
        public string CountedText(int index) => _counted[index].text;
        public string NotCountedText(int index) => _notCounted[index].text;
        public Vector2 WorldPointOfAccept => Centre(_accept.Rect);
        public Vector2 WorldPointOfDecline => Centre(_decline.Rect);

        public void Show()
        {
            _title.text = ConsentTexts.Title;
            _countedHeading.text = ConsentTexts.CountedHeading;
            _notCountedHeading.text = ConsentTexts.NotCountedHeading;
            Fill(_counted, ConsentTexts.Counted);
            Fill(_notCounted, ConsentTexts.NotCounted);
            _note.text = ConsentTexts.NetworkNote;
            _accept.Text = ConsentTexts.Accept;
            _decline.Text = ConsentTexts.Decline;
            _root.gameObject.SetActive(true);
        }

        public void Hide() => _root.gameObject.SetActive(false);

        public bool IsAccept(Vector2 worldPoint) => IsShown && _accept.Contains(worldPoint);

        public bool IsDecline(Vector2 worldPoint) => IsShown && _decline.Contains(worldPoint);

        private static float ContentWidth => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private static void Fill(List<TextMeshProUGUI> labels, IReadOnlyList<string> lines)
        {
            for (var i = 0; i < labels.Count; i++)
                labels[i].text = i < lines.Count ? lines[i] : string.Empty;
        }

        private static TextMeshProUGUI Heading(string name, RectTransform body, UiTheme theme, Color color, ref float y)
        {
            var heading = UiElements.Label(name, body, theme.BoldFont, HeadingSizePx, color, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(ContentWidth, HeadingPx));
            y += HeadingPx;
            return heading;
        }

        private static TextMeshProUGUI Line(string name, RectTransform body, UiTheme theme, Color dotColor, ref float y)
        {
            var dot = UiElements.Dot(name + " Dot", body, dotColor);
            UiElements.Anchor(dot.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -(y + (LinePx - DotPx) / 2f)), Vector2.one * DotPx);
            var label = UiElements.Label(name, body, theme.TextFont, LineSizePx, theme.Text, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var indent = DotPx + DotGapPx;
            UiElements.Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx + indent, -y), new Vector2(ContentWidth - indent, LinePx));
            y += LinePx;
            return label;
        }

        private static UiButton Button(string name, RectTransform parent, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, TMP_FontAsset? font, float sizeTextPx)
        {
            var back = UiElements.Fill(name, parent, color, ButtonRadiusPx);
            UiElements.Anchor(back.rectTransform, anchor, positionPx, sizePx);
            var label = UiElements.Label(name + "Text", back.rectTransform, font, sizeTextPx, Color.white, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform);
            return new UiButton(back, label);
        }

        private static Vector2 Centre(RectTransform rect) => rect.TransformPoint(rect.rect.center);
    }
}
