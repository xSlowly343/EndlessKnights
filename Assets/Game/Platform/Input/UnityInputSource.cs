using Game.Core.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Platform.Windows.Input
{
    public sealed class UnityInputSource : MonoBehaviour, IInputSource
    {
        private const float WheelNotch = 120f;

        private readonly FrameInputAccumulator _accumulator = new FrameInputAccumulator();
        private float _wheelRemainder;

        public static UnityInputSource Attach(GameObject host) => host.AddComponent<UnityInputSource>();

        public InputSnapshot Drain() => _accumulator.Drain(Time.realtimeSinceStartupAsDouble);

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            ReadKeyboard(now);
            ReadMouse(now);
        }

        private void ReadKeyboard(double now)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            foreach (var key in keyboard.allKeys)
            {
                if (key != null && key.wasPressedThisFrame)
                    _accumulator.AddKey(Classify(key.keyCode), now);
            }
        }

        private void ReadMouse(double now)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            _accumulator.AddMouseDistance(mouse.delta.ReadValue().magnitude, now);
            _accumulator.AddClicks(
                Pressed(mouse.leftButton), Pressed(mouse.rightButton), Pressed(mouse.middleButton), now);

            _wheelRemainder += Mathf.Abs(mouse.scroll.ReadValue().y);
            var ticks = (int)(_wheelRemainder / WheelNotch);
            _wheelRemainder -= ticks * WheelNotch;
            _accumulator.AddWheelTicks(ticks, now);
        }

        private static int Pressed(ButtonControl button) => button.wasPressedThisFrame ? 1 : 0;

        private static KeyClass Classify(Key key)
        {
            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftMeta:
                case Key.RightMeta:
                    return KeyClass.None;
                case Key.Enter:
                case Key.NumpadEnter:
                    return KeyClass.Enter;
                case Key.Backspace:
                    return KeyClass.Backspace;
                case Key.Space:
                    return KeyClass.Space;
                default:
                    return KeyClass.Regular;
            }
        }
    }
}
