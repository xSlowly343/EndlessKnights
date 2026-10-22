namespace Game.Core.Content
{
    public sealed class CombatRules
    {
        public const string FileName = "combat.json";
        public const int SupportedVersion = 1;

        private CombatRules(double keyDamage, double clickDamage, double mouseDamage, double mousePxPerHit, FocusRules focus)
        {
            KeyDamage = keyDamage;
            ClickDamage = clickDamage;
            MouseDamage = mouseDamage;
            MousePxPerHit = mousePxPerHit;
            Focus = focus;
        }

        public double KeyDamage { get; }
        public double ClickDamage { get; }
        public double MouseDamage { get; }
        public double MousePxPerHit { get; }
        public FocusRules Focus { get; }

        public static CombatRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var hits = root.Field("hits");
            var mouse = hits.Field("mouse");
            return new CombatRules(
                hits.Field("keyboard").Field("damage").AsNumber(0, 1e6),
                hits.Field("click").Field("damage").AsNumber(0, 1e6),
                mouse.Field("damage").AsNumber(0, 1e6),
                mouse.Field("pxPerHit").AsNumber(1, 1e6),
                FocusRules.Read(root.Field("focus")));
        }
    }
}
