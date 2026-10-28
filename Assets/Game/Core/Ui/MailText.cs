using System.Collections.Generic;
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

        public static string Title(MailLetter letter) => Texts.Get(letter.Kind == MailLetter.RiftKind ? "mail.rift.title" : "mail.title");

        public static string Body(MailLetter letter) =>
            letter.Kind == MailLetter.RiftKind ? Texts.Format("mail.rift.body", letter.Place, letter.Of, letter.Depth, PartyCardText.Title(letter.Title)) : string.Empty;

        public static string Reward(MailLetter letter)
        {
            var parts = new List<string>();
            if (letter.Reward.Shards > 0)
                parts.Add(Texts.Format("rift.reward.shards", letter.Reward.Shards));
            if (letter.Reward.UncutGems > 0)
                parts.Add(Texts.Format("rift.reward.uncut", letter.Reward.UncutGems));
            if (letter.Reward.Ingots > 0)
                parts.Add(Texts.Format("rift.reward.ingots", letter.Reward.Ingots));
            return string.Join(" · ", parts);
        }

        public static string Arrived(MailLetter letter) => Texts.Format("mail.arrived", Title(letter));

        public static string Taken(MailLetter letter) => Texts.Format("mail.taken", Reward(letter));

        public static IReadOnlyList<MailRow> Rows(IReadOnlyList<MailLetter> letters)
        {
            var rows = new List<MailRow>(letters.Count);
            foreach (var letter in letters)
                rows.Add(new MailRow(letter.Id, Title(letter), Body(letter), Reward(letter), letter.Claimed));
            return rows;
        }
    }
}
