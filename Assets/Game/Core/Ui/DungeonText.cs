using System.Text;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class DungeonText
    {
        public static string Key(DungeonSystem dungeons, DungeonKey key) =>
            Texts.Format("dungeon.key", key.Tier, dungeons.StartStage(key), key.Attempts, dungeons.Rules.KeyAttempts);

        public static string Modifiers(DungeonRules rules, DungeonKey key)
        {
            if (key.Modifiers.Count == 0)
                return Texts.Get("dungeon.modifiers.none");
            var russian = Texts.Language == StringTable.DefaultLanguage;
            var text = new StringBuilder();
            foreach (var id in key.Modifiers)
            {
                if (!rules.TryGetModifier(id, out var modifier))
                    continue;
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(russian ? modifier.NameRu : modifier.NameEn);
            }
            return text.ToString();
        }

        public static string Reward(DungeonSystem dungeons, DungeonKey key)
        {
            var tier = dungeons.Rules.TierAt(key.Tier);
            if (tier == null)
                return string.Empty;
            var scale = 1 + dungeons.RewardBonus(key);
            return Texts.Format("dungeon.reward", System.Math.Round(tier.Loot.Items * scale, 1), System.Math.Round(tier.Shards * scale), System.Math.Round(dungeons.RewardBonus(key) * 100));
        }

        public static string Salvage(DungeonRules rules, DungeonKey key) => Texts.Format("dungeon.salvage", rules.SalvageShards(key.Tier));

        public static string Merge(DungeonRules rules, int tier) => Texts.Format("dungeon.merge", rules.MergeCount, tier, tier + 1);

        public static string Auto(bool on, int minTier) => on ? Texts.Format("dungeon.auto.on", minTier) : Texts.Get("dungeon.auto.off");

        public static string Entry(DungeonResult result) => Texts.Get(result switch
        {
            DungeonResult.Locked => "dungeon.entry.locked",
            DungeonResult.NoKey => "dungeon.entry.noKey",
            DungeonResult.AlreadyInside => "dungeon.entry.inside",
            DungeonResult.InsideRift => "dungeon.entry.rift",
            DungeonResult.InParty => "dungeon.entry.party",
            _ => "dungeon.entry.entered"
        });

        public static string Change(DungeonKeyResult result) => Texts.Get(result switch
        {
            DungeonKeyResult.NoKey => "dungeon.entry.noKey",
            DungeonKeyResult.NotEnoughKeys => "dungeon.merge.notEnough",
            DungeonKeyResult.NoHigherTier => "dungeon.merge.noTier",
            _ => "dungeon.change.done"
        });
    }
}
