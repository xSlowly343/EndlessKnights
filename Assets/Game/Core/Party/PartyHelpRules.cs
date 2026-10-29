using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyHelpRules
    {
        public const string AcceptAll = "all";
        public const string AcceptFriends = "friends";
        public const string AcceptNone = "none";

        private static readonly string[] Choices = { AcceptAll, AcceptFriends, AcceptNone };

        private PartyHelpRules(double seconds, double answerSeconds, double callCooldownSeconds, string defaultAccept)
        {
            Seconds = seconds;
            AnswerSeconds = answerSeconds;
            CallCooldownSeconds = callCooldownSeconds;
            DefaultAccept = defaultAccept;
        }

        public double Seconds { get; }
        public double AnswerSeconds { get; }
        public double CallCooldownSeconds { get; }
        public string DefaultAccept { get; }
        public static IReadOnlyList<string> AcceptChoices => Choices;

        public static PartyHelpRules Read(ContentNode node)
        {
            var accept = node.Field("defaultAccept");
            var value = accept.AsString();
            if (System.Array.IndexOf(Choices, value) < 0)
                throw accept.Error("must be all, friends or none");
            return new PartyHelpRules(
                node.Field("seconds").AsNumber(5, 300),
                node.Field("answerSeconds").AsNumber(10, 600),
                node.Field("callCooldownSeconds").AsNumber(0, 3600),
                value);
        }
    }
}
