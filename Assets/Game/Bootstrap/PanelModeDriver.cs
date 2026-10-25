using System;
using System.Collections.Generic;
using Game.Core.Ui;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class PanelModeDriver : MonoBehaviour
    {
        private TaskbarDockDriver _dock = null!;
        private PanelCanvas _panels = null!;
        private int _frameWidthPx = PanelGeometry.WidthPx;
        private string? _reportTitle;
        private IReadOnlyList<string>? _reportLines;
        private string? _reportAction;

        public PanelNavigator Navigator { get; private set; } = null!;
        public bool IsOpen => _panels.IsOpen;

        public static PanelModeDriver Attach(GameObject host, TaskbarDockDriver dock, PanelCanvas panels, StripButton menuButton)
        {
            var driver = host.AddComponent<PanelModeDriver>();
            driver._dock = dock;
            driver._panels = panels;
            driver.Navigator = new PanelNavigator();
            driver.Wire(menuButton);
            return driver;
        }

        private void Wire(StripButton menuButton)
        {
            var navigator = Navigator;
            menuButton.Clicked += Toggle;
            _panels.TabPressed += navigator.SelectTab;
            _panels.MorePressed += navigator.PressMore;
            _panels.ClosePressed += navigator.Close;
            _panels.PinPressed += navigator.TogglePin;
            _panels.HeightPressed += navigator.ToggleHeight;
            _panels.WidePressed += navigator.ToggleWide;
            _panels.SubTabPressed += navigator.SelectSubTab;
            _panels.ClaimPressed += Close;
            navigator.Changed += Apply;
            _dock.Placed += OnPlaced;
        }

        public void Toggle()
        {
            if (_panels.IsShowingCard)
                Close();
            else
                Navigator.PressMenu();
        }

        public void Refresh()
        {
            if (Navigator.IsOpen)
                _panels.Show(Navigator);
        }

        public void OpenReport(string title, IReadOnlyList<string> lines, string? action = null)
        {
            _reportTitle = title;
            _reportLines = lines;
            _reportAction = action;
            Navigator.Close();
            _panels.ShowReport(title, lines, action);
            Expand(PanelCanvas.ReportFrame);
        }

        public void OpenConsent()
        {
            if (!_panels.IsShowingReport)
                ForgetReport();
            Navigator.Close();
            _panels.ShowConsent();
            Expand(PanelCanvas.ConsentFrame);
        }

        public void FinishConsent()
        {
            if (_reportTitle != null && _reportLines != null)
                OpenReport(_reportTitle, _reportLines, _reportAction);
            else
                Close();
        }

        public void Close()
        {
            ForgetReport();
            if (Navigator.IsOpen)
            {
                Navigator.Close();
                return;
            }
            _panels.Hide();
            _dock.Collapse();
        }

        private void ForgetReport()
        {
            _reportTitle = null;
            _reportLines = null;
            _reportAction = null;
        }

        private void Apply()
        {
            if (!Navigator.IsOpen)
            {
                _panels.Hide();
                _dock.Collapse();
                return;
            }
            _panels.Show(Navigator);
            Expand(Navigator.Frame);
        }

        private void Expand(PanelFrame frame)
        {
            _frameWidthPx = frame.WidthPx;
            _dock.Expand();
            PlacePanel(_dock.Layout);
        }

        private void OnPlaced(PanelLayout layout, int stripHeightPx)
        {
            if (_panels.IsOpen)
                PlacePanel(layout);
        }

        private void PlacePanel(PanelLayout layout)
        {
            var scale = DpiScale;
            var left = layout.PanelLeft(Mathf.RoundToInt(_frameWidthPx * scale), _dock.StripWidthPx);
            _panels.Place(left / scale, layout.PanelsAboveStrip);
        }

        private float DpiScale => Math.Max(1, _dock.StripHeightPx) / StripSpace.BaseHeightPx;
    }
}
