using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Tree
{
    public sealed class ClassAttributeDef
    {
        public static readonly StatId Stat = new StatId("classAttribute");
        public static readonly IReadOnlyList<string> Names = new[] { "strength", "dexterity", "intelligence" };
        public static readonly ClassAttributeDef None = new ClassAttributeDef(string.Empty, Array.Empty<Modifier>());

        public ClassAttributeDef(string name, IReadOnlyList<Modifier> perPoint)
        {
            Name = name;
            PerPoint = perPoint;
        }

        public string Name { get; }
        public IReadOnlyList<Modifier> PerPoint { get; }

        public static ClassAttributeDef Read(ContentNode node)
        {
            var nameNode = node.Field("name");
            var name = nameNode.AsString();
            if (!Contains(Names, name))
                throw nameNode.Error($"the class attribute is one of: {string.Join(", ", Names)}");
            var perPoint = ModifierReader.ReadList(node.Field("perPoint"));
            if (perPoint.Count == 0)
                throw node.Field("perPoint").Error("the class attribute must strengthen the class feature");
            foreach (var modifier in perPoint)
            {
                if (modifier.Stat == Stat || Contains(Names, modifier.Stat.Name))
                    throw node.Field("perPoint").Error("the class attribute cannot raise an attribute");
            }
            return new ClassAttributeDef(name, perPoint);
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var item in names)
            {
                if (item == name)
                    return true;
            }
            return false;
        }
    }
}
