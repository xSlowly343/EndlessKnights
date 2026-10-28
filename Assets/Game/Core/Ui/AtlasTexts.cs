using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class AtlasTexts
    {
        public static string Name(AtlasNode node) => Texts.Pick(node.NameRu, node.NameEn);

        public static string Cost(AtlasNode node) => Texts.Format("atlas.cost", node.Cost);

        public static string Result(AtlasResult result) => result switch
        {
            AtlasResult.UnknownNode => Texts.Get("atlas.result.unknownNode"),
            AtlasResult.AlreadyTaken => Texts.Get("atlas.result.alreadyTaken"),
            AtlasResult.NotConnected => Texts.Get("atlas.result.notConnected"),
            AtlasResult.NotEnoughEcho => Texts.Get("atlas.result.notEnoughEcho"),
            _ => string.Empty
        };
    }
}
