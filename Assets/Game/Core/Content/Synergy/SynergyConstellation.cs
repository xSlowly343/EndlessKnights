using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Synergy
{
    public sealed class SynergyConstellation
    {
        public SynergyConstellation(string id, IReadOnlyList<HeroClass> classes, IReadOnlyList<int> roleGroups, int marks, bool noReactionCooldown, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            Classes = classes;
            RoleGroups = roleGroups;
            Marks = marks;
            NoReactionCooldown = noReactionCooldown;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public IReadOnlyList<HeroClass> Classes { get; }
        public IReadOnlyList<int> RoleGroups { get; }
        public int Marks { get; }
        public bool NoReactionCooldown { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public int Members => Classes.Count + RoleGroups.Count;
    }
}
