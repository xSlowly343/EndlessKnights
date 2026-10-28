using System;
using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Strip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class PanelCanvas : MonoBehaviour
    {
        public const int MaxSubTabs = 4;
        public const int PartySearchSubTab = 3;
        public const int MaxMoreRows = 5;
        public const int MaxReportLines = 7;
        public const int TabCount = PanelNavigator.BarSize + 1;
        public const float TweenSeconds = 0.15f;
        public const float BorderPx = 1f;
        public const int SortingOrder = 5;

        private const float MarginPx = 8f;
        private const float FrameRadiusPx = 10f;
        private const float CornerGemPx = 12f;
        private const float CornerGemRimPx = 6f;
        private const float CornerGemInsetPx = 4f;
        private const float ButtonRadiusPx = 6f;
        private const float PillRadiusPx = 14f;
        private const float HeaderButtonPx = 30f;
        private const float PlateWidthPx = 128f;
        private const float PlateLinePx = 2f;
        private const float WideButtonWidthPx = 64f;
        private const float HandleWidthPx = 120f;
        private const float HandleHeightPx = 8f;
        private const float HandleBarWidthPx = 44f;
        private const float HandleBarHeightPx = 4f;
        private const float PillHeightPx = 28f;
        private const int PillPaddingPx = 14;
        private const float TabStripePx = 3f;
        private const float TabStripeInsetPx = 12f;
        private const float MoreRowHeightPx = 44f;
        private const float MoreTextInsetPx = 16f;
        private const float MoreIconPx = 24f;
        private const float TabIconPx = 24f;
        private const float TabIconTopPx = 8f;
        private const float ReportPlateWidthPx = 280f;
        private const float ReportLineHeightPx = 28f;
        private const float ClaimWidthPx = 160f;
        private const float ClaimHeightPx = 36f;
        private const float CoinPx = 22f;
        private const float CurrencyWidthPx = 140f;
        private const float CurrencyRefreshSeconds = 0.25f;
        private const float TitleSizePx = 16f;
        private const float TextSizePx = 15f;
        private const float SmallTextSizePx = 13f;
        private const float NumberSizePx = 19f;
        private const float GlyphSizePx = 20f;
        private const float NoticeMinSizePx = 10f;
        private const float NoticeRightPx = 394f;
        private const float NoticeRightWithWidePx = 322f;
        private const float NoticeSeconds = 3f;
        private const float PresetWidthPx = 96f;

        private readonly List<UiButton> _subTabs = new List<UiButton>();
        private readonly List<UiButton> _tabs = new List<UiButton>();
        private readonly List<Image> _tabStripes = new List<Image>();
        private readonly List<UiButton> _moreRows = new List<UiButton>();
        private readonly List<TextMeshProUGUI> _reportLines = new List<TextMeshProUGUI>();
        private readonly List<PanelSection> _moreSections = new List<PanelSection>();
        private readonly List<PanelSection> _barSections = new List<PanelSection>();
        private readonly List<SectionMark> _tabMarks = new List<SectionMark>();
        private readonly List<SectionMark> _moreMarks = new List<SectionMark>();
        private IReadOnlyList<PanelSection> _moreAll = Array.Empty<PanelSection>();
        private PanelMarks? _marks;
        private Image _coin = null!;
        private TextMeshProUGUI _notice = null!;
        private float _noticeLeft;
        private HeroPanel _heroPanel = null!;
        private ItemsPanel _itemsPanel = null!;
        private StatsPanel _statsPanel = null!;
        private MasterPanel _masterPanel = null!;
        private PartyPanel _partyPanel = null!;
        private PartySearchPanel _partySearchPanel = null!;
        private SettingsPanel _settingsPanel = null!;
        private ConsentCard _consent = null!;
        private UiButton _preset = null!;

        private UiTheme _theme = null!;
        private RectTransform _canvasRect = null!;
        private ButtonHighlight _highlight = null!;
        private BoxCollider2D _hitArea = null!;
        private RectTransform _frame = null!;
        private RectTransform _report = null!;
        private RectTransform _subTabRow = null!;
        private TextMeshProUGUI _slots = null!;
        private UiButton _handle = null!;
        private UiButton _titlePlate = null!;
        private UiButton _pin = null!;
        private Image _pinDot = null!;
        private UiButton _close = null!;
        private UiButton _wide = null!;
        private UiButton _claim = null!;
        private TextMeshProUGUI _currency = null!;
        private TextMeshProUGUI _placeholder = null!;
        private TextMeshProUGUI _reportTitle = null!;
        private PanelFrame _shownFrame;
        private bool _showingCard;
        private bool _showingConsent;
        private bool _aboveStrip = true;
        private bool _treeShown;
        private bool _treeCaptured;
        private bool _subclassShown;
        private bool _bookShown;
        private bool _bookCaptured;
        private float _heightPx;
        private float _tweenFromPx;
        private float _tweenToPx;
        private float _tweenElapsed = TweenSeconds;
        private GameLoop? _loop;
        private string _shownGold = string.Empty;
        private float _sinceCurrency;

        public TreePanel Tree { get; private set; } = null!;
        public BookPanel Book { get; private set; } = null!;
        public SubclassPanel Subclass { get; private set; } = null!;

        public event Action<PanelSection>? TabPressed;
        public event Action? MorePressed;
        public event Action? ClosePressed;
        public event Action? PinPressed;
        public event Action? HeightPressed;
        public event Action? WidePressed;
        public event Action<int>? SubTabPressed;
        public event Action? ClaimPressed;
        public event Action? ConsentAccepted;
        public event Action? ConsentDeclined;

        public static PanelCanvas Create()
        {
            var host = new GameObject("Panel");
            var view = host.AddComponent<PanelCanvas>();
            view.Build();
            host.SetActive(false);
            return view;
        }

        public static PanelFrame ReportFrame => PanelGeometry.Frame(PanelHeight.Half, false, false);
        public static PanelFrame ConsentFrame => new PanelFrame(PanelGeometry.WidthPx, Mathf.CeilToInt(ConsentCard.NaturalHeightPx), 0, 0, 0);
        public static float CollapsedHeightPx => PanelGeometry.HeaderPx + PanelGeometry.TabBarPx + 2f * BorderPx;

        public bool IsOpen => gameObject.activeSelf;
        public bool IsShowingReport => IsOpen && _showingCard && !_showingConsent;
        public bool IsShowingConsent => IsOpen && _showingConsent;
        public bool IsShowingCard => IsOpen && _showingCard;
        public bool IsTweening => _tweenElapsed < TweenSeconds;
        public float HeightPx => _heightPx;
        public RectTransform CanvasRect => _canvasRect;
        public bool IsNoticeShown => _notice.gameObject.activeSelf;
        public HeroPanel Hero => _heroPanel;
        public ItemsPanel Items => _itemsPanel;
        public StatsPanel Stats => _statsPanel;
        public MasterPanel Master => _masterPanel;
        public PartyPanel Party => _partyPanel;
        public PartySearchPanel PartySearch => _partySearchPanel;
        public SettingsPanel Settings => _settingsPanel;
        public ConsentCard Consent => _consent;

        public SectionMark TabMark(int index) => _tabMarks[index];
        public SectionMark MoreMark(int index) => _moreMarks[index];

        public void UseMarks(PanelMarks marks) => _marks = marks;

        public void RefreshMarks()
        {
            if (IsOpen && !_showingCard)
                ShowMarks();
        }

        public void ShowNotice(string text)
        {
            _notice.text = text;
            _noticeLeft = NoticeSeconds;
            ShowNoticeInsteadOfGold(true);
        }

        public void Bind(GameLoop loop, PassiveTree tree)
        {
            _loop = loop;
            Tree.Bind(loop, tree);
            Subclass.Bind(loop);
            Book.Bind(loop);
            _itemsPanel.Bind(loop);
            _masterPanel.Bind(loop);
        }

        public void Show(PanelNavigator navigator)
        {
            var startPx = IsOpen && !_showingCard ? _heightPx : CollapsedHeightPx;
            _showingCard = false;
            _showingConsent = false;
            _shownFrame = navigator.Frame;
            _frame.gameObject.SetActive(true);
            _report.gameObject.SetActive(false);
            _consent.Hide();
            ShowHeader(navigator);
            ShowSubTabs(navigator);
            ShowTabs(navigator);
            ShowContent(navigator);
            ShowMarks();
            _shownGold = string.Empty;
            _sinceCurrency = CurrencyRefreshSeconds;
            RefreshLive();
            gameObject.SetActive(true);
            StartTween(startPx, _shownFrame.HeightPx);
        }

        public void ShowReport(string title, IReadOnlyList<string> lines, string? action = null)
        {
            var startPx = IsOpen ? _heightPx : CollapsedHeightPx;
            _showingCard = true;
            _showingConsent = false;
            _shownFrame = ReportFrame;
            _frame.gameObject.SetActive(false);
            _consent.Hide();
            _report.gameObject.SetActive(true);
            _reportTitle.text = title;
            _claim.Text = action ?? Game.Core.Text.Texts.Get("report.claim");
            for (var i = 0; i < _reportLines.Count; i++)
                _reportLines[i].text = i < lines.Count ? lines[i] : string.Empty;
            gameObject.SetActive(true);
            StartTween(startPx, _shownFrame.HeightPx);
        }

        public void ShowConsent()
        {
            var startPx = IsOpen ? _heightPx : CollapsedHeightPx;
            _showingCard = true;
            _showingConsent = true;
            _shownFrame = ConsentFrame;
            _frame.gameObject.SetActive(false);
            _report.gameObject.SetActive(false);
            _consent.Show();
            gameObject.SetActive(true);
            StartTween(startPx, _shownFrame.HeightPx);
        }

        public void Place(float leftPx, bool aboveStrip)
        {
            _aboveStrip = aboveStrip;
            var gap = StripSpace.Units(PanelGeometry.GapToStripPx);
            var left = StripSpace.Units(leftPx);
            _canvasRect.pivot = aboveStrip ? Vector2.zero : Vector2.up;
            _canvasRect.position = new Vector3(left, aboveStrip ? StripSpace.Height + gap : -gap, 0f);
            ApplyHeight(_heightPx);
        }

        public void Hide()
        {
            CancelPress();
            Tree.HoverAway();
            Subclass.HoverAway();
            _noticeLeft = 0f;
            ShowNoticeInsteadOfGold(false);
            gameObject.SetActive(false);
        }

        public void Point(Vector2 worldPoint, bool held)
        {
            if (IsOpen)
                _highlight.Point(worldPoint, held);
            else
                _highlight.Clear();
            if (IsOpen && _treeShown && !held)
                Tree.Hover(worldPoint);
            else
                Tree.HoverAway();
            if (IsOpen && _subclassShown && !held)
                Subclass.Hover(worldPoint);
            else
                Subclass.HoverAway();
        }

        public void PointAway()
        {
            _highlight.Clear();
            Tree.HoverAway();
            Subclass.HoverAway();
        }

        public void SetRoomBeside(float leftPx, float rightPx)
        {
            Tree.SetRoomBeside(leftPx, rightPx);
            Subclass.SetRoomBeside(leftPx, rightPx);
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsOpen)
                return false;
            if (_showingConsent)
                return TryClickConsent(worldPoint);
            if (_showingCard)
                return TryClickReport(worldPoint);
            if (TryClickHeader(worldPoint) || TryClickRows(worldPoint) || _heroPanel.TryClick(worldPoint) || _itemsPanel.TryClick(worldPoint) || _masterPanel.TryClick(worldPoint) || _partyPanel.TryClick(worldPoint) || _partySearchPanel.TryClick(worldPoint) || _settingsPanel.TryClick(worldPoint))
                return true;
            if (_preset.Contains(worldPoint))
            {
                _loop?.SwitchPreset();
                RefreshPreset();
                return true;
            }
            if (_treeShown && Tree.TryPress(worldPoint))
            {
                _treeCaptured = true;
                return true;
            }
            if (_subclassShown && Subclass.TryPress(worldPoint))
                return true;
            if (_bookShown && Book.TryPress(worldPoint))
            {
                _bookCaptured = true;
                return true;
            }
            return UiElements.Contains(_canvasRect, worldPoint);
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

        public void FinishTween()
        {
            if (!IsTweening)
                return;
            _tweenElapsed = TweenSeconds;
            ApplyHeight(_tweenToPx);
        }

        private bool TryClickReport(Vector2 worldPoint)
        {
            if (_claim.Contains(worldPoint))
            {
                ClaimPressed?.Invoke();
                return true;
            }
            return UiElements.Contains(_canvasRect, worldPoint);
        }

        private bool TryClickConsent(Vector2 worldPoint)
        {
            if (_consent.IsAccept(worldPoint))
            {
                ConsentAccepted?.Invoke();
                return true;
            }
            if (_consent.IsDecline(worldPoint))
            {
                ConsentDeclined?.Invoke();
                return true;
            }
            return UiElements.Contains(_canvasRect, worldPoint);
        }

        private bool TryClickHeader(Vector2 worldPoint)
        {
            if (Pressed(_close, worldPoint, ClosePressed) || Pressed(_pin, worldPoint, PinPressed) || Pressed(_handle, worldPoint, HeightPressed))
                return true;
            return Pressed(_wide, worldPoint, WidePressed);
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

        private static bool Pressed(UiButton button, Vector2 worldPoint, Action? handler)
        {
            if (!button.Contains(worldPoint))
                return false;
            handler?.Invoke();
            return true;
        }

        private void LateUpdate()
        {
            AdvanceTween();
            AdvanceNotice();
            RefreshLive();
            Tree.Tick(Time.unscaledDeltaTime);
        }

        private void RefreshLive()
        {
            if (_showingCard || _loop == null)
                return;
            _sinceCurrency += Time.unscaledDeltaTime;
            if (_sinceCurrency < CurrencyRefreshSeconds)
                return;
            _sinceCurrency = 0f;
            RefreshCurrency(_loop);
            _heroPanel.Refresh(_loop);
            _statsPanel.Refresh(_loop);
            _itemsPanel.Refresh();
            _masterPanel.Refresh();
            _partyPanel.Refresh(_loop);
            Book.Refresh();
            RefreshPreset();
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (_loop != null && _slots.gameObject.activeSelf)
                _slots.text = BookTexts.Slots(_loop.Book.SlotCount, _loop.Book.MaxSlots);
        }

        private void RefreshPreset()
        {
            if (_loop != null && _preset.Rect.gameObject.activeSelf)
                _preset.Text = HeroSummary.Preset(_loop.State);
        }

        private void AdvanceNotice()
        {
            if (_noticeLeft <= 0f)
                return;
            _noticeLeft -= Time.unscaledDeltaTime;
            if (_noticeLeft <= 0f)
                ShowNoticeInsteadOfGold(false);
        }

        private void ShowNoticeInsteadOfGold(bool notice)
        {
            _notice.gameObject.SetActive(notice);
            _coin.gameObject.SetActive(!notice);
            _currency.gameObject.SetActive(!notice);
        }

        private void ShowMarks()
        {
            for (var i = 0; i < _tabMarks.Count; i++)
            {
                if (i < _barSections.Count)
                    _tabMarks[i].Show(HasDot(_barSections[i]), LockStage(_barSections[i]));
                else
                    _tabMarks[i].Show(_marks != null && _marks.HasDotAmong(_moreAll), 0);
            }
            for (var i = 0; i < _moreSections.Count && i < _moreMarks.Count; i++)
                _moreMarks[i].Show(HasDot(_moreSections[i]), LockStage(_moreSections[i]));
        }

        private bool HasDot(PanelSection section) => _marks != null && _marks.HasDot(section);

        private long LockStage(PanelSection section) => _marks == null || _marks.IsOpen(section) ? 0 : _marks.UnlockStage(section);

        private void AdvanceTween()
        {
            if (!IsTweening)
                return;
            _tweenElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_tweenElapsed / TweenSeconds);
            ApplyHeight(Mathf.Lerp(_tweenFromPx, _tweenToPx, 1f - (1f - t) * (1f - t)));
        }

        private void RefreshCurrency(GameLoop loop)
        {
            var gold = NumberFormatter.Format(loop.State.Gold, NumberDisplay.Settings);
            if (gold != _shownGold)
                _currency.text = _shownGold = gold;
        }

        private void StartTween(float fromPx, float toPx)
        {
            _tweenToPx = toPx;
            if (!Application.isPlaying || toPx <= fromPx)
            {
                _tweenElapsed = TweenSeconds;
                ApplyHeight(toPx);
            }
            else
            {
                _tweenFromPx = fromPx;
                _tweenElapsed = 0f;
                ApplyHeight(fromPx);
            }
        }

        private void ApplyHeight(float heightPx)
        {
            _heightPx = heightPx;
            var size = new Vector2(_shownFrame.WidthPx, heightPx);
            _canvasRect.sizeDelta = size;
            _hitArea.size = size;
            _hitArea.offset = new Vector2(size.x / 2f, _aboveStrip ? size.y / 2f : -size.y / 2f);
        }

        private void ShowHeader(PanelNavigator navigator)
        {
            _titlePlate.Text = navigator.Title;
            _pinDot.color = navigator.IsPinned ? _theme.Gold : _theme.TextDim;
            var canBeWide = !navigator.ShowingMore && PanelSections.CanBeWide(navigator.Active);
            _wide.Show(canBeWide);
            _wide.Text = navigator.IsWide ? "Уже" : "Шире";
            var noticeLeft = _notice.rectTransform.anchoredPosition.x;
            _notice.rectTransform.sizeDelta = new Vector2((canBeWide ? NoticeRightWithWidePx : NoticeRightPx) - noticeLeft, HeaderButtonPx);
            _handle.Show(!navigator.IsWide && !navigator.ShowingMore);
        }

        private void ShowSubTabs(PanelNavigator navigator)
        {
            var names = navigator.SubTabs;
            var visible = _shownFrame.HasSubTabs;
            _subTabRow.gameObject.SetActive(visible);
            for (var i = 0; i < _subTabs.Count; i++)
            {
                var pill = _subTabs[i];
                var shown = visible && i < names.Count;
                pill.Show(shown);
                if (!shown)
                    continue;
                var active = i == navigator.SubTab;
                pill.Text = names[i];
                pill.Back.color = active ? _theme.Gold : _theme.Card;
                pill.Label!.color = active ? _theme.TextOnGold : _theme.Text;
                pill.Label.font = Font(active ? _theme.BoldFont : _theme.TextFont);
            }
        }

        private void ShowTabs(PanelNavigator navigator)
        {
            _barSections.Clear();
            _barSections.AddRange(navigator.Bar);
            _moreAll = navigator.More;
            var activeIndex = navigator.ShowingMore ? _barSections.Count : _barSections.IndexOf(navigator.Active);
            for (var i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var active = i == activeIndex;
                var inBar = i < _barSections.Count;
                tab.Text = inBar ? PanelSections.Title(_barSections[i]) : "Ещё";
                UiElements.Use(_tabMarks[i].Icon, inBar ? UiIcons.Tab(_barSections[i]) : UiIcons.Tab(UiIcons.More));
                _tabMarks[i].Tint(active ? _theme.Gold : _theme.TextDim);
                tab.Back.color = active ? _theme.Card : Color.clear;
                tab.Label!.color = active ? _theme.Gold : _theme.TextDim;
                tab.Label.font = Font(active ? _theme.BoldFont : _theme.TextFont);
                _tabStripes[i].gameObject.SetActive(active);
            }
        }

        private void ShowContent(PanelNavigator navigator)
        {
            _moreSections.Clear();
            if (navigator.ShowingMore)
                _moreSections.AddRange(navigator.More);
            for (var i = 0; i < _moreRows.Count; i++)
            {
                var shown = i < _moreSections.Count;
                _moreRows[i].Show(shown);
                if (!shown)
                    continue;
                _moreRows[i].Text = PanelSections.Title(_moreSections[i]);
                UiElements.Use(_moreMarks[i].Icon, UiIcons.Tab(_moreSections[i]));
            }
            _treeShown = !navigator.ShowingMore && navigator.Active == PanelSection.Tree && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            Tree.Show(_treeShown, _shownFrame.WidthPx, _shownFrame.ContentPx);
            _subclassShown = !navigator.ShowingMore && navigator.Active == PanelSection.Tree && navigator.SubTab == 1 && _shownFrame.HasSubTabs;
            Subclass.Show(_subclassShown, _shownFrame.WidthPx, _shownFrame.ContentPx);
            _bookShown = !navigator.ShowingMore && navigator.Active == PanelSection.Book && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            Book.Show(_bookShown, _shownFrame.ContentPx);
            var masterShown = !navigator.ShowingMore && navigator.Active == PanelSection.Book && navigator.SubTab == 1 && _shownFrame.HasSubTabs;
            _masterPanel.Show(masterShown);
            _slots.gameObject.SetActive(!navigator.ShowingMore && navigator.Active == PanelSection.Book && _shownFrame.HasSubTabs);
            var heroSubTab = _shownFrame.HasSubTabs ? navigator.SubTab : 0;
            var heroShown = !navigator.ShowingMore && navigator.Active == PanelSection.Hero && HeroPanel.Covers(heroSubTab);
            _heroPanel.Show(heroShown, heroSubTab, _shownFrame.ContentPx);
            _preset.Show(!navigator.ShowingMore && navigator.Active == PanelSection.Hero && _shownFrame.HasSubTabs);
            var itemsShown = !navigator.ShowingMore && navigator.Active == PanelSection.Items && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            _itemsPanel.Show(itemsShown);
            var partyShown = !navigator.ShowingMore && navigator.Active == PanelSection.Party && (navigator.SubTab == 0 || !_shownFrame.HasSubTabs);
            _partyPanel.Show(partyShown, _loop);
            var partySearchShown = !navigator.ShowingMore && navigator.Active == PanelSection.Party && navigator.SubTab == PartySearchSubTab && _shownFrame.HasSubTabs;
            _partySearchPanel.Show(partySearchShown, _shownFrame.ContentPx);
            var statsShown = !navigator.ShowingMore && navigator.Active == PanelSection.Stats;
            _statsPanel.Show(statsShown);
            var settingsShown = !navigator.ShowingMore && navigator.Active == PanelSection.Settings;
            _settingsPanel.Show(settingsShown, _shownFrame.HasSubTabs ? navigator.SubTab : 0, _shownFrame.ContentPx);
            var placeholder = !navigator.ShowingMore && !_treeShown && !_subclassShown && !_bookShown && !masterShown && !heroShown && !itemsShown && !partyShown && !partySearchShown && !statsShown && !settingsShown;
            _placeholder.gameObject.SetActive(placeholder);
            if (placeholder)
                _placeholder.text = PlaceholderText(navigator);
        }

        private static string PlaceholderText(PanelNavigator navigator)
        {
            var subTabs = navigator.SubTabs;
            var name = subTabs.Count > 0 ? $"{navigator.Title} · {subTabs[navigator.SubTab]}" : navigator.Title;
            return $"{name}\n<size=80%>раздел в работе</size>";
        }

        private void Build()
        {
            _theme = UiTheme.Load();
            _canvasRect = UiElements.Rect("Canvas", transform);
            var canvas = _canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;
            _canvasRect.localScale = Vector3.one / StripSpace.PixelsPerUnit;
            _canvasRect.pivot = Vector2.zero;
            _hitArea = _canvasRect.gameObject.AddComponent<BoxCollider2D>();
            _canvasRect.gameObject.AddComponent<StripClickable>();
            _frame = BuildFrame();
            _report = BuildReport();
            _consent = ConsentCard.Create(_canvasRect, _theme);
            _highlight = new ButtonHighlight(_canvasRect);
            Tree.UseTooltip(new TreeNodeCardView("TreeTooltip", _canvasRect, _theme, TreeNodeCardMode.Tooltip), _frame);
            Subclass.UseTooltip(new TreeNodeCardView("SubclassTooltip", _canvasRect, _theme, TreeNodeCardMode.Tooltip), _frame);
        }

        private RectTransform BuildFrame()
        {
            var body = Shell("Frame");
            var column = body.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            BuildHeader(Row("Header", body, PanelGeometry.HeaderPx));
            _subTabRow = Row("SubTabs", body, 0f);
            var subTabsLayout = _subTabRow.gameObject.AddComponent<LayoutElement>();
            subTabsLayout.preferredHeight = PanelGeometry.SubTabsPx;
            BuildSubTabs(_subTabRow);
            var content = Row("Content", body, 0f);
            content.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            BuildContent(content);
            BuildTabBar(Row("TabBar", body, PanelGeometry.TabBarPx));
            BuildHandle(body);
            return (RectTransform)body.parent;
        }

        private RectTransform Shell(string name)
        {
            var root = UiElements.Stretch(UiElements.Rect(name, _canvasRect));
            UiElements.Stretch(UiElements.Fill("Border", root, _theme.Border, FrameRadiusPx).rectTransform);
            var body = UiElements.Stretch(UiElements.Fill("Body", root, _theme.Background, FrameRadiusPx - BorderPx).rectTransform, BorderPx);
            AddCornerGems(root);
            return body;
        }

        private void AddCornerGems(RectTransform root)
        {
            for (var corner = 0; corner < 4; corner++)
            {
                var anchor = new Vector2(corner % 2, corner / 2);
                var offset = new Vector2(anchor.x > 0f ? -CornerGemInsetPx : CornerGemInsetPx, anchor.y > 0f ? -CornerGemInsetPx : CornerGemInsetPx);
                var rim = UiElements.Shape("CornerGem", root, _theme.TabBar, UiSprites.Diamond);
                UiElements.Anchor(rim.rectTransform, anchor, offset, Vector2.one * (CornerGemPx + CornerGemRimPx));
                rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var gem = UiElements.Shape("Gold", rim.rectTransform, _theme.Gold, UiSprites.Diamond);
                UiElements.Anchor(gem.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * CornerGemPx);
            }
        }

        private static RectTransform Row(string name, RectTransform parent, float heightPx)
        {
            var row = UiElements.Rect(name, parent);
            if (heightPx > 0f)
                UiElements.FixedHeight(row, heightPx);
            return row;
        }

        private void BuildHeader(RectTransform header)
        {
            var middleLeft = new Vector2(0f, 0.5f);
            var middleRight = new Vector2(1f, 0.5f);
            _titlePlate = Button("TitlePlate", header, middleLeft, new Vector2(MarginPx, 0f), new Vector2(PlateWidthPx, HeaderButtonPx), _theme.Plate, ButtonRadiusPx, _theme.BoldFont, TitleSizePx);
            _titlePlate.Label!.color = Color.white;
            UiElements.BottomEdge(UiElements.Fill("Line", _titlePlate.Rect, _theme.Gold).rectTransform, PlateLinePx, ButtonRadiusPx);
            var coinX = 2f * MarginPx + PlateWidthPx;
            _coin = UiElements.Icon("Coin", header, UiIcons.Currency(UiIcons.Gold), _theme.Gold);
            UiElements.Anchor(_coin.rectTransform, middleLeft, new Vector2(coinX, 0f), Vector2.one * CoinPx);
            _currency = UiElements.Label("Currency", header, _theme.NumberFont, NumberSizePx, _theme.Gold, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(_currency.rectTransform, middleLeft, new Vector2(coinX + CoinPx + MarginPx / 2f, 0f), new Vector2(CurrencyWidthPx, HeaderButtonPx));
            _notice = UiElements.Label("Notice", header, _theme.TextFont, SmallTextSizePx, _theme.Text, TextAlignmentOptions.MidlineLeft);
            _notice.enableAutoSizing = true;
            _notice.fontSizeMin = NoticeMinSizePx;
            _notice.fontSizeMax = SmallTextSizePx;
            UiElements.Anchor(_notice.rectTransform, middleLeft, new Vector2(coinX, 0f), new Vector2(NoticeRightPx - coinX, HeaderButtonPx));
            _notice.gameObject.SetActive(false);
            _close = Button("Close", header, middleRight, new Vector2(-MarginPx, 0f), Vector2.one * HeaderButtonPx, _theme.PlateDark, ButtonRadiusPx, _theme.BoldFont, GlyphSizePx);
            _close.Text = "×";
            _pin = Button("Pin", header, middleRight, new Vector2(-2f * MarginPx - HeaderButtonPx, 0f), Vector2.one * HeaderButtonPx, _theme.Card, ButtonRadiusPx);
            _pinDot = UiElements.Dot("Dot", _pin.Rect, _theme.TextDim);
            UiElements.Anchor(_pinDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (HeaderButtonPx / 3f));
            _wide = Button("Wide", header, middleRight, new Vector2(-3f * MarginPx - 2f * HeaderButtonPx, 0f), new Vector2(WideButtonWidthPx, HeaderButtonPx), _theme.Card, ButtonRadiusPx, _theme.TextFont, SmallTextSizePx);
        }

        private void BuildHandle(RectTransform body)
        {
            _handle = Button("Handle", body, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(HandleWidthPx, HandleHeightPx), Color.clear, 0f);
            UiElements.IgnoreLayout(_handle.Rect);
            var bar = UiElements.Fill("Bar", _handle.Rect, _theme.Border, HandleBarHeightPx / 2f);
            UiElements.Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandleBarWidthPx, HandleBarHeightPx));
        }

        private void BuildSubTabs(RectTransform row)
        {
            row.gameObject.AddComponent<RectMask2D>();
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset((int)MarginPx, (int)MarginPx, 0, 0);
            line.spacing = MarginPx * 0.75f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = false;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            for (var i = 0; i < MaxSubTabs; i++)
                _subTabs.Add(Pill("SubTab " + i, row));
            var middleRight = new Vector2(1f, 0.5f);
            _preset = Button("Preset", row, middleRight, new Vector2(-MarginPx, 0f), new Vector2(PresetWidthPx, PillHeightPx), _theme.Accent, PillRadiusPx);
            UiElements.Stretch(UiElements.Fill("Inner", _preset.Rect, _theme.TabBar, PillRadiusPx - BorderPx).rectTransform, BorderPx);
            var presetLabel = UiElements.Label("PresetText", _preset.Rect, _theme.TextFont, SmallTextSizePx - 1f, _theme.Text, TextAlignmentOptions.Center);
            UiElements.Stretch(presetLabel.rectTransform);
            _preset = new UiButton(_preset.Back, presetLabel);
            UiElements.IgnoreLayout(_preset.Rect);
            _preset.Show(false);
            _slots = UiElements.Label("Slots", row, _theme.TextFont, SmallTextSizePx - 1f, _theme.TextDim, TextAlignmentOptions.MidlineRight);
            UiElements.Anchor(_slots.rectTransform, middleRight, new Vector2(-1.5f * MarginPx, 0f), new Vector2(PresetWidthPx, PillHeightPx));
            UiElements.IgnoreLayout(_slots.rectTransform);
            _slots.gameObject.SetActive(false);
        }

        private UiButton Pill(string name, RectTransform parent)
        {
            var back = UiElements.Fill(name, parent, _theme.Card, PillRadiusPx);
            back.rectTransform.sizeDelta = new Vector2(0f, PillHeightPx);
            var fit = back.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(PillPaddingPx, PillPaddingPx, 0, 0);
            fit.childControlWidth = true;
            fit.childControlHeight = true;
            fit.childForceExpandWidth = false;
            var label = UiElements.Label(name + "Text", back.rectTransform, _theme.TextFont, SmallTextSizePx, _theme.Text, TextAlignmentOptions.Center);
            return new UiButton(back, label);
        }

        private void BuildContent(RectTransform content)
        {
            content.gameObject.AddComponent<RectMask2D>();
            var list = UiElements.Stretch(UiElements.Rect("More", content));
            var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset((int)MarginPx, (int)MarginPx, (int)MarginPx, (int)MarginPx);
            column.spacing = MarginPx;
            column.childControlWidth = true;
            column.childControlHeight = false;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            for (var i = 0; i < MaxMoreRows; i++)
                _moreRows.Add(MoreRow("More " + i, list));
            _placeholder = UiElements.Label("Placeholder", content, _theme.TextFont, TextSizePx, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Stretch(_placeholder.rectTransform);
            _heroPanel = HeroPanel.Create(content, _theme);
            _heroPanel.TreeRequested += () => TabPressed?.Invoke(PanelSection.Tree);
            _itemsPanel = ItemsPanel.Create(content, _theme);
            _statsPanel = StatsPanel.Create(content, _theme);
            _masterPanel = MasterPanel.Create(content, _theme);
            _partyPanel = PartyPanel.Create(content, _theme);
            _partyPanel.NoticeRequested += ShowNotice;
            _partySearchPanel = PartySearchPanel.Create(content, _theme);
            Book = BookPanel.Create(content, _theme);
            Tree = TreePanel.Create(content, _theme);
            Subclass = SubclassPanel.Create(content, _theme);
            _settingsPanel = SettingsPanel.Create(content, _theme);
        }

        private UiButton MoreRow(string name, RectTransform parent)
        {
            var back = UiElements.Fill(name, parent, _theme.Card, ButtonRadiusPx);
            back.rectTransform.sizeDelta = new Vector2(0f, MoreRowHeightPx);
            var icon = UiElements.Icon(name + "Icon", back.rectTransform, null, _theme.Text);
            UiElements.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(MoreTextInsetPx, 0f), Vector2.one * MoreIconPx);
            var label = UiElements.Label(name + "Text", back.rectTransform, _theme.TextFont, TextSizePx, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Stretch(label.rectTransform).offsetMin = new Vector2(MoreTextInsetPx + MoreIconPx + MarginPx, 0f);
            var chevron = UiElements.Label("Chevron", back.rectTransform, _theme.BoldFont, GlyphSizePx, _theme.TextDim, TextAlignmentOptions.MidlineRight);
            UiElements.Stretch(chevron.rectTransform).offsetMax = new Vector2(-MoreTextInsetPx, 0f);
            chevron.text = "›";
            var row = new UiButton(back, label);
            var mark = SectionMark.ForRow(row, icon, chevron.gameObject, _theme);
            mark.Tint(_theme.Text);
            _moreMarks.Add(mark);
            return row;
        }

        private void BuildTabBar(RectTransform bar)
        {
            UiElements.Stretch(UiElements.Fill("Back", bar, _theme.TabBar, FrameRadiusPx - BorderPx).rectTransform);
            var square = UiElements.Fill("BackTop", bar, _theme.TabBar).rectTransform;
            UiElements.TopEdge(square, PanelGeometry.TabBarPx / 2f);
            UiElements.TopEdge(UiElements.Fill("Line", bar, _theme.Border).rectTransform, BorderPx);
            var tabs = UiElements.Stretch(UiElements.Rect("Tabs", bar));
            var line = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = true;
            line.childForceExpandHeight = true;
            for (var i = 0; i < TabCount; i++)
            {
                var back = UiElements.Fill("Tab " + i, tabs, Color.clear);
                var icon = UiElements.Icon("Tab " + i + "Icon", back.rectTransform, null, _theme.TextDim);
                UiElements.Anchor(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -TabIconTopPx), Vector2.one * TabIconPx);
                var label = UiElements.Label("Tab " + i + "Text", back.rectTransform, _theme.TextFont, SmallTextSizePx, _theme.TextDim, TextAlignmentOptions.Center);
                UiElements.BottomEdge(label.rectTransform, SectionMark.TabLabelHeightPx).anchoredPosition = new Vector2(0f, SectionMark.TabLabelBottomPx);
                var stripe = UiElements.Fill("Stripe", back.rectTransform, _theme.Gold, TabStripePx / 2f);
                UiElements.TopEdge(stripe.rectTransform, TabStripePx, TabStripeInsetPx);
                var tab = new UiButton(back, label);
                _tabs.Add(tab);
                _tabStripes.Add(stripe);
                _tabMarks.Add(SectionMark.ForTab(tab, icon, _theme));
            }
        }

        private RectTransform BuildReport()
        {
            var body = Shell("Report");
            var top = new Vector2(0.5f, 1f);
            var plate = Button("Title", body, top, new Vector2(0f, -MarginPx), new Vector2(ReportPlateWidthPx, HeaderButtonPx), _theme.Plate, ButtonRadiusPx, _theme.BoldFont, TitleSizePx);
            plate.Label!.color = Color.white;
            UiElements.BottomEdge(UiElements.Fill("Line", plate.Rect, _theme.Gold).rectTransform, PlateLinePx, ButtonRadiusPx);
            _reportTitle = plate.Label;
            var firstLineTop = PanelGeometry.HeaderPx + MarginPx;
            for (var i = 0; i < MaxReportLines; i++)
            {
                var line = UiElements.Label("Line " + i, body, _theme.TextFont, TextSizePx, _theme.Text, TextAlignmentOptions.MidlineLeft);
                UiElements.Anchor(line.rectTransform, new Vector2(0f, 1f), new Vector2(2f * MarginPx, -(firstLineTop + i * ReportLineHeightPx)), new Vector2(PanelGeometry.WidthPx - 4f * MarginPx, ReportLineHeightPx));
                _reportLines.Add(line);
            }
            _claim = Button("Claim", body, new Vector2(0.5f, 0f), new Vector2(0f, MarginPx), new Vector2(ClaimWidthPx, ClaimHeightPx), _theme.Success, ButtonRadiusPx, _theme.BoldFont, TitleSizePx);
            _claim.Label!.color = Color.white;
            return (RectTransform)body.parent;
        }

        private UiButton Button(string name, RectTransform parent, Vector2 anchor, Vector2 positionPx, Vector2 sizePx, Color color, float radiusPx, TMP_FontAsset? font = null, float fontSizePx = 0f)
        {
            var back = UiElements.Raise(UiElements.Fill(name, parent, color, radiusPx));
            UiElements.Anchor(back.rectTransform, anchor, positionPx, sizePx);
            if (fontSizePx <= 0f)
                return new UiButton(back, null);
            var label = UiElements.Label(name + "Text", back.rectTransform, font, fontSizePx, _theme.Text, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform);
            return new UiButton(back, label);
        }

        private TMP_FontAsset Font(TMP_FontAsset? preferred) => preferred != null ? preferred : TMP_Settings.defaultFontAsset;
    }
}
