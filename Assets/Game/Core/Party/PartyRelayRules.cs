using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyRelayRules
    {
        private PartyRelayRules(double share, double maxBonus, double seconds, double senderSeconds, double recipientSeconds, double cooldownSeconds)
        {
            Share = share;
            MaxBonus = maxBonus;
            Seconds = seconds;
            SenderSeconds = senderSeconds;
            RecipientSeconds = recipientSeconds;
            CooldownSeconds = cooldownSeconds;
        }

        public double Share { get; }
        public double MaxBonus { get; }
        public double Seconds { get; }
        public double SenderSeconds { get; }
        public double RecipientSeconds { get; }
        public double CooldownSeconds { get; }

        public double BonusFor(double dropped) => dropped > 0 && !double.IsInfinity(dropped) ? System.Math.Min(MaxBonus, dropped * Share) : 0;

        public static PartyRelayRules Read(ContentNode node) => new PartyRelayRules(
            node.Field("share").AsNumber(0, 1),
            node.Field("maxBonus").AsNumber(0, 10),
            node.Field("seconds").AsNumber(1, 600),
            node.Field("senderSeconds").AsNumber(0.5, 60),
            node.Field("recipientSeconds").AsNumber(0.5, 60),
            node.Field("cooldownSeconds").AsNumber(0, 600));
    }
}
