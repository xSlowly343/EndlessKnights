using System.Collections.Generic;
using Game.Core.Content.Rift;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class RiftBoardText
    {
        public static string Row(int place, string titleId, long depth, bool yours) =>
            Texts.Format(yours ? "rift.board.yourRow" : "rift.board.row", place, PartyCardText.Title(titleId), depth);

        public static string Own(int place, int parties, long depth) => Texts.Format("rift.board.own", place, parties, depth);

        public static string Reward(int place, int of, long depth, in RiftReward reward)
        {
            var parts = new List<string> { Texts.Format("rift.reward.place", place, of, depth) };
            if (reward.Shards > 0)
                parts.Add(Texts.Format("rift.reward.shards", reward.Shards));
            if (reward.UncutGems > 0)
                parts.Add(Texts.Format("rift.reward.uncut", reward.UncutGems));
            if (reward.Ingots > 0)
                parts.Add(Texts.Format("rift.reward.ingots", reward.Ingots));
            return string.Join(" · ", parts);
        }
    }
}
