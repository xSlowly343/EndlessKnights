using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Mail;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct MailRow
    {
        public MailRow(string id, string title, string body, string reward, bool claimed)
        {
            Id = id;
            Title = title;
            Body = body;
            Reward = reward;
            Claimed = claimed;
        }

        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public string Reward { get; }
        public bool Claimed { get; }
        public string Button => Texts.Get(Claimed ? "mail.claimed" : "mail.claim");
    }

    public readonly struct MailPage
    {
        public MailPage(int index, int pages, int first, int count)
        {
            Index = index;
            Pages = pages;
            First = first;
            Count = count;
        }

        public int Index { get; }
        public int Pages { get; }
        public int First { get; }
        public int Count { get; }
        public bool HasPrevious => Index > 0;
        public bool HasNext => Index < Pages - 1;
        public string Label => Texts.Format("mail.page", Index + 1, Pages);
    }

    public static class MailText
    {
        public static MailPage Page(int letters, int perPage, int wanted)
        {
            var size = System.Math.Max(1, perPage);
            var pages = System.Math.Max(1, (System.Math.Max(0, letters) + size - 1) / size);
            var index = System.Math.Clamp(wanted, 0, pages - 1);
            var first = index * size;
            return new MailPage(index, pages, first, System.Math.Clamp(letters - first, 0, size));
        }

        public static string Empty => Texts.Get("mail.empty");

        public static string Title(MailLetter letter) => Texts.Get(letter.Kind switch
        {
            MailLetter.RiftKind => "mail.rift.title",
            MailLetter.MentorKind => "mail.mentor.title",
            MailLetter.RiftDepthKind => "mail.riftDepth.title",
            MailLetter.ChestKind => "mail.chest.title",
            MailLetter.TournamentKind => "mail.tournament.title",
            MailLetter.FairKind => "mail.fair.title",
            MailLetter.CouponKind => "mail.coupon.title",
            MailLetter.TownKind => "mail.town.title",
            _ => "mail.title"
        });

        public static string Body(MailLetter letter, ChestCatalog? chests = null, TournamentRules? tournaments = null, FairRules? fair = null, GuildRules? guilds = null) => letter.Kind switch
        {
            MailLetter.TownKind => letter.Place > 0 ? Texts.Format("mail.town.bodyTop", HallName(letter.Title, guilds), letter.Depth, letter.Place)
                : Texts.Format("mail.town.body", HallName(letter.Title, guilds), letter.Depth),
            MailLetter.RiftKind => Texts.Format("mail.rift.body", letter.Place, letter.Of, letter.Depth, PartyCardText.Title(letter.Title)),
            MailLetter.MentorKind => Texts.Format("mail.mentor.body", letter.Depth),
            MailLetter.RiftDepthKind => Texts.Format("mail.riftDepth.body", letter.Depth, PartyCardText.Title(letter.Title)),
            MailLetter.ChestKind => Texts.Format("mail.chest.body", ChestName(letter.Title, chests)),
            MailLetter.TournamentKind => Texts.Format("mail.tournament.body", TournamentView.FormatName(tournaments, letter.Title), letter.Place, letter.Of, letter.Depth),
            MailLetter.FairKind => Texts.Format("mail.fair.body", ContestName(letter.Title, fair), letter.Place, letter.Of, letter.Depth),
            MailLetter.CouponKind => Texts.Format("mail.coupon.body", letter.Title),
            _ => string.Empty
        };

        public static string Reward(MailLetter letter)
        {
            var parts = new List<string>();
            if (letter.Reward.Shards > 0)
                parts.Add(Texts.Format("rift.reward.shards", letter.Reward.Shards));
            if (letter.Reward.Ingots > 0)
                parts.Add(Texts.Format("rift.reward.ingots", letter.Reward.Ingots));
            if (letter.Reward.Essence > 0)
                parts.Add(Texts.Format("rift.reward.essence", letter.Reward.Essence));
            if (letter.Reward.Friendship > 0)
                parts.Add(Texts.Format("rift.reward.friendship", letter.Reward.Friendship));
            return string.Join(" · ", parts);
        }

        private static string HallName(string id, GuildRules? guilds) => guilds == null ? id : GuildView.ProjectName(guilds, id);

        private static string ContestName(string id, FairRules? fair) => fair?.Contest(id) is FairContest contest ? Texts.Pick(contest.NameRu, contest.NameEn) : id;

        private static string ChestName(string id, ChestCatalog? chests) => chests != null && chests.TryGet(id, out var chest) ? Texts.Pick(chest.NameRu, chest.NameEn) : id;

        public static string Arrived(MailLetter letter) => Texts.Format("mail.arrived", Title(letter));

        public static string Taken(MailLetter letter) => Texts.Format("mail.taken", Reward(letter));

        public static IReadOnlyList<MailRow> Rows(IReadOnlyList<MailLetter> letters, ChestCatalog? chests = null, TournamentRules? tournaments = null)
        {
            var rows = new List<MailRow>(letters.Count);
            foreach (var letter in letters)
                rows.Add(new MailRow(letter.Id, Title(letter), Body(letter, chests, tournaments), Reward(letter), letter.Claimed));
            return rows;
        }
    }
}
