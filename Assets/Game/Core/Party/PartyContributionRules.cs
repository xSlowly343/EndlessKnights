using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyContributionRules
    {
        private PartyContributionRules(double minShare, double activeInputSeconds, double restBankSeconds, double idlePortionShare, double typingReserveShare, double allyCreditMax)
        {
            MinShare = minShare;
            ActiveInputSeconds = activeInputSeconds;
            RestBankSeconds = restBankSeconds;
            IdlePortionShare = idlePortionShare;
            TypingReserveShare = typingReserveShare;
            AllyCreditMax = allyCreditMax;
        }

        public double MinShare { get; }
        public double ActiveInputSeconds { get; }
        public double RestBankSeconds { get; }
        public double IdlePortionShare { get; }
        public double TypingReserveShare { get; }
        public double AllyCreditMax { get; }

        public static PartyContributionRules Read(ContentNode node, double restAfterSeconds)
        {
            var activeInput = node.Field("activeInputSeconds").AsNumber(1, 3600);
            if (activeInput > restAfterSeconds)
                throw node.Field("activeInputSeconds").Error($"must not be longer than presence.restAfterSeconds ({restAfterSeconds})");
            return new PartyContributionRules(
                node.Field("minShare").AsNumber(0, 1),
                activeInput,
                node.Field("restBankMinutes").AsNumber(0, 1440) * 60,
                node.Field("idlePortionShare").AsNumber(1, 100),
                node.Field("typingReserveShare").AsNumber(0, 1),
                node.Field("allyCreditMax").AsNumber(0, 1));
        }
    }
}
