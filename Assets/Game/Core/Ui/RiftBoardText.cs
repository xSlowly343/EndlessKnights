using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class RiftBoardText
    {
        public static string Row(int place, string titleId, long depth, bool yours) =>
            Texts.Format(yours ? "rift.board.yourRow" : "rift.board.row", place, PartyCardText.Title(titleId), depth);

        public static string Own(int place, int parties, long depth) => Texts.Format("rift.board.own", place, parties, depth);
    }
}
