using Game.Core.Ui;
using Game.Platform.Windows.Window;
using Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    public sealed class CouponEntryDriver : MonoBehaviour
    {
        private PanelCanvas _panels = null!;
        private WindowController? _window;
        private Keyboard? _keyboard;

        public static CouponEntryDriver Attach(GameObject host, PanelCanvas panels, WindowController? window)
        {
            var driver = host.AddComponent<CouponEntryDriver>();
            driver._panels = panels;
            driver._window = window;
            return driver;
        }

        private bool Listening =>
            _panels.IsOpen && _panels.Settings.IsShown && _panels.Settings.Tab == SettingsScreen.AboutTab && (_window == null || _window.IsForeground);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == _keyboard)
                return;
            if (_keyboard != null)
                _keyboard.onTextInput -= OnText;
            _keyboard = keyboard;
            if (_keyboard != null)
                _keyboard.onTextInput += OnText;
        }

        private void OnDestroy()
        {
            if (_keyboard != null)
                _keyboard.onTextInput -= OnText;
        }

        private void OnText(char symbol)
        {
            if (!Listening)
                return;
            var entry = SettingsScreen.CouponCode;
            if (CouponEntry.IsSubmit(symbol))
            {
                if (entry.Ready)
                    _panels.Settings.Press(SettingId.Coupon);
                return;
            }
            var changed = symbol == CouponEntry.PasteKey ? entry.Paste(GUIUtility.systemCopyBuffer) : entry.Type(symbol);
            if (changed)
                _panels.Settings.Refresh();
        }
    }
}
