namespace Game.Core.Content
{
    public sealed class CurseRules
    {
        public CurseRules(int maxOnEnemy, double flickBonus, double flickMax)
        {
            MaxOnEnemy = maxOnEnemy;
            FlickBonus = flickBonus;
            FlickMax = flickMax;
        }

        public int MaxOnEnemy { get; }
        public double FlickBonus { get; }
        public double FlickMax { get; }
        public int FlicksToMax => FlickBonus <= 0 ? 0 : (int)System.Math.Ceiling(FlickMax / FlickBonus - 1e-9);

        public static CurseRules Read(ContentNode node)
        {
            var flick = node.Field("flick");
            return new CurseRules(
                node.Field("maxOnEnemy").AsInt(1, 100),
                flick.Field("bonus").AsNumber(0, 10),
                flick.Field("max").AsNumber(0, 100));
        }
    }
}
