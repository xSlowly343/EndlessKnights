using System.Collections.Generic;
using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class PanelModeDriver : MonoBehaviour
    {
        private TaskbarDockDriver _dock = null!;
        private PanelView _panels = null!;

        public bool IsOpen => _panels.IsOpen;

        public static PanelModeDriver Attach(GameObject host, TaskbarDockDriver dock, PanelView panels, StripButton menuButton)
        {
            var driver = host.AddComponent<PanelModeDriver>();
            driver._dock = dock;
            driver._panels = panels;
            menuButton.Clicked += driver.Toggle;
            panels.CloseRequested += driver.Close;
            return driver;
        }

        public void Toggle()
        {
            if (_panels.IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            var pixelsPerUnitScale = _dock.StripHeightPx / StripSpace.BaseHeightPx;
            var layout = _dock.Expand((int)(PanelView.AreaWidthPx * pixelsPerUnitScale), (int)(PanelView.AreaHeightPx * pixelsPerUnitScale));
            _panels.Show(layout.PanelsAboveStrip);
        }

        public void OpenReport(string title, IReadOnlyList<string> lines)
        {
            var pixelsPerUnitScale = _dock.StripHeightPx / StripSpace.BaseHeightPx;
            var layout = _dock.Expand((int)(PanelView.ReportAreaWidthPx * pixelsPerUnitScale), (int)(PanelView.ReportAreaHeightPx * pixelsPerUnitScale));
            _panels.ShowReport(title, lines, layout.PanelsAboveStrip);
        }

        public void Close()
        {
            _panels.Hide();
            _dock.Collapse();
        }
    }
}
