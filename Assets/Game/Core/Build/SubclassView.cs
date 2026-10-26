using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Build
{
    public static class SubclassView
    {
        public static bool IsTaken(AscendancyState state, TreeNodeDef node) => node.Type == TreeNodeType.Start || state.IsAllocated(node.Id);

        public static IReadOnlyList<string> StatLines(TreeNodeDef node)
        {
            var lines = new List<string>();
            foreach (var modifier in node.Modifiers)
                lines.Add(ModifierText.Describe(modifier));
            return lines;
        }

        public static string Status(AscendancyState state, AscendancyCatalog catalog, TreeNodeDef node)
        {
            if (node.Type == TreeNodeType.Start)
                return Texts.Get("subclass.status.start");
            if (state.IsAllocated(node.Id))
                return Texts.Get("tree.status.taken");
            return state.Check(catalog, node.Id) switch
            {
                AllocationResult.Locked => Texts.Get("tree.status.locked"),
                AllocationResult.NoPoints => Texts.Get("tree.status.noPoints"),
                AllocationResult.NotConnected => Texts.Get("tree.status.notConnected"),
                _ => Texts.Get("subclass.status.canTake")
            };
        }

        public static string Kind(SubclassDef subclass, TreeNodeDef node) =>
            TreeNodeText.TypeLabel(node) + " · " + Name(subclass);

        public static TreeNodeCard Card(AscendancyState state, AscendancyCatalog catalog, SubclassDef subclass, TreeNodeDef node, StatSheet hero) =>
            TreeNodeCard.Build(node, Kind(subclass, node), StatLines(node), Status(state, catalog, node), node.Modifiers, IsTaken(state, node), hero);

        public static bool CanTake(AscendancyState state, AscendancyCatalog catalog, TreeNodeDef node) =>
            !state.IsAllocated(node.Id) && state.Check(catalog, node.Id) == AllocationResult.Allocated;

        public static string Name(SubclassDef subclass) => Texts.Pick(subclass.NameRu, subclass.NameEn);

        public static string Points(AscendancyState state, AscendancyCatalog catalog, SubclassDef subclass) =>
            Texts.Format("subclass.points", state.PointsLeft(catalog), state.Allocated.Count, Takeable(subclass));

        public static int Takeable(SubclassDef subclass)
        {
            var count = 0;
            foreach (var node in subclass.Nodes)
                if (node.Type != TreeNodeType.Start)
                    count++;
            return count;
        }

        public static string? PathSkill(GemCatalog gems, SubclassDef subclass)
        {
            foreach (var gem in gems.All)
                if (gem.Ascendancy == subclass.Id)
                    return Texts.Format("subclass.skill", Texts.Pick(gem.NameRu, gem.NameEn));
            return null;
        }

        public static string Locked(AscendancyCatalog catalog)
        {
            var names = new List<string>();
            foreach (var activity in catalog.Activities)
                names.Add(Texts.Pick(activity.NameRu, activity.NameEn).ToLowerInvariant());
            return Texts.Format("subclass.locked", string.Join(", ", names));
        }
    }
}
