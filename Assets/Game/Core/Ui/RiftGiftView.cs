using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class RiftGiftView
    {
        private const string NameSeparator = ": ";

        public readonly struct Card
        {
            public Card(string id, string name, string effect)
            {
                Id = id;
                Name = name;
                Effect = effect;
            }

            public string Id { get; }
            public string Name { get; }
            public string Effect { get; }
        }

        private RiftGiftView(IReadOnlyList<Card> cards, int number, long nextAt)
        {
            Cards = cards;
            Number = number;
            NextAt = nextAt;
        }

        public IReadOnlyList<Card> Cards { get; }
        public int Number { get; }
        public long NextAt { get; }

        public string Title => Texts.Get("rift.gift.offered");
        public string NumberText => Texts.Format("rift.gift.number", Number);
        public string NextText => Texts.Format("rift.gift.next", NextAt);

        public static RiftGiftView? Of(RiftGiftSystem gifts)
        {
            if (!gifts.Choosing)
                return null;
            var cards = new Card[gifts.Offer.Count];
            for (var i = 0; i < cards.Length; i++)
                cards[i] = CardOf(gifts.Offer[i]);
            return new RiftGiftView(cards, gifts.Taken.Count + 1, gifts.NextAt);
        }

        public static RiftGiftResult Choose(GameLoop loop, string id) => loop.RiftGifts.Choose(id);

        public static Card CardOf(string id)
        {
            var text = Texts.Get("rift.gift." + id);
            var split = text.IndexOf(NameSeparator, System.StringComparison.Ordinal);
            return split < 0
                ? new Card(id, text, string.Empty)
                : new Card(id, text.Substring(0, split), text.Substring(split + NameSeparator.Length));
        }
    }
}
