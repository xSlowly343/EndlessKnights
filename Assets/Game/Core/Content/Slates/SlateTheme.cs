using System.Collections.Generic;

namespace Game.Core.Content.Slates
{
    public sealed class SlateTheme
    {
        public SlateTheme(string id, IReadOnlyList<SlateLine> lines, SlateLine pattern, SlateLine rule)
        {
            Id = id;
            Lines = lines;
            Pattern = pattern;
            Rule = rule;
        }

        public string Id { get; }
        public IReadOnlyList<SlateLine> Lines { get; }
        public SlateLine Pattern { get; }
        public SlateLine Rule { get; }
    }
}
