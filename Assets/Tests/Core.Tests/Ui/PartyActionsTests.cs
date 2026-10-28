using System;
using Game.Core.Tests.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PartyActionsTests
    {
        [SetUp]
        public void SetUp()
        {
            _ = TestWorld.Real;
            Texts.UseLanguage("ru");
        }

        [TestCase("K7M-Q2X", "K7MQ2X")]
        [TestCase("  k7mq2x\\n", "K7MQ2X")]
        [TestCase("k7m q2x", "K7MQ2X")]
        public void ACodeIsReadFromTheClipboard(string text, string code)
        {
            Assert.That(PartyActions.TryReadCode(text.Replace("\\n", "\n"), 6, out var read), Is.True);
            Assert.That(read, Is.EqualTo(code));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("K7M-Q2")]
        [TestCase("K7M-Q2XA")]
        [TestCase("K0M-Q2X")]
        [TestCase("hello, here is my party code: K7MQ2X")]
        public void AnythingElseIsNotACode(string? text) => Assert.That(PartyActions.TryReadCode(text, 6, out _), Is.False);

        [Test]
        public void ButtonsChangeWithTheParty()
        {
            Assert.That(PartyActions.Label(PartyActions.Left(false), ""), Is.EqualTo("Создать отряд"));
            Assert.That(PartyActions.Label(PartyActions.Middle(false), ""), Is.EqualTo("Вступить по коду"));
            Assert.That(PartyActions.Label(PartyActions.Right(false), ""), Is.EqualTo("Найти отряд"));
            Assert.That(PartyActions.Label(PartyActions.Left(true), "K7MQ2X"), Is.EqualTo("Код K7M-Q2X"));
            Assert.That(PartyActions.Label(PartyActions.Right(true), ""), Is.EqualTo("Покинуть"));
        }

        [TestCase("party is full", "Не вышло: в отряде нет мест")]
        [TestCase("unknown code", "Не вышло: такого кода нет")]
        [TestCase("no connection to the server: refused", "Не вышло: нет связи с сервером")]
        public void ServerRefusalsAreExplained(string reason, string text) => Assert.That(PartyActions.Failed(reason), Is.EqualTo(text));

        [TestCase("ru")]
        [TestCase("en")]
        public void EveryActionHasAText(string language)
        {
            Texts.UseLanguage(language);
            foreach (PartyAction action in Enum.GetValues(typeof(PartyAction)))
            {
                if (action != PartyAction.None)
                    Assert.That(PartyActions.Label(action, "K7MQ2X"), Is.Not.Empty, action.ToString());
            }
            Assert.That(PartyActions.CodeCopied("K7MQ2X"), Does.Contain("K7M-Q2X"));
            Texts.UseLanguage("ru");
        }
    }
}
