using System;
using System.Linq;
using Game.Core.Sim;
using Game.Core.Sim.Rift;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class RiftGiftViewTests
    {
        private static readonly long Inside = new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static GameLoop InRift()
        {
            var loop = TestWorld.Loop();
            Assert.That(loop.Rift.Enter(Inside), Is.EqualTo(RiftEntry.Entered));
            loop.Tick();
            return loop;
        }

        [Test]
        public void ThereIsNoWindowAtHomeOrWhenNothingIsOffered()
        {
            var home = TestWorld.Loop();
            home.Tick();
            Assert.That(RiftGiftView.Of(home.RiftGifts), Is.Null);

            var loop = InRift();
            RiftGiftView.Choose(loop, loop.RiftGifts.Offer[0]);
            Assert.That(RiftGiftView.Of(loop.RiftGifts), Is.Null, "the next offer waits for its stage");
        }

        [Test]
        public void ThreeCardsShowNameAndEffectWithTheGiftNumberAndTheNextStage()
        {
            var loop = InRift();
            var view = RiftGiftView.Of(loop.RiftGifts)!;
            Assert.That(view.Cards.Count, Is.EqualTo(loop.RiftGifts.Rules.Offer));
            Assert.That(view.Cards.Select(card => card.Id), Is.EqualTo(loop.RiftGifts.Offer));
            foreach (var card in view.Cards)
            {
                Assert.That(card.Name, Is.Not.Empty.And.Not.StartWith("rift."), card.Id);
                Assert.That(card.Effect, Is.Not.Empty, card.Id);
                Assert.That(card.Name + ": " + card.Effect, Is.EqualTo(Texts.Get("rift.gift." + card.Id)));
            }
            Assert.That(view.Number, Is.EqualTo(1));
            Assert.That(view.NextAt, Is.EqualTo(loop.RiftGifts.Rules.Every));
            Assert.That(view.NumberText, Does.Contain("1"));
            Assert.That(view.NextText, Does.Contain(loop.RiftGifts.Rules.Every.ToString()));
            Assert.That(view.Title, Is.Not.Empty.And.Not.StartWith("rift."));
        }

        [Test]
        public void ChoosingACardTakesTheGiftAndTheSecondOfferCountsOn()
        {
            var loop = InRift();
            var first = RiftGiftView.Of(loop.RiftGifts)!.Cards[1].Id;
            Assert.That(RiftGiftView.Choose(loop, first), Is.EqualTo(RiftGiftResult.Taken));
            Assert.That(loop.RiftGifts.Taken, Is.EqualTo(new[] { first }));
            Assert.That(RiftGiftView.Choose(loop, first), Is.EqualTo(RiftGiftResult.NotOffered));

            loop.State.HighestStage = loop.RiftGifts.Rules.Every;
            loop.Tick();
            var second = RiftGiftView.Of(loop.RiftGifts)!;
            Assert.That(second.Number, Is.EqualTo(2));
            Assert.That(second.NextAt, Is.EqualTo(loop.RiftGifts.Rules.Every * 2));
            Assert.That(second.Cards.Select(card => card.Id), Has.None.EqualTo(first));
        }
    }
}
