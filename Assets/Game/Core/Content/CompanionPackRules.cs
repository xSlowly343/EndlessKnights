namespace Game.Core.Content
{
    public sealed class CompanionPackRules
    {
        public double Loot { get; private set; } = 1;
        public double FamilyBonus { get; private set; }
        public int Slots { get; private set; } = 3;
        public int MentorMinStars { get; private set; } = 1;
        public int StudentMaxLevel { get; private set; } = 20;
        public double MentorShare { get; private set; }

        internal static CompanionPackRules Load(ContentNode pack, ContentNode mentor) => new CompanionPackRules
        {
            Loot = pack.Field("loot").AsNumber(1, 10),
            FamilyBonus = pack.Field("familyBonus").AsNumber(0, 1),
            Slots = pack.Field("slots").AsInt(1, 10),
            MentorMinStars = mentor.Field("minStars").AsInt(0, 10),
            StudentMaxLevel = mentor.Field("studentMaxLevel").AsInt(1, 100),
            MentorShare = mentor.Field("share").AsNumber(0, 1)
        };
    }
}
