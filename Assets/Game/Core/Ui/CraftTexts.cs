using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class CraftTexts
    {
        public static string Cost(double ingots) => Texts.Format("craft.cost", ingots);

        public static string Result(CraftResult result) => result switch
        {
            CraftResult.UnknownSet => Texts.Get("craft.result.unknownSet"),
            CraftResult.NeedsSlot => Texts.Get("craft.result.needsSlot"),
            CraftResult.WrongSlot => Texts.Get("craft.result.wrongSlot"),
            CraftResult.NotEnoughIngots => Texts.Get("craft.result.notEnoughIngots"),
            CraftResult.BagFull => Texts.Get("craft.result.bagFull"),
            _ => string.Empty
        };
    }
}
