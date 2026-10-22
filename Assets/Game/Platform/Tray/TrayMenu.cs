using System.Collections.Generic;

namespace Game.Platform.Windows.Tray
{
    public readonly struct TrayMenuState
    {
        public const int DefaultFrameRate = 60;

        public static readonly TrayMenuState Default = new TrayMenuState(paused: false, clickThrough: true, alwaysOnTop: true, frameRate: DefaultFrameRate, showInputDebug: false);

        public readonly bool Paused;
        public readonly bool ClickThrough;
        public readonly bool AlwaysOnTop;
        public readonly int FrameRate;
        public readonly bool ShowInputDebug;

        public TrayMenuState(bool paused, bool clickThrough, bool alwaysOnTop, int frameRate, bool showInputDebug)
        {
            Paused = paused;
            ClickThrough = clickThrough;
            AlwaysOnTop = alwaysOnTop;
            FrameRate = frameRate;
            ShowInputDebug = showInputDebug;
        }

        public TrayMenuState Apply(TrayCommand command)
        {
            switch (command)
            {
                case TrayCommand.TogglePause:
                    return new TrayMenuState(!Paused, ClickThrough, AlwaysOnTop, FrameRate, ShowInputDebug);
                case TrayCommand.ToggleClickThrough:
                    return new TrayMenuState(Paused, !ClickThrough, AlwaysOnTop, FrameRate, ShowInputDebug);
                case TrayCommand.ToggleAlwaysOnTop:
                    return new TrayMenuState(Paused, ClickThrough, !AlwaysOnTop, FrameRate, ShowInputDebug);
                case TrayCommand.ToggleInputDebug:
                    return new TrayMenuState(Paused, ClickThrough, AlwaysOnTop, FrameRate, !ShowInputDebug);
                case TrayCommand.FrameRate30:
                case TrayCommand.FrameRate60:
                case TrayCommand.FrameRate144:
                    return new TrayMenuState(Paused, ClickThrough, AlwaysOnTop, (int)command, ShowInputDebug);
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
        private static readonly TrayCommand[] FrameRateCommands = { TrayCommand.FrameRate30, TrayCommand.FrameRate60, TrayCommand.FrameRate144 };

        public static IReadOnlyList<TrayMenuItem> Items(TrayMenuState state)
        {
            var items = new List<TrayMenuItem>
            {
                TrayMenuItem.Toggle(TrayCommand.TogglePause, "Пауза", state.Paused),
                TrayMenuItem.Toggle(TrayCommand.ToggleClickThrough, "Клики насквозь", state.ClickThrough),
                TrayMenuItem.Toggle(TrayCommand.ToggleAlwaysOnTop, "Поверх всех окон", state.AlwaysOnTop),
                TrayMenuItem.Toggle(TrayCommand.ToggleInputDebug, "Отладка ввода", state.ShowInputDebug),
                TrayMenuItem.Separator()
            };
            foreach (var command in FrameRateCommands)
                items.Add(TrayMenuItem.Toggle(command, $"Лимит {(int)command} FPS", state.FrameRate == (int)command));
            items.Add(TrayMenuItem.Separator());
            items.Add(TrayMenuItem.Action(TrayCommand.TogglePanel, "Меню игры"));
            items.Add(TrayMenuItem.Action(TrayCommand.Exit, "Выход"));
            return items;
        }
    }
}
