using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyGiftRules
    {
        private PartyGiftRules(int dailyLimit, int perFriendDaily, int receiveDailyLimit, int friendshipCost)
        {
            DailyLimit = dailyLimit;
            PerFriendDaily = perFriendDaily;
            ReceiveDailyLimit = receiveDailyLimit;
            FriendshipCost = friendshipCost;
        }

        public int DailyLimit { get; }
        public int PerFriendDaily { get; }
        public int ReceiveDailyLimit { get; }
        public int FriendshipCost { get; }

        public static PartyGiftRules Read(ContentNode node)
        {
            var daily = node.Field("dailyLimit").AsInt(1, 100);
            var perFriend = node.Field("perFriendDaily");
            if (perFriend.AsInt(1, 100) > daily)
                throw perFriend.Error($"must not be above dailyLimit ({daily})");
            return new PartyGiftRules(daily, perFriend.AsInt(1, 100), node.Field("receiveDailyLimit").AsInt(1, 100), node.Field("friendshipCost").AsInt(0, 10000));
        }
    }
}
