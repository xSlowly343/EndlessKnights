namespace Game.Core.Content.Items
{
    public sealed class RerollRules
    {
        public RerollRules(double dustPerSalvage, double choice, double lockFactor)
        {
            DustPerSalvage = dustPerSalvage;
            Choice = choice;
            Lock = lockFactor;
        }

        public double DustPerSalvage { get; }
        public double Choice { get; }
        public double Lock { get; }

        public double Cost(double salvageDust, bool locked) => System.Math.Ceiling(salvageDust * DustPerSalvage * Choice * (locked ? Lock : 1) - 1e-9);

        public static RerollRules Read(ContentNode node) =>
            new RerollRules(node.Field("dustPerSalvage").AsNumber(0, 1000), node.Field("choice").AsNumber(1, 100), node.Field("lock").AsNumber(1, 100));
    }
}
