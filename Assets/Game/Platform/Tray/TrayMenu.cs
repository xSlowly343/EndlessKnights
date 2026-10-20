using System.Collections.Generic;

namespace Game.Platform.Windows.Tray
{
    public readonly struct TrayMenuState
    {
        public static readonly TrayMenuState Default = new TrayMenuState(paused: false, clickThrough: true, alwaysOnTop: true);

        public readonly bool Paused;
        public readonly bool ClickThrough;
        public readonly bool AlwaysOnTop;

        public TrayMenuState(bool paused, bool clickThrough, bool alwaysOnTop)
        {
            Paused = paused;
            ClickThrough = clickThrough;
            AlwaysOnTop = alwaysOnTop;
        }

        public TrayMenuState Apply(TrayCommand command)
        {
            switch (command)
            {
                case TrayCommand.TogglePause:
                    return new TrayMenuState(!Paused, ClickThrough, AlwaysOnTop);
                case TrayCommand.ToggleClickThrough:
                    return new TrayMenuState(Paused, !ClickThrough, AlwaysOnTop);
                case TrayCommand.ToggleAlwaysOnTop:
                    return new TrayMenuState(Paused, ClickThrough, !AlwaysOnTop);
                default:
                    return this;
            }
        }
    }

    public readonly struct TrayMenuItem
    {
        public readonly TrayCommand Command;
        public readonly string Text;
        public readonly bool IsChecked;
        public readonly bool IsSeparator;

        private TrayMenuItem(TrayCommand command, string text, bool isChecked, bool isSeparator)
        {
            Command = command;
            Text = text;
            IsChecked = isChecked;
            IsSeparator = isSeparator;
        }

        public static TrayMenuItem Toggle(TrayCommand command, string text, bool isChecked) => new TrayMenuItem(command, text, isChecked, false);
        public static TrayMenuItem Action(TrayCommand command, string text) => new TrayMenuItem(command, text, false, false);
        public static TrayMenuItem Separator() => new TrayMenuItem(default, string.Empty, false, true);
    }

    public static class TrayMenu
    {
        public static IReadOnlyList<TrayMenuItem> Items(TrayMenuState state) => new[]
        {
            TrayMenuItem.Toggle(TrayCommand.TogglePause, "Пауза", state.Paused),
            TrayMenuItem.Toggle(TrayCommand.ToggleClickThrough, "Клики насквозь", state.ClickThrough),
            TrayMenuItem.Toggle(TrayCommand.ToggleAlwaysOnTop, "Поверх всех окон", state.AlwaysOnTop),
            TrayMenuItem.Separator(),
            TrayMenuItem.Action(TrayCommand.Exit, "Выход")
        };
    }
}
