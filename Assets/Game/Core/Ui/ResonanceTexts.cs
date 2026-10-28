using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ResonanceTexts
    {
        public static string Name(ResonanceBranch branch) => Texts.Pick(branch.NameRu, branch.NameEn);

        public static string Level(int level, ResonanceBranch branch) => Texts.Format("resonance.level", level, branch.MaxLevel);

        public static string Result(ResonanceResult result) => result switch
        {
            ResonanceResult.UnknownBranch => Texts.Get("resonance.result.unknownBranch"),
            ResonanceResult.MaxLevel => Texts.Get("resonance.result.maxLevel"),
            ResonanceResult.NotEnoughEcho => Texts.Get("resonance.result.notEnoughEcho"),
            _ => string.Empty
        };
    }
}
