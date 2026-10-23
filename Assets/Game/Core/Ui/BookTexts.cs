using Game.Core.Build;
using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public static class BookTexts
    {
        public const string Hint = "Перетащи гем в строку или обратно в сумку";
        public const string CompactHint = "Сумка гемов видна в полной высоте панели";
        public const string EmptyRow = "пусто";

        public static string Result(BookResult result) => result switch
        {
            BookResult.Done => string.Empty,
            BookResult.NotOwned => "Свободной копии этого гема нет",
            BookResult.WrongKind => "Сюда этот гем не встаёт",
            BookResult.NoFreeSlot => "Свободных слотов нет",
            BookResult.NoSuchSlot => "Здесь нет слота для этого гема",
            BookResult.AttributesTooLow => "Не хватает атрибутов",
            _ => string.Empty
        };

        public static string RowKey(int row) => row switch
        {
            0 => "ЛКМ",
            1 => "ПКМ",
            _ => "Авто"
        };

        public static string Locked(long unlockStage) => $"Откроется после стража этапа {unlockStage}";

        public static string Kind(GemKind kind) => kind switch
        {
            GemKind.Skill => "скилл",
            GemKind.Trigger => "триггер",
            _ => "поддержка"
        };

        public static string Short(string name) => name.Length <= 2 ? name : name.Substring(0, 2);

        public static string Filter(GemBagFilter filter) => filter switch
        {
            GemBagFilter.Skills => "Скиллы",
            GemBagFilter.Triggers => "Триггеры",
            GemBagFilter.Supports => "Поддержки",
            _ => "Все"
        };
    }
}
