using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content.Gems;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class GemRebirthTests
    {
        private static GemLevels Levels => TestWorld.Data.Gems.Levels;
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop WithSkill()
        {
            var loop = TestWorld.Loop();
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.OwnedGems.Add(Gem("march"));
            Assert.That(loop.Book.Equip(Gem("march")), Is.EqualTo(BookResult.Done));
            return loop;
        }

        private static void MaxOut(GameLoop loop) => loop.State.GemProgress["march"].Level = Levels.Max;

        [Test]
        public void RebirthNeedsTheMaximumLevel()
        {
            var loop = WithSkill();

            Assert.That(loop.RebirthGem(Gem("march")), Is.False);
            MaxOut(loop);
            Assert.That(loop.RebirthGem(Gem("march")), Is.True);

            var progress = loop.State.GemProgress["march"];
            Assert.That(progress.Level, Is.EqualTo(1));
            Assert.That(progress.Experience, Is.EqualTo(0));
            Assert.That(progress.Rebirths, Is.EqualTo(1));
        }

        [Test]
        public void GemAfterTwoRebirthsHasThreeSocketsWithoutRecords()
        {
            var loop = WithSkill();
            Assert.That(loop.Book.SocketsAt(0), Is.EqualTo(1));

            MaxOut(loop);
            loop.RebirthGem(Gem("march"));
            MaxOut(loop);
            loop.RebirthGem(Gem("march"));

            Assert.That(loop.Book.SocketsAt(0), Is.EqualTo(3));
            Assert.That(loop.Book.RebirthsOf(Gem("march")), Is.EqualTo(2));
        }

        [Test]
        public void ThirdRebirthIsRefused()
        {
            var loop = WithSkill();
            for (var i = 0; i < TestWorld.Data.SkillBook.SocketRebirths; i++)
            {
                MaxOut(loop);
                Assert.That(loop.RebirthGem(Gem("march")), Is.True);
            }

            MaxOut(loop);

            Assert.That(loop.RebirthGem(Gem("march")), Is.False);
            Assert.That(loop.State.GemProgress["march"].Level, Is.EqualTo(Levels.Max));
        }

        [Test]
        public void RebirthSocketOpensForSupports()
        {
            var loop = WithSkill();
            loop.State.HeroLevel = Gem("rebound").UnlockLevel;
            Assert.That(loop.Book.SetSupport(0, 1, Gem("rebound")), Is.EqualTo(BookResult.NoSuchSocket));

            MaxOut(loop);
            loop.RebirthGem(Gem("march"));

            Assert.That(loop.Book.SetSupport(0, 1, Gem("rebound")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void RebirthsSurviveSaveAndLoad()
        {
            var loop = WithSkill();
            MaxOut(loop);
            loop.RebirthGem(Gem("march"));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.GemProgress["march"].Rebirths, Is.EqualTo(1));
        }
    }
}
