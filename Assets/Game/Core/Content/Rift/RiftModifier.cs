using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Rift
{
    public sealed class RiftModifier
    {
        public RiftModifier(string id, IReadOnlyList<string> unlessMarks, IReadOnlyList<string> favours, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            UnlessMarks = unlessMarks;
            Favours = favours;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public IReadOnlyList<string> UnlessMarks { get; }
        public IReadOnlyList<string> Favours { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }
}
