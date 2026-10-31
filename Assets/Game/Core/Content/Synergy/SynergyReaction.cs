using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Synergy
{
    public sealed class SynergyReaction
    {
        public SynergyReaction(string id, int first, int second, double seconds, double cooldownSeconds, IReadOnlyList<Modifier> modifiers, StatId? unlock = null)
        {
            Unlock = unlock;
            Id = id;
            First = first;
            Second = second;
            Seconds = seconds;
            CooldownSeconds = cooldownSeconds;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public int First { get; }
        public int Second { get; }
        public double Seconds { get; }
        public double CooldownSeconds { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public StatId? Unlock { get; }
    }
}
