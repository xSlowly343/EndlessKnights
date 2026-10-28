using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class SpiritTexts
    {
        public static string Name(SpiritDef spirit) => Texts.Pick(spirit.NameRu, spirit.NameEn);

        public static string Rule(SpiritDef spirit, int level) => Texts.Format(RuleKey(spirit.Trigger), spirit.Every, spirit.MultiplierAt(System.Math.Max(1, level)));

        public static string Result(SpiritResult result) => result switch
        {
            SpiritResult.UnknownSpirit => Texts.Get("spirit.result.unknownSpirit"),
            SpiritResult.MaxLevel => Texts.Get("spirit.result.maxLevel"),
            SpiritResult.NotEnoughEcho => Texts.Get("spirit.result.notEnoughEcho"),
            _ => string.Empty
        };

        private static string RuleKey(SpiritTrigger trigger) => trigger switch
        {
            SpiritTrigger.Keys => "spirit.trigger.keys",
            SpiritTrigger.MousePx => "spirit.trigger.mousePx",
            SpiritTrigger.Clicks => "spirit.trigger.clicks",
            SpiritTrigger.Casts => "spirit.trigger.casts",
            SpiritTrigger.IdleSeconds => "spirit.trigger.idleSeconds",
            SpiritTrigger.Kills => "spirit.trigger.kills",
            SpiritTrigger.Crits => "spirit.trigger.crits",
            _ => "spirit.trigger.seconds"
        };
    }
}
