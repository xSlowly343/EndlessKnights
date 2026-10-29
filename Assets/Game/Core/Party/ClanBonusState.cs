using System.Collections.Generic;

namespace Game.Core.Party
{
    public sealed class ClanBonusState
    {
        public List<string> Nodes { get; } = new List<string>();
        public long AnsweredUnixMs { get; set; }
        public long SeenUnixMs { get; set; }
    }
}
