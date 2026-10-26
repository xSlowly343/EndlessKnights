using Game.Platform.Windows.Window;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class StripCameraDriver : MonoBehaviour
    {
        private Camera _camera = null!;

        public static void Attach(GameObject host, Camera camera, TaskbarDockDriver dock)
        {
            var driver = host.AddComponent<StripCameraDriver>();
            driver._camera = camera;
            dock.Placed += driver.Fit;
            driver.Fit(dock.Layout, dock.StripHeightPx);
        }

        private void Fit(PanelLayout layout, int stripHeightPx)
        {
            StripCamera.Fit(_camera, layout.Window.Width, layout.Window.Height, stripHeightPx, layout.StripOffsetX, layout.StripOffsetFromBottom);
        }
    }
}
