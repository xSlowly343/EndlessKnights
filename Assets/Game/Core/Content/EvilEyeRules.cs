namespace Game.Core.Content
{
    public sealed class EvilEyeRules
    {
        public static readonly EvilEyeRules None = new EvilEyeRules(0);

        public EvilEyeRules(double damageTaken)
        {
            DamageTaken = damageTaken;
        }

        public double DamageTaken { get; }

        public static EvilEyeRules Read(ContentNode node) => new EvilEyeRules(node.Field("damageTaken").AsNumber(0, 10));
    }
}
