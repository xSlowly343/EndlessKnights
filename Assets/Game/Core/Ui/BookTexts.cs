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
        public static string BagTitle => Texts.Get("book.bag");
        public static string AddTrigger => Texts.Get("book.addTrigger");
        public static string Unarmed => Texts.Get("book.unarmed");
        public static string Remove => Texts.Get("book.remove");

        public static string Removed(GemDef gem) => Texts.Format("book.removed", Name(gem));

        public static string Slots(int open, int max) => Texts.Format("book.slots", open, max);

        public static string Name(GemDef gem) => Texts.Pick(gem.NameRu, gem.NameEn);

        public static string Describe(GemDef gem, int freeCopies) =>
            freeCopies > 0
                ? Texts.Format("book.describeWithCopies", Name(gem), Kind(gem.Kind), freeCopies)
                : Texts.Format("book.describe", Name(gem), Kind(gem.Kind));

        public static string Result(BookResult result) => result switch
        {
            BookResult.Done => string.Empty,
            BookResult.NotOwned => Texts.Get("book.result.notOwned"),
            BookResult.WrongKind => Texts.Get("book.result.wrongKind"),
            BookResult.NoFreeSlot => Texts.Get("book.result.noFreeSlot"),
            BookResult.NoSuchSlot => Texts.Get("book.result.noSuchSlot"),
            BookResult.AttributesTooLow => Texts.Get("book.result.attributesTooLow"),
            BookResult.WrongPath => Texts.Get("book.result.wrongPath"),
            BookResult.WaitsForMechanic => Texts.Get("book.result.waitsForMechanic"),
            BookResult.NotOnWeapon => Texts.Get("book.result.notOnWeapon"),
            BookResult.AlreadyInBook => Texts.Get("book.result.alreadyInBook"),
            BookResult.NoSuchSocket => Texts.Get("book.result.noSuchSocket"),
            BookResult.AlreadyInBuild => Texts.Get("book.result.alreadyInBuild"),
            BookResult.TooManyColored => Texts.Get("book.result.tooManyColored"),
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
