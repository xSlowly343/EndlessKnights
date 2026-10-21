using Game.Core.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class ContentNodeTests
    {
        private const string File = "stages.json";

        private static ContentNode Root(string json) => ContentNode.Root(json, File);

        private static ContentException Fails(TestDelegate action) => Assert.Throws<ContentException>(action)!;

        [Test]
        public void ReadsNestedValues()
        {
            var root = Root(@"{ ""version"": 1, ""name"": ""Hills"", ""boss"": true, ""stages"": [ { ""hp"": 10.5 }, { ""hp"": 2e3 } ] }");

            Assert.That(root.Field("version").AsInt(), Is.EqualTo(1));
            Assert.That(root.Field("name").AsString(), Is.EqualTo("Hills"));
            Assert.That(root.Field("boss").AsBool(), Is.True);
            var stages = root.Field("stages").Items();
            Assert.That(stages.Count, Is.EqualTo(2));
            Assert.That(stages[1].Field("hp").AsNumber(), Is.EqualTo(2000));
            Assert.That(stages[0].Field("hp").Path, Is.EqualTo("stages[0].hp"));
        }

        [Test]
        public void MissingFieldNamesFileAndPath()
        {
            var root = Root(@"{ ""stages"": [ { ""hp"": 1 }, { ""gold"": 2 } ] }");

            var error = Fails(() => root.Field("stages").Items()[1].Field("hp"));

            Assert.That(error.FileName, Is.EqualTo(File));
            Assert.That(error.FieldPath, Is.EqualTo("stages[1]"));
            Assert.That(error.Message, Is.EqualTo("stages.json: stages[1]: required field 'hp' is missing"));
        }

        [Test]
        public void WrongTypeNamesTheField()
        {
            var error = Fails(() => Root(@"{ ""hp"": ""ten"" }").Field("hp").AsNumber());

            Assert.That(error.Message, Is.EqualTo("stages.json: hp: expected a number, got a string"));
        }

        [Test]
        public void OutOfRangeAndFractionsAreRejected()
        {
            var root = Root(@"{ ""count"": 2.5, ""chance"": 1.5 }");

            Assert.That(Fails(() => root.Field("count").AsInt()).Message, Does.Contain("count: expected a whole number"));
            Assert.That(Fails(() => root.Field("chance").AsNumber(0, 1)).Message, Does.Contain("chance: 1.5 is outside the allowed range 0..1"));
        }

        [Test]
        public void EmptyStringIsRejected()
        {
            Assert.That(Fails(() => Root(@"{ ""id"": """" }").Field("id").AsString()).Message, Is.EqualTo("stages.json: id: must not be empty"));
        }

        [Test]
        public void SyntaxErrorReportsLineAndColumn()
        {
            var error = Fails(() => Root("{\n  \"hp\": 10\n  \"gold\": 5\n}"));

            Assert.That(error.Message, Is.EqualTo("stages.json: line 3, column 3: expected ',' or '}' in object"));
        }

        [TestCase("{", "unexpected end of file")]
        [TestCase("{ \"a\": tru }", "unexpected character 't'")]
        [TestCase("{ \"a\": 1 } x", "unexpected text after the end of the document")]
        [TestCase("{ \"a\": \"open }", "string is not closed")]
        [TestCase("{ \"a\": 1, \"a\": 2 }", "field 'a' is repeated")]
        [TestCase("{ \"a\": 1.2.3 }", "'1.2.3' is not a number")]
        [TestCase("[1, 2,]", "unexpected character ']'")]
        public void BrokenJsonIsExplained(string json, string problem)
        {
            Assert.That(Fails(() => Root(json)).Message, Does.Contain(problem));
        }

        [Test]
        public void StringEscapesAreDecoded()
        {
            var text = Root(@"{ ""s"": ""a\""b\\c\nd\u0041"" }").Field("s").AsString();

            Assert.That(text, Is.EqualTo("a\"b\\c\ndA"));
        }

        [Test]
        public void HasFieldForOptionalValues()
        {
            var root = Root(@"{ ""a"": null }");

            Assert.That(root.HasField("a"), Is.True);
            Assert.That(root.HasField("b"), Is.False);
            Assert.That(Fails(() => root.Field("a").AsNumber()).Message, Does.Contain("expected a number, got null"));
        }
    }
}
