using System.Linq;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class BookTextsTests
    {
        [Test]
        public void EveryRefusalHasAMessage()
        {
            foreach (BookResult result in System.Enum.GetValues(typeof(BookResult)))
                Assert.That(BookTexts.Result(result), result == BookResult.Done ? Is.Empty : Is.Not.Empty);
        }

        [Test]
        public void FirstTwoRowsAreTheMouseButtons()
        {
            Assert.That(BookTexts.RowKey(0), Is.EqualTo("ЛКМ"));
            Assert.That(BookTexts.RowKey(1), Is.EqualTo("ПКМ"));
            Assert.That(BookTexts.RowKey(2), Is.EqualTo("Авто"));
        }

        [Test]
        public void SlotsAndGemDescriptionReadNaturally()
        {
            var gem = TestWorld.Real.Gems.All.First(def => def.Kind == GemKind.Skill);
            Texts.UseLanguage("ru");

            Assert.That(BookTexts.Slots(3, 5), Is.EqualTo("Слоты 3 / 5"));
            Assert.That(BookTexts.Describe(gem, 2), Is.EqualTo(gem.NameRu + " · скилл · в сумке 2"));
            Assert.That(BookTexts.Describe(gem, 0), Is.EqualTo(gem.NameRu + " · скилл"));
        }

        [Test]
        public void ShortNameIsTwoLetters()
        {
            Assert.That(BookTexts.Short("Стенография"), Is.EqualTo("Ст"));
            Assert.That(BookTexts.Short("Я"), Is.EqualTo("Я"));
        }
    }
}
