using System.Collections.Generic;

namespace Game.Core.Ui
{
    public static class ConsentTexts
    {
        public const string Title = "Что игра считает и что нет";
        public const string Accept = "Понятно, начать";
        public const string Decline = "Не сейчас";
        public const string Declined = "Ввод не считается, пока ты не согласишься. Экран согласия есть в настройках.";

        public static readonly IReadOnlyList<string> Counted = new[]
        {
            "Сколько раз нажаты клавиши, без того, какие именно",
            "Отдельно только Enter, Backspace и пробел",
            "Путь мыши в пикселях, взмахи и круги",
            "Клики левой, правой и средней кнопкой и щелчки колёсика",
            "Имя активной программы, только чтобы сверить с чёрным списком, без сохранения"
        };

        public static readonly IReadOnlyList<string> NotCounted = new[]
        {
            "Коды клавиш и набранный текст: игра их не видит и не хранит",
            "Заголовки окон, адреса сайтов и содержимое экрана",
            "Ввод в приложениях из чёрного списка и при паузе"
        };

        public const string NetworkNote = "На диск и в сеть уходят только счётчики. Что именно считать, можно выбрать в настройках.";
    }
}
