using System.Collections.Generic;
using Game.Core.Content.Gems;
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

    public static class MailText
    {
        public static string Empty => Texts.Get("mail.empty");

        public static string Title(MailLetter letter) => Texts.Get(letter.Kind switch
        {
            MailLetter.RiftKind => "mail.rift.title",
            MailLetter.MentorKind => "mail.mentor.title",
            MailLetter.GiftKind => "mail.gift.title",
            MailLetter.RiftDepthKind => "mail.riftDepth.title",
            _ => "mail.title"
        });

        public static string Body(MailLetter letter) => letter.Kind switch
        {
            MailLetter.RiftKind => Texts.Format("mail.rift.body", letter.Place, letter.Of, letter.Depth, PartyCardText.Title(letter.Title)),
            MailLetter.MentorKind => Texts.Format("mail.mentor.body", letter.Depth),
            MailLetter.GiftKind => Texts.Format("mail.gift.body", letter.Title),
            MailLetter.RiftDepthKind => Texts.Format("mail.riftDepth.body", letter.Depth, PartyCardText.Title(letter.Title)),
            _ => string.Empty
        };

        public static string Reward(MailLetter letter, GemCatalog? gems = null)
        {
            var parts = new List<string>();
            if ((letter.Kind == MailLetter.GiftKind || letter.Kind == MailLetter.RiftDepthKind) && letter.Gem.Length > 0)
                parts.Add(Texts.Format("mail.gift.gem", gems != null && gems.TryGet(letter.Gem, out var gem) ? BookTexts.Name(gem) : letter.Gem));
            if (letter.Reward.Shards > 0)
                parts.Add(Texts.Format("rift.reward.shards", letter.Reward.Shards));
            if (letter.Reward.UncutGems > 0)
                parts.Add(Texts.Format("rift.reward.uncut", letter.Reward.UncutGems));
            if (letter.Reward.Ingots > 0)
                parts.Add(Texts.Format("rift.reward.ingots", letter.Reward.Ingots));
            if (letter.Reward.Friendship > 0)
                parts.Add(Texts.Format("rift.reward.friendship", letter.Reward.Friendship));
            return string.Join(" · ", parts);
        }

        public static string Arrived(MailLetter letter) => Texts.Format("mail.arrived", Title(letter));

        public static string Taken(MailLetter letter, GemCatalog? gems = null) => Texts.Format("mail.taken", Reward(letter, gems));

        public static IReadOnlyList<MailRow> Rows(IReadOnlyList<MailLetter> letters, GemCatalog? gems = null)
        {
            var rows = new List<MailRow>(letters.Count);
            foreach (var letter in letters)
                rows.Add(new MailRow(letter.Id, Title(letter), Body(letter), Reward(letter, gems), letter.Claimed));
            return rows;
        }
    }
}
