using Game.Core.Content;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ClanTreeText
    {
        public static string Name(ClanTreeNode node) => Texts.Pick(node.NameRu, node.NameEn);

        public static string Effect(ClanTreeNode node)
        {
            var parts = new string[node.Modifiers.Count];
            for (var i = 0; i < parts.Length; i++)
                parts[i] = ModifierText.Describe(node.Modifiers[i]);
            return string.Join(", ", parts);
        }

        public static string State(ClanTreeNode node, bool taken, bool open) =>
            taken ? Texts.Get("clanTree.taken") : open ? Texts.Format("clanTree.cost", node.Cost) : Texts.Format("clanTree.closed", node.Cost);

        public static string Treasury(long seals) => Texts.Format("clanTree.treasury", seals);

        public static string Yours(long seals) => Texts.Format("clanTree.yours", seals);

        public static string Log(string kind, string name, long amount, string nodeName) => kind == "bought"
            ? Texts.Format("clanTree.log.bought", name, nodeName, amount)
            : Texts.Format("clanTree.log.donated", name, amount);

        public static string Failed(string reason) => Texts.Format("party.failed", reason switch
        {
            "not enough seals" => Texts.Get("clanTree.reason.notEnough"),
            "not connected" => Texts.Get("clanTree.reason.notConnected"),
            "already taken" => Texts.Get("clanTree.reason.taken"),
            "only the leader can do that" => Texts.Get("clanTree.reason.leaderOnly"),
            "not in a clan" => Texts.Get("clan.reason.notIn"),
            _ => Texts.Get("party.reason.network")
        });
    }
}
