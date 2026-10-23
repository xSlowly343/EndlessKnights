using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Book;
using Game.Presentation.Tree;
using UnityEngine;

namespace Game.Presentation.Strip
{
    public sealed class PanelView : MonoBehaviour
    {
        public const int MaxSubTabs = 4;
        public const int MaxMoreRows = 5;
        public const int MaxReportLines = 7;
        public const int TabCount = PanelNavigator.BarSize + 1;

        private const float MarginPx = 8f;
        private const float HeaderButtonPx = 30f;
        private const float TitlePlateWidthPx = 120f;
        private const float WideButtonWidthPx = 60f;
        private const float HandleWidthPx = 120f;
        private const float HandleHeightPx = 8f;
        private const float SubTabWidthPx = 104f;
        private const float SubTabHeightPx = 28f;
        private const float ZoomButtonPx = 28f;
        private const float TabStripePx = 3f;
        private const float MoreRowHeightPx = 44f;
        private const float ReportLineHeightPx = 28f;
        private const float ClaimWidthPx = 160f;
        private const float ClaimHeightPx = 36f;
        private const float CurrencyRefreshSeconds = 0.25f;

        private static readonly Color FrameColor = new Color(0.1f, 0.13f, 0.23f, 1f);
        private static readonly Color RowColor = new Color(0.13f, 0.19f, 0.29f, 1f);
        private static readonly Color TitleColor = new Color(0.62f, 0.12f, 0.12f, 1f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.23f, 0.35f, 1f);
        private static readonly Color ActiveColor = new Color(0.94f, 0.71f, 0.24f, 1f);
        private static readonly Color PinnedColor = new Color(0.55f, 0.42f, 0.14f, 1f);
        private static readonly Color HandleColor = new Color(0.35f, 0.4f, 0.55f, 1f);
        private static readonly Color ClaimColor = new Color(0.2f, 0.5f, 0.2f, 1f);
        private static readonly Color LineColor = new Color(0.92f, 0.94f, 1f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f, 1f);

        private readonly List<StripButton> _subTabs = new List<StripButton>();
        private readonly List<TextMesh> _subTabTexts = new List<TextMesh>();
        private readonly List<StripButton> _tabs = new List<StripButton>();
        private readonly List<TextMesh> _tabTexts = new List<TextMesh>();
        private readonly List<StripButton> _moreRows = new List<StripButton>();
        private readonly List<TextMesh> _moreTexts = new List<TextMesh>();
        private readonly List<TextMesh> _reportLines = new List<TextMesh>();
        private readonly List<PanelSection> _moreSections = new List<PanelSection>();
        private readonly List<PanelSection> _barSections = new List<PanelSection>();

        private Transform _frame = null!;
        private Transform _report = null!;
        private Transform _treeHost = null!;
        private Transform _bookHost = null!;
        private StripButton _body = null!;
        private StripButton _handle = null!;
        private StripButton _titlePlate = null!;
        private StripButton _pin = null!;
        private StripButton _close = null!;
        private StripButton _wide = null!;
        private StripButton _zoomIn = null!;
        private StripButton _zoomOut = null!;
        private StripButton _subTabRow = null!;
        private StripButton _tabRow = null!;
        private StripButton _tabStripe = null!;
        private TextMesh _title = null!;
        private TextMesh _currency = null!;
        private TextMesh _wideText = null!;
        private TextMesh _zoomInText = null!;
        private TextMesh _zoomOutText = null!;
        private TextMesh _placeholder = null!;
        private StripButton _reportBody = null!;
        private StripButton _reportTitleBar = null!;
        private StripButton _claimButton = null!;
        private TextMesh _reportTitle = null!;
        private TextMesh _claimText = null!;
        private PanelFrame _shownFrame;
        private bool _showingReport;
        private int _tabStripeIndex;
        private bool _treeShown;
        private bool _treeCaptured;
        private bool _bookShown;
        private bool _bookCaptured;
        private GameLoop? _loop;
        private string _shownGold = string.Empty;
        private float _sinceCurrency;

        public TreePanel Tree { get; private set; } = null!;
        public BookPanel Book { get; private set; } = null!;

        public event Action<PanelSection>? TabPressed;
        public event Action? MorePressed;
        public event Action? ClosePressed;
        public event Action? PinPressed;
        public event Action? HeightPressed;
        public event Action? WidePressed;
        public event Action<int>? SubTabPressed;
        public event Action? ClaimPressed;

        public static PanelView Create()
        {
            var host = new GameObject("Panel");
            var view = host.AddComponent<PanelView>();
            view.Build();
            host.SetActive(false);
            return view;
        }

        public bool IsOpen => gameObject.activeSelf;
        public bool IsShowingReport => IsOpen && _showingReport;

        public void Bind(GameLoop loop, PassiveTree tree)
        {
            _loop = loop;
            Tree.Bind(loop, tree);
            Book.Bind(loop);
        }

        public void Show(PanelNavigator navigator)
        {
            _showingReport = false;
            _shownFrame = navigator.Frame;
            _frame.gameObject.SetActive(true);
            _report.gameObject.SetActive(false);
            ShowHeader(navigator);
            ShowSubTabs(navigator);
            ShowTabs(navigator);
            ShowContent(navigator);
            _shownGold = string.Empty;
            _sinceCurrency = CurrencyRefreshSeconds;
            gameObject.SetActive(true);
        }

        public void ShowReport(string title, IReadOnlyList<string> lines)
        {
            _showingReport = true;
            _shownFrame = ReportFrame;
            _frame.gameObject.SetActive(false);
            _report.gameObject.SetActive(true);
            _reportTitle.text = title;
            for (var i = 0; i < _reportLines.Count; i++)
                _reportLines[i].text = i < lines.Count ? lines[i] : string.Empty;
            gameObject.SetActive(true);
        }

        public static PanelFrame ReportFrame => PanelGeometry.Frame(PanelHeight.Half, false, false);

        public void Place(float leftPx, bool aboveStrip)
        {
            if (_showingReport)
                ArrangeReport(leftPx, aboveStrip);
            else
                Arrange(leftPx, aboveStrip);
        }

        public void Hide()
        {
            CancelPress();
            gameObject.SetActive(false);
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsOpen)
                return false;
            if (_showingReport)
                return TryClickReport(worldPoint);
            if (TryClickHeader(worldPoint) || TryClickRows(worldPoint))
                return true;
            if (_treeShown && Tree.TryPress(worldPoint))
            {
                _treeCaptured = true;
                return true;
            }
            if (_bookShown && Book.TryPress(worldPoint))
            {
                _bookCaptured = true;
                return true;
            }
            return _body.Contains(worldPoint);
        }

        public void Drag(Vector2 worldPoint)
        {
            if (_treeCaptured)
                Tree.Drag(worldPoint);
            if (_bookCaptured)
                Book.Drag(worldPoint);
        }

        public void Release(Vector2 worldPoint)
        {
            if (_bookCaptured)
            {
                _bookCaptured = false;
                Book.Release(worldPoint);
            }
            if (!_treeCaptured)
                return;
            _treeCaptured = false;
            Tree.Release(worldPoint);
        }

        public void CancelPress()
        {
            _treeCaptured = false;
            _bookCaptured = false;
            Tree.CancelPress();
            Book.CancelPress();
        }

        private bool TryClickReport(Vector2 worldPoint)
        {
            if (_claimButton.Contains(worldPoint))
            {
                ClaimPressed?.Invoke();
                return true;
            }
            return _reportBody.Contains(worldPoint);
        }

        private bool TryClickHeader(Vector2 worldPoint)
        {
            if (Pressed(_close, worldPoint, ClosePressed) || Pressed(_pin, worldPoint, PinPressed) || Pressed(_handle, worldPoint, HeightPressed))
                return true;
            if (Pressed(_wide, worldPoint, WidePressed))
                return true;
            if (_zoomIn.Contains(worldPoint))
            {
                Tree.ZoomIn();
                return true;
            }
            if (_zoomOut.Contains(worldPoint))
            {
                Tree.ZoomOut();
                return true;
            }
            return false;
        }

        private bool TryClickRows(Vector2 worldPoint)
        {
            for (var i = 0; i < _subTabs.Count; i++)
            {
                if (!_subTabs[i].Contains(worldPoint))
                    continue;
                SubTabPressed?.Invoke(i);
                return true;
            }
            for (var i = 0; i < _moreRows.Count; i++)
            {
                if (!_moreRows[i].Contains(worldPoint) || i >= _moreSections.Count)
                    continue;
                TabPressed?.Invoke(_moreSections[i]);
                return true;
            }
            for (var i = 0; i < _tabs.Count; i++)
            {
                if (!_tabs[i].Contains(worldPoint))
                    continue;
                if (i < _barSections.Count)
                    TabPressed?.Invoke(_barSections[i]);
                else
                    MorePressed?.Invoke();
                return true;
            }
            return false;
        }

        private static bool Pressed(StripButton button, Vector2 worldPoint, Action? handler)
        {
            if (!button.Contains(worldPoint))
                return false;
            handler?.Invoke();
            return true;
        }

        private void LateUpdate()
        {
            if (_showingReport || _loop == null)
                return;
            _sinceCurrency += Time.unscaledDeltaTime;
            if (_sinceCurrency < CurrencyRefreshSeconds)
                return;
            _sinceCurrency = 0f;
            var gold = NumberFormatter.Format(_loop.State.Gold, NumberFormatSettings.Default);
            if (gold != _shownGold)
                _currency.text = "Золото " + (_shownGold = gold);
        }

        private void ShowHeader(PanelNavigator navigator)
        {
            _title.text = navigator.Title;
            _pin.GetComponent<SpriteRenderer>().color = navigator.IsPinned ? PinnedColor : ButtonColor;
            var canBeWide = !navigator.ShowingMore && PanelSections.CanBeWide(navigator.Active);
            _wide.gameObject.SetActive(canBeWide);
            _wideText.gameObject.SetActive(canBeWide);
            _wideText.text = navigator.IsWide ? "Уже" : "Шире";
            _handle.gameObject.SetActive(!navigator.IsWide && !navigator.ShowingMore);
        }

        private void ShowSubTabs(PanelNavigator navigator)
        {
            var names = navigator.SubTabs;
            var visible = _shownFrame.HasSubTabs;
            _subTabRow.gameObject.SetActive(visible);
            for (var i = 0; i < _subTabs.Count; i++)
            {
                var shown = visible && i < names.Count;
                _subTabs[i].gameObject.SetActive(shown);
                _subTabTexts[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _subTabTexts[i].text = names[i];
                _subTabs[i].GetComponent<SpriteRenderer>().color = i == navigator.SubTab ? ActiveColor : ButtonColor;
                _subTabTexts[i].color = i == navigator.SubTab ? FrameColor : LineColor;
            }
        }

        private void ShowTabs(PanelNavigator navigator)
        {
            _barSections.Clear();
            _barSections.AddRange(navigator.Bar);
            var activeIndex = navigator.ShowingMore ? _barSections.Count : _barSections.IndexOf(navigator.Active);
            for (var i = 0; i < _tabs.Count; i++)
            {
                _tabTexts[i].text = i < _barSections.Count ? PanelSections.Title(_barSections[i]) : "Ещё";
                var active = i == activeIndex;
                _tabs[i].GetComponent<SpriteRenderer>().color = active ? RowColor : FrameColor;
                _tabTexts[i].color = active ? ActiveColor : LineColor;
            }
            _tabStripe.gameObject.SetActive(activeIndex >= 0);
            _tabStripeIndex = activeIndex;
        }

        private void ShowContent(PanelNavigator navigator)
        {
            _moreSections.Clear();
            if (navigator.ShowingMore)
                _moreSections.AddRange(navigator.More);
            for (var i = 0; i < _moreRows.Count; i++)
            {
                var shown = i < _moreSections.Count;
                _moreRows[i].gameObject.SetActive(shown);
                _moreTexts[i].gameObject.SetActive(shown);
                if (shown)
                    _moreTexts[i].text = PanelSections.Title(_moreSections[i]);
            }
            _treeShown = !navigator.ShowingMore && navigator.Active == PanelSection.Tree && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            _treeHost.gameObject.SetActive(_treeShown);
            var zoomShown = _treeShown && _shownFrame.HasSubTabs;
            _zoomIn.gameObject.SetActive(zoomShown);
            _zoomOut.gameObject.SetActive(zoomShown);
            _zoomInText.gameObject.SetActive(zoomShown);
            _zoomOutText.gameObject.SetActive(zoomShown);
            _bookShown = !navigator.ShowingMore && navigator.Active == PanelSection.Book && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            _bookHost.gameObject.SetActive(_bookShown);
            var placeholder = !navigator.ShowingMore && !_treeShown && !_bookShown;
            _placeholder.gameObject.SetActive(placeholder);
            if (placeholder)
                _placeholder.text = PlaceholderText(navigator);
        }

        private static string PlaceholderText(PanelNavigator navigator)
        {
            var subTabs = navigator.SubTabs;
            var name = subTabs.Count > 0 ? $"{navigator.Title} · {subTabs[navigator.SubTab]}" : navigator.Title;
            return $"{name}\nраздел в работе";
        }

        private void Arrange(float leftPx, bool aboveStrip)
        {
            var frame = _shownFrame;
            var left = StripSpace.Units(leftPx);
            var width = StripSpace.Units(frame.WidthPx);
            var height = StripSpace.Units(frame.HeightPx);
            var gap = StripSpace.Units(PanelGeometry.GapToStripPx);
            var bottom = aboveStrip ? StripSpace.Height + gap : -gap - height;
            var top = bottom + height;
            var right = left + width;
            var margin = StripSpace.Units(MarginPx);

            Place(_body.transform, new Vector2(left + width / 2f, bottom + height / 2f), new Vector2(width, height));

            var headerY = top - StripSpace.Units(frame.HeaderPx) / 2f;
            var buttonSize = StripSpace.Units(HeaderButtonPx);
            Place(_handle.transform, new Vector2(left + width / 2f, top - StripSpace.Units(HandleHeightPx) / 2f), new Vector2(StripSpace.Units(HandleWidthPx), StripSpace.Units(HandleHeightPx)));
            var plateWidth = StripSpace.Units(TitlePlateWidthPx);
            var plateCentre = new Vector2(left + margin + plateWidth / 2f, headerY);
            Place(_titlePlate.transform, plateCentre, new Vector2(plateWidth, buttonSize));
            _title.transform.position = plateCentre;
            _currency.transform.position = new Vector2(left + margin * 2f + plateWidth, headerY);
            var closeCentre = new Vector2(right - margin - buttonSize / 2f, headerY);
            PlaceButton(_close, closeCentre, Vector2.one * buttonSize);
            var pinCentre = closeCentre - new Vector2(buttonSize + margin, 0f);
            PlaceButton(_pin, pinCentre, Vector2.one * buttonSize);
            var wideWidth = StripSpace.Units(WideButtonWidthPx);
            var wideCentre = new Vector2(pinCentre.x - buttonSize / 2f - margin - wideWidth / 2f, headerY);
            Place(_wide.transform, wideCentre, new Vector2(wideWidth, buttonSize));
            _wideText.transform.position = wideCentre;

            var subTabsTop = top - StripSpace.Units(frame.HeaderPx);
            var subTabsHeight = StripSpace.Units(frame.SubTabsPx);
            var subTabsY = subTabsTop - subTabsHeight / 2f;
            Place(_subTabRow.transform, new Vector2(left + width / 2f, subTabsY), new Vector2(width, subTabsHeight));
            var pillSize = new Vector2(StripSpace.Units(SubTabWidthPx) - margin, StripSpace.Units(SubTabHeightPx));
            for (var i = 0; i < _subTabs.Count; i++)
            {
                var centre = new Vector2(left + margin + pillSize.x / 2f + i * StripSpace.Units(SubTabWidthPx), subTabsY);
                Place(_subTabs[i].transform, centre, pillSize);
                _subTabTexts[i].transform.position = centre;
            }
            var zoomSize = Vector2.one * StripSpace.Units(ZoomButtonPx);
            var zoomInCentre = new Vector2(right - margin - zoomSize.x / 2f, subTabsY);
            var zoomOutCentre = zoomInCentre - new Vector2(zoomSize.x + margin / 2f, 0f);
            Place(_zoomIn.transform, zoomInCentre, zoomSize);
            Place(_zoomOut.transform, zoomOutCentre, zoomSize);
            _zoomInText.transform.position = zoomInCentre;
            _zoomOutText.transform.position = zoomOutCentre;

            var tabHeight = StripSpace.Units(frame.TabBarPx);
            var tabWidth = width / TabCount;
            var tabY = bottom + tabHeight / 2f;
            Place(_tabRow.transform, new Vector2(left + width / 2f, tabY), new Vector2(width, tabHeight));
            for (var i = 0; i < _tabs.Count; i++)
            {
                var centre = new Vector2(left + tabWidth * (i + 0.5f), tabY);
                Place(_tabs[i].transform, centre, new Vector2(tabWidth, tabHeight));
                _tabTexts[i].transform.position = centre;
            }
            var stripeHeight = StripSpace.Units(TabStripePx);
            Place(_tabStripe.transform, new Vector2(left + tabWidth * (_tabStripeIndex + 0.5f), bottom + tabHeight - stripeHeight / 2f), new Vector2(tabWidth, stripeHeight));

            var contentBottom = bottom + tabHeight;
            var contentHeight = StripSpace.Units(frame.ContentPx);
            var contentTop = contentBottom + contentHeight;
            _placeholder.transform.position = new Vector2(left + width / 2f, contentBottom + contentHeight / 2f);
            var rowHeight = StripSpace.Units(MoreRowHeightPx);
            for (var i = 0; i < _moreRows.Count; i++)
            {
                var centre = new Vector2(left + width / 2f, contentTop - margin - rowHeight / 2f - i * (rowHeight + margin));
                Place(_moreRows[i].transform, centre, new Vector2(width - 2f * margin, rowHeight));
                _moreTexts[i].transform.position = centre;
            }
            Tree.Arrange(left, contentBottom, width, contentHeight, StripSpace.PixelsPerUnit);
            Book.Arrange(left, contentBottom, width, contentHeight, StripSpace.PixelsPerUnit);
        }

        private void ArrangeReport(float leftPx, bool aboveStrip)
        {
            var frame = _shownFrame;
            var left = StripSpace.Units(leftPx);
            var width = StripSpace.Units(frame.WidthPx);
            var height = StripSpace.Units(frame.HeightPx);
            var gap = StripSpace.Units(PanelGeometry.GapToStripPx);
            var margin = StripSpace.Units(MarginPx);
            var titleHeight = StripSpace.Units(frame.HeaderPx);
            var lineHeight = StripSpace.Units(ReportLineHeightPx);
            var claimSize = new Vector2(StripSpace.Units(ClaimWidthPx), StripSpace.Units(ClaimHeightPx));
            var bottom = aboveStrip ? StripSpace.Height + gap : -gap - height;
            var centre = new Vector2(left + width / 2f, bottom + height / 2f);
            Place(_reportBody.transform, centre, new Vector2(width, height));
            var titleCentre = new Vector2(centre.x, bottom + height - titleHeight / 2f);
            Place(_reportTitleBar.transform, titleCentre, new Vector2(width, titleHeight));
            _reportTitle.transform.position = titleCentre;
            var claimCentre = new Vector2(centre.x, bottom + margin + claimSize.y / 2f);
            Place(_claimButton.transform, claimCentre, claimSize);
            _claimText.transform.position = claimCentre;
            var firstLineY = bottom + height - titleHeight - margin - lineHeight / 2f;
            for (var i = 0; i < _reportLines.Count; i++)
                _reportLines[i].transform.position = new Vector2(left + 2f * margin, firstLineY - i * lineHeight);
        }

        private void Build()
        {
            _frame = Group("Frame");
            _body = Button("Body", FrameColor, _frame, 5);
            _subTabRow = Button("SubTabRow", RowColor, _frame, 6);
            _tabRow = Button("TabRow", FrameColor, _frame, 6);
            _handle = Button("Handle", HandleColor, _frame, 7);
            _titlePlate = Button("TitlePlate", TitleColor, _frame, 7);
            _title = Text("Title", 0.2f, TextAnchor.MiddleCenter, Color.white, _frame);
            _currency = Text("Currency", 0.15f, TextAnchor.MiddleLeft, GoldColor, _frame);
            _pin = Button("Pin", ButtonColor, _frame, 7);
            Text("PinText", 0.18f, TextAnchor.MiddleCenter, Color.white, _frame).text = "П";
            _close = Button("Close", ButtonColor, _frame, 7);
            Text("CloseText", 0.2f, TextAnchor.MiddleCenter, Color.white, _frame).text = "X";
            _wide = Button("Wide", ButtonColor, _frame, 7);
            _wideText = Text("WideText", 0.15f, TextAnchor.MiddleCenter, Color.white, _frame);
            _zoomOut = Button("ZoomOut", ButtonColor, _frame, 7);
            _zoomOutText = Text("ZoomOutText", 0.22f, TextAnchor.MiddleCenter, Color.white, _frame);
            _zoomOutText.text = "−";
            _zoomIn = Button("ZoomIn", ButtonColor, _frame, 7);
            _zoomInText = Text("ZoomInText", 0.22f, TextAnchor.MiddleCenter, Color.white, _frame);
            _zoomInText.text = "+";
            for (var i = 0; i < MaxSubTabs; i++)
            {
                _subTabs.Add(Button("SubTab " + i, ButtonColor, _frame, 7));
                _subTabTexts.Add(Text("SubTabText " + i, 0.14f, TextAnchor.MiddleCenter, LineColor, _frame));
            }
            for (var i = 0; i < TabCount; i++)
            {
                _tabs.Add(Button("Tab " + i, FrameColor, _frame, 7));
                _tabTexts.Add(Text("TabText " + i, 0.15f, TextAnchor.MiddleCenter, LineColor, _frame));
            }
            _tabStripe = Button("TabStripe", ActiveColor, _frame, 8);
            for (var i = 0; i < MaxMoreRows; i++)
            {
                _moreRows.Add(Button("More " + i, ButtonColor, _frame, 7));
                _moreTexts.Add(Text("MoreText " + i, 0.17f, TextAnchor.MiddleCenter, LineColor, _frame));
            }
            _placeholder = Text("Placeholder", 0.17f, TextAnchor.MiddleCenter, LineColor, _frame);
            _treeHost = Group("Tree", _frame);
            Tree = TreePanel.Create(_treeHost);
            _bookHost = Group("Book", _frame);
            Book = BookPanel.Create(_bookHost);
            BuildReport();
        }

        private void BuildReport()
        {
            _report = Group("Report");
            _reportBody = Button("Body", FrameColor, _report, 5);
            _reportTitleBar = Button("Title", TitleColor, _report, 6);
            _claimButton = Button("Claim", ClaimColor, _report, 6);
            _reportTitle = Text("TitleText", 0.22f, TextAnchor.MiddleCenter, Color.white, _report);
            _claimText = Text("ClaimText", 0.22f, TextAnchor.MiddleCenter, Color.white, _report);
            _claimText.text = "Забрать";
            for (var i = 0; i < MaxReportLines; i++)
                _reportLines.Add(Text("Line " + i, 0.17f, TextAnchor.MiddleLeft, LineColor, _report));
        }

        private Transform Group(string name, Transform? parent = null)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent ?? transform, false);
            return group;
        }

        private static StripButton Button(string name, Color color, Transform parent, int sortingOrder)
        {
            var button = StripButton.Create(name, color, Vector2.zero, Vector2.one, parent);
            button.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
            return button;
        }

        private static TextMesh Text(string name, float size, TextAnchor anchor, Color color, Transform parent) =>
            StripText.Create(name, string.Empty, Vector2.zero, size, anchor, color, parent, 9);

        private static void PlaceButton(StripButton button, Vector2 centre, Vector2 size)
        {
            Place(button.transform, centre, size);
            var label = button.transform.parent.Find(button.name + "Text");
            if (label != null)
                label.position = centre;
        }

        private static void Place(Transform item, Vector2 centre, Vector2 size)
        {
            item.position = new Vector3(centre.x, centre.y, item.position.z);
            item.localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
