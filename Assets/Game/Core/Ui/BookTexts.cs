using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class BookTexts
    {
        public static string Hint => Texts.Get("book.hint");
        public static string CompactHint => Texts.Get("book.compactHint");
        public static string EmptyRow => Texts.Get("book.emptyRow");

        public static string Result(BookResult result) => result switch
        {
            BookResult.Done => string.Empty,
            BookResult.NotOwned => Texts.Get("book.result.notOwned"),
            BookResult.WrongKind => Texts.Get("book.result.wrongKind"),
            BookResult.NoFreeSlot => Texts.Get("book.result.noFreeSlot"),
            BookResult.NoSuchSlot => Texts.Get("book.result.noSuchSlot"),
            BookResult.AttributesTooLow => Texts.Get("book.result.attributesTooLow"),
            _ => string.Empty
        };

        public static string RowKey(int row) => row switch
        {
            0 => Texts.Get("book.row.left"),
            1 => Texts.Get("book.row.right"),
            _ => Texts.Get("book.row.auto")
        };

        public static string Locked(long unlockStage) => Texts.Format("book.locked", unlockStage);

        public static string Kind(GemKind kind) => kind switch
        {
            GemKind.Skill => Texts.Get("book.kind.skill"),
            GemKind.Trigger => Texts.Get("book.kind.trigger"),
            _ => Texts.Get("book.kind.support")
        };

        public static string Short(string name) => name.Length <= 2 ? name : name.Substring(0, 2);

        public static string Filter(GemBagFilter filter) => filter switch
        {
            GemBagFilter.Skills => Texts.Get("book.filter.skills"),
            GemBagFilter.Triggers => Texts.Get("book.filter.triggers"),
            GemBagFilter.Supports => Texts.Get("book.filter.supports"),
            _ => Texts.Get("book.filter.all")
        };
    }
}
