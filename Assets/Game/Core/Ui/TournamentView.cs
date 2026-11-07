using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct TournamentRewardRow
    {
        public TournamentRewardRow(string places, string reward)
        {
            Places = places;
            Reward = reward;
        }

        public string Places { get; }
        public string Reward { get; }
    }

    public static class TournamentView
    {
        public static string FormatName(TournamentRules? rules, string formatId)
        {
            var format = rules?.Format(formatId);
            return format == null ? formatId : Texts.Pick(format.NameRu, format.NameEn);
        }

        public static string Score(TournamentFormat format, long score) => Texts.Format("tournament.score." + format.Id, score);

        public static string Rules(TournamentFormat format) => Texts.Get("tournament.rules." + format.Id);

        public static string Group(TournamentRules rules) => Texts.Format("tournament.group", rules.GroupSize);

        public static string Place(int place, int of) => Texts.Format("tournament.place", place, of);

        public static string JoinButton(TournamentFormat format, bool canJoin, bool joinOpen) =>
            !joinOpen ? Texts.Get("tournament.joinClosed")
            : !canJoin ? Texts.Get("tournament.prestigeNotReady")
            : Texts.Get(format.Prestige ? "tournament.joinPrestige" : "tournament.join");

        public static List<TournamentRewardRow> Rewards(TournamentRules rules, int band)
        {
            var rows = new List<TournamentRewardRow>(rules.Rewards.Count);
            var from = 1;
            foreach (var place in rules.Rewards)
            {
                var places = place.UpTo == 1 ? Texts.Get("tournament.firstPlace") : Texts.Format("tournament.places", from, place.UpTo);
                rows.Add(new TournamentRewardRow(places, RewardText(rules.RewardFor(place.UpTo, band))));
                from = place.UpTo + 1;
            }
            return rows;
        }

        private static string RewardText(Content.Rift.RiftReward reward)
        {
            var parts = new List<string>(2);
            if (reward.Ingots > 0)
                parts.Add(Texts.Format("chest.contents.ingots", reward.Ingots));
            if (reward.Essence > 0)
                parts.Add(Texts.Format("chest.contents.essence", reward.Essence));
            return string.Join(" · ", parts);
        }
    }
}
