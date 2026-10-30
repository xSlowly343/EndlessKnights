using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartyChronicleTextTests
    {
        private const long October23 = 1792717200000;

        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [Test]
        public void GoalsShowTheNextTierAndUntrackedFeatsWait()
        {
            Assert.That(PartyChronicleText.Goal("bossesTogether", true, 0, new[] { 10.0 }, 7), Is.EqualTo("боссов вместе: 7 из 10"));
            Assert.That(PartyChronicleText.Goal("bossesTogether", true, 1, new[] { 100.0, 1000, 10000 }, 240), Is.EqualTo("боссов вместе: 240 из 1000 · ступень 2 из 3"));
            Assert.That(PartyChronicleText.Goal("bossesTogether", true, 3, new[] { 100.0, 1000, 10000 }, 12000), Is.EqualTo("боссов вместе: 10000 из 10000 · ступень 3 из 3"));
            Assert.That(PartyChronicleText.Goal("hoursTogether", true, 0, new[] { 100.0 }, 2.7), Is.EqualTo("часов в бою вместе: 2 из 100"));
            Assert.That(PartyChronicleText.Goal("nightBosses", false, 0, new[] { 20.0 }, 0), Is.EqualTo("появится позже"));
        }

        [Test]
        public void RecordsReadLikeAPartyLog()
        {
            Assert.That(PartyChronicleText.Record("created", October23, "", "", 180), Is.EqualTo("23.10 — отряд собран"));
            Assert.That(PartyChronicleText.Record("joined", October23, "Аня", "", 180), Is.EqualTo("23.10 — в отряде новый участник: Аня"));
            Assert.That(PartyChronicleText.Record("feat", October23, "", "first_steps", 180), Is.EqualTo("23.10 — подвиг «Первые шаги»"));
            Assert.That(PartyChronicleText.Record("rift", October23, "", "", 180, 125), Is.EqualTo("23.10 — Разлом, глубина 125"));
            Assert.That(PartyChronicleText.Feat("wanderers"), Is.EqualTo("Странники"));
            Assert.That(PartyChronicleText.Earned(October23, -180), Is.EqualTo("получено 22.10"), "the day follows the player's own clock");
            Assert.That(PartyChronicleText.Banner(0), Does.StartWith("Знамени пока нет"));
            Assert.That(PartyChronicleText.Banner(2), Is.EqualTo("Знамя отряда · уровень 2"));
        }
    }
}
