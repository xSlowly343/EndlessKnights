using Game.Platform.Windows.Window;
using UnityEngine;

namespace Game.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PointerInput : MonoBehaviour
    {
        private readonly PointerEdges _edges = new PointerEdges();

        public bool Pressed => _edges.Pressed;
        public bool Held => _edges.Held;
        public bool Released => _edges.Released;
        public int X { get; private set; }
        public int Y { get; private set; }

        public static PointerInput Attach(GameObject host) => host.AddComponent<PointerInput>();

        private void Update()
        {
            var (down, pressedSinceLastPoll) = PointerState.ReadPrimaryButton();
            _edges.Update(down, pressedSinceLastPoll);
            if (ScreenCursor.TryGetPosition(out var x, out var y))
            {
                X = x;
                Y = y;
            }
        }
    }
}
