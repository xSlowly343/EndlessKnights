using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public readonly struct ShowCue
    {
        public ShowCue(GemDef gem, int source, ShowSize size)
        {
            Gem = gem;
            Source = source;
            Size = size;
        }

        public GemDef Gem { get; }
        public int Source { get; }
        public ShowSize Size { get; }
    }
}
