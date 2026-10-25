using Game.Core.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ChangeLogTests
    {
        [Test]
        public void RealFileHasBothLanguagesForTheLatestVersion()
        {
            var log = ChangeLog.Load(TestData.ReadGameDataFile(ChangeLog.FileName));

            Assert.That(log.Latest, Is.Not.Null);
            Assert.That(log.Latest!.Lines("ru"), Is.Not.Empty);
            Assert.That(log.Latest.Lines("en").Count, Is.EqualTo(log.Latest.Lines("ru").Count));
        }

        [Test]
        public void MissingLanguageFallsBackToRussian()
        {
            var log = ChangeLog.Load(@"{ ""version"": 1, ""entries"": [ { ""game"": ""0.9"", ""lines"": { ""ru"": [ ""Первая строка"" ] } } ] }");

            Assert.That(log.Latest!.Game, Is.EqualTo("0.9"));
            Assert.That(log.Latest.Lines("en"), Is.EqualTo(new[] { "Первая строка" }));
        }

        [Test]
        public void TooManyLinesAreRejected()
        {
            var lines = string.Join(", ", System.Linq.Enumerable.Repeat(@"""строка""", ChangeLog.MaxLines + 1));

            Assert.Throws<ContentException>(() => ChangeLog.Load(@"{ ""version"": 1, ""entries"": [ { ""game"": ""1.0"", ""lines"": { ""ru"": [ " + lines + @" ] } } ] }"));
        }
    }
}
