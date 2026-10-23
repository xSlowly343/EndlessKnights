using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class PanelNavigator
    {
        public const int BarSize = 5;

        private readonly PanelSection[] _bar;
        private readonly Dictionary<PanelSection, PanelHeight> _heights = new Dictionary<PanelSection, PanelHeight>();
        private readonly Dictionary<PanelSection, int> _subTabs = new Dictionary<PanelSection, int>();
        private bool _wide;

        public PanelNavigator() : this(PanelSections.DefaultBar)
        {
        }

        public PanelNavigator(IReadOnlyList<PanelSection> bar)
        {
            if (bar.Count != BarSize || bar.Distinct().Count() != BarSize)
                throw new ArgumentException($"The tab bar needs {BarSize} different sections.", nameof(bar));
            _bar = bar.ToArray();
            Active = _bar[0];
        }

        public event Action? Changed;

        public bool IsOpen { get; private set; }
        public bool ShowingMore { get; private set; }
        public bool IsPinned { get; private set; }
        public PanelSection Active { get; private set; }
        public IReadOnlyList<PanelSection> Bar => _bar;
        public IReadOnlyList<PanelSection> More => PanelSections.All.Where(section => Array.IndexOf(_bar, section) < 0).ToArray();
        public bool IsWide => _wide && !ShowingMore && PanelSections.CanBeWide(Active);
        public PanelHeight Height => IsWide ? PanelHeight.Full : HeightOf(Active);
        public int SubTab => _subTabs.TryGetValue(Active, out var index) ? index : 0;
        public IReadOnlyList<string> SubTabs => ShowingMore ? Array.Empty<string>() : PanelSections.SubTabs(Active);
        public string Title => ShowingMore ? Texts.Get("panel.more") : PanelSections.Title(Active);
        public PanelFrame Frame => PanelGeometry.Frame(Height, IsWide, SubTabs.Count > 0);

        public PanelHeight HeightOf(PanelSection section) =>
            _heights.TryGetValue(section, out var height) ? height : PanelSections.DefaultHeight(section);

        public void PressMenu()
        {
            if (IsOpen)
                Close();
            else
                Open(Active, false);
        }

        public void SelectTab(PanelSection section)
        {
            if (IsOpen && !ShowingMore && Active == section)
                Close();
            else
                Open(section, false);
        }

        public void PressMore()
        {
            if (IsOpen && ShowingMore)
                Close();
            else
                Open(Active, true);
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            ShowingMore = false;
            Changed?.Invoke();
        }

        public void ClickOutside()
        {
            if (!IsPinned)
                Close();
        }

        public void TogglePin()
        {
            IsPinned = !IsPinned;
            Changed?.Invoke();
        }

        public void ToggleHeight()
        {
            if (!IsOpen || ShowingMore || IsWide)
                return;
            _heights[Active] = HeightOf(Active) == PanelHeight.Full ? PanelHeight.Half : PanelHeight.Full;
            Changed?.Invoke();
        }

        public void ToggleWide()
        {
            if (!IsOpen || ShowingMore || !PanelSections.CanBeWide(Active))
                return;
            _wide = !_wide;
            Changed?.Invoke();
        }

        public void SelectSubTab(int index)
        {
            if (index < 0 || index >= SubTabs.Count || index == SubTab)
                return;
            _subTabs[Active] = index;
            Changed?.Invoke();
        }

        public void Scroll(int steps)
        {
            if (!IsOpen || steps == 0)
                return;
            var current = Array.IndexOf(_bar, Active);
            var start = current < 0 ? 0 : current;
            var next = ((start + steps) % BarSize + BarSize) % BarSize;
            Open(_bar[next], false);
        }

        public void PlaceOnBar(int slot, PanelSection section)
        {
            if (slot < 0 || slot >= BarSize)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            var existing = Array.IndexOf(_bar, section);
            if (existing >= 0)
                _bar[existing] = _bar[slot];
            _bar[slot] = section;
            Changed?.Invoke();
        }

        private void Open(PanelSection section, bool more)
        {
            IsOpen = true;
            Active = section;
            ShowingMore = more;
            Changed?.Invoke();
        }
    }
}
