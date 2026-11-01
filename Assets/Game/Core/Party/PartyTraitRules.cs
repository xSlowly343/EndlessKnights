using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyTraitRules
    {
        public static readonly PartyTraitRules Default = new PartyTraitRules();

        public double PullMaxSeconds { get; private set; } = 10;
        public double PullStepSeconds { get; private set; } = 1;
        public double PullCooldownSeconds { get; private set; } = 1;
        public int SpiritCallsMax { get; private set; } = 4;
        public double SpiritHpMax { get; private set; } = 20;

        public static PartyTraitRules Read(ContentNode node) => new PartyTraitRules
        {
            PullMaxSeconds = node.Field("pullMaxSeconds").AsNumber(0, 600),
            PullStepSeconds = node.Field("pullStepSeconds").AsNumber(0, 60),
            PullCooldownSeconds = node.Field("pullCooldownSeconds").AsNumber(0, 60),
            SpiritCallsMax = node.Field("spiritCallsMax").AsInt(0, 100),
            SpiritHpMax = node.Field("spiritHpMax").AsNumber(1, 100)
        };
    }
}
