using Game.Core.Build;
using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public struct SkillWindowSupport
    {
        public GemDef Gem;
        public BookResult Offer;
        public int Socket;

        public bool Socketed => Socket >= 0;
        public bool Available => Offer == BookResult.Done;
    }
}
