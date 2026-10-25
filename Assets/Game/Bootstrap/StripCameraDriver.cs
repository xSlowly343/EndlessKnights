using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class StripCameraDriver : MonoBehaviour
    {
        private readonly SurfaceResizeGate _gate = new SurfaceResizeGate();
        private Camera _camera = null!;
        private int _stripHeightPx;

        public static void Attach(GameObject host, Camera camera, TaskbarDockDriver dock)
        {
            var driver = host.AddComponent<StripCameraDriver>();
            driver._camera = camera;
            dock.Placed += driver.Request;
            driver.Request(dock.Layout, dock.StripHeightPx);
        }

        private void Request(PanelLayout layout, int stripHeightPx)
        {
            _stripHeightPx = stripHeightPx;
            _gate.Request(layout);
            FitWhenSurfaceMatches();
        }

        private void LateUpdate() => FitWhenSurfaceMatches();

        private void FitWhenSurfaceMatches()
        {
            if (_gate.TryTake(Screen.width, Screen.height, out var layout))
                StripCamera.Fit(_camera, layout.Window.Width, layout.Window.Height, _stripHeightPx, layout.StripOffsetX, layout.StripOffsetFromBottom);
        }
    }
}
