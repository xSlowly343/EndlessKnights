using System.Collections.Generic;
using Game.Core.Content.Json;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class JsonWriterTests
    {
        [Test]
        public void KeysAreSortedAndOutputIsIndented()
        {
            var value = JsonValue.Object(new Dictionary<string, JsonValue> { ["b"] = JsonValue.Of(1), ["a"] = JsonValue.Of(true) });

            Assert.That(JsonWriter.Write(value), Is.EqualTo("{\n  \"a\": true,\n  \"b\": 1\n}"));
        }

        [Test]
        public void TextIsEscapedAndReadBack()
        {
            var text = "quote \" slash \\ line\nend\ttab \u0001";

            var written = JsonWriter.Write(JsonValue.Of(text));

            Assert.That(JsonParser.Parse(written, "test.json").Text, Is.EqualTo(text));
        }

        [TestCase(0.1)]
        [TestCase(1.0 / 3)]
        [TestCase(1760000000000d)]
        [TestCase(-2.5e-7)]
        public void NumbersSurviveExactly(double number)
        {
            var written = JsonWriter.Write(JsonValue.Of(number));

            Assert.That(JsonParser.Parse(written, "test.json").Number, Is.EqualTo(number));
        }

        [Test]
        public void EmptyContainersAndNull()
        {
            var value = JsonValue.Array(new[] { JsonValue.Null(), JsonValue.Array(new JsonValue[0]), JsonValue.Object(new Dictionary<string, JsonValue>()) });

            Assert.That(JsonWriter.Write(value), Is.EqualTo("[\n  null,\n  [],\n  {}\n]"));
        }

        [Test]
        public void NotANumberIsRefused()
        {
            Assert.Throws<System.ArgumentException>(() => JsonWriter.Write(JsonValue.Of(double.NaN)));
        }
    }
}
