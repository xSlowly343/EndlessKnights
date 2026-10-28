using Game.Core.Content.Rift;

namespace Game.Core.Mail
{
    public sealed class MailLetter
    {
        public const string RiftKind = "rift";

        public MailLetter(string id, string kind, long atUnixMs, int place, int of, long depth, string title, RiftReward reward, bool claimed)
        {
            Id = id;
            Kind = kind;
            AtUnixMs = atUnixMs;
            Place = place;
            Of = of;
            Depth = depth;
            Title = title;
            Reward = reward;
            Claimed = claimed;
        }

        public string Id { get; }
        public string Kind { get; }
        public long AtUnixMs { get; }
        public int Place { get; }
        public int Of { get; }
        public long Depth { get; }
        public string Title { get; }
        public RiftReward Reward { get; }
        public bool Claimed { get; private set; }

        public void MarkClaimed() => Claimed = true;
    }
}
