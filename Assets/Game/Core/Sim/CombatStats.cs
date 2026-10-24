using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class CombatStats
    {
        public static readonly StatId Damage = new StatId("damage");
        public static readonly StatId CritChance = new StatId("critChance");
        public static readonly StatId CritMultiplier = new StatId("critMultiplier");
        public static readonly StatId Gold = new StatId("gold");
        public static readonly StatId FocusGain = new StatId("focusGain");
        public static readonly StatId FocusMultiplier = new StatId("focusMultiplier");
        public static readonly StatId FocusMax = new StatId("focusMax");
        public static readonly StatId FocusResetSeconds = new StatId("focusResetSeconds");
        public static readonly StatId MouseDistance = new StatId("mouseDistance");
        public static readonly StatId ResourceGain = new StatId("resourceGain");
        public static readonly StatId KeyHitLightning = new StatId("keyHitLightning");
        public static readonly StatId KeyStrikeEvery = new StatId("keyStrikeEvery");
        public static readonly StatId KeyStrikeMultiplier = new StatId("keyStrikeMultiplier");
        public static readonly StatId AuraDuration = new StatId("auraDuration");
        public static readonly StatId AuraEffect = new StatId("auraEffect");
        public static readonly StatId IdleStartSeconds = new StatId("idleStartSeconds");
        public static readonly StatId OfflineGold = new StatId("offlineGold");
        public static readonly StatId BossTimerSeconds = new StatId("bossTimerSeconds");
        public static readonly StatId BossTimerSpeed = new StatId("bossTimerSpeed");
        public static readonly StatId OfflineCapHours = new StatId("offlineCapHours");
        public static readonly StatId ComboTimeoutSeconds = new StatId("comboTimeoutSeconds");
        public static readonly StatId CritEvery = new StatId("critEvery");
        public static readonly StatId OverkillCarry = new StatId("overkillCarry");
        public static readonly StatId ComboStrikeMultiplier = new StatId("comboStrikeMultiplier");
    }
}
