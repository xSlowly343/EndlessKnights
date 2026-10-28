using Game.Core.Content.Gems;
using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct EnemyBadge
    {
        public EnemyBadge(int colorIndex, GemColor gemColor, int secondsLeft, string name)
        {
            ColorIndex = colorIndex;
            GemColor = gemColor;
            SecondsLeft = secondsLeft;
            Name = name;
        }

        public int ColorIndex { get; }
        public GemColor GemColor { get; }
        public int SecondsLeft { get; }
        public string Name { get; }
    }

    public readonly struct BossShare
    {
        public BossShare(int colorIndex, double share, string label)
        {
            ColorIndex = colorIndex;
            Share = share;
            Label = label;
        }

        public int ColorIndex { get; }
        public double Share { get; }
        public string Label { get; }
    }

    public static class PartyStripMarks
    {
        public const int NoColor = -1;

        public static int ColorIndexOf(in RosterMessage roster, int ownSlot, int slot)
        {
            if (slot < 0 || slot >= PartyRules.MaxSlots || ownSlot < 0)
                return NoColor;
            if (slot == ownSlot)
                return 0;
            if (roster.Player(slot) == 0)
                return NoColor;
            var index = 1;
            for (var other = 0; other < slot; other++)
            {
                if (other != ownSlot && roster.Player(other) != 0)
                    index++;
            }
            return index;
        }

        public static int FillBadges(PartyClient client, GemCatalog gems, EnemyBadge[] into)
        {
            if (!client.InParty)
                return 0;
            var count = 0;
            var roster = client.Roster;
            for (var i = 0; i < ShownEffect.MaxShown && count < into.Length; i++)
            {
                var effect = client.EnemyEffect(i);
                if (!effect.Active)
                    continue;
                var known = gems.TryGetByNetId(effect.Id, out var gem);
                var seconds = (effect.TicksLeft + GameLoop.TicksPerSecond - 1) / GameLoop.TicksPerSecond;
                into[count++] = new EnemyBadge(ColorIndexOf(roster, client.OwnSlot, effect.Owner), known ? gem.Color : GemColor.White, seconds,
                    known ? Texts.Pick(gem.NameRu, gem.NameEn) : string.Empty);
            }
            return count;
        }

        public static int FillBossShares(in EnemyKilledMessage kill, in RosterMessage roster, int ownSlot, BossShare[] into)
        {
            var count = 0;
            var number = 1;
            for (var slot = 0; slot < PartyRules.MaxSlots && count < into.Length && ownSlot >= 0; slot++)
            {
                var self = slot == ownSlot;
                if (!self && roster.Player(slot) == 0)
                    continue;
                if (!self)
                    number++;
                var share = kill.Contribution(slot);
                var percent = (int)System.Math.Round(share * 100, System.MidpointRounding.AwayFromZero);
                var name = self ? Texts.Get("party.you") : Texts.Format("party.player", number);
                into[count++] = new BossShare(ColorIndexOf(roster, ownSlot, slot), share, Texts.Format("party.boss.share", name, percent));
            }
            return count;
        }

        public static string BossTitle(bool rewarded) => Texts.Get(rewarded ? "party.boss.down" : "party.boss.downUnpaid");
    }
}
