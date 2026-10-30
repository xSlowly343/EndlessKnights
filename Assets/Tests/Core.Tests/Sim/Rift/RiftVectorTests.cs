using System;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core.Tests.Party;
using NUnit.Framework;

namespace Game.Core.Tests.Sim.Rift
{
    public class RiftVectorTests
    {
        private static readonly string VectorPath = Path.Combine("Server", "test-vectors", "rift-schedule.json");

        [Test]
        public void ServerScheduleVectorsMatchTheCore()
        {
            var json = Build();
            var path = PartyVectorTests.RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, json);
                Assert.Pass("vectors written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(json), $"vectors are stale, run the tests with {PartyVectorTests.UpdateVariable}=1");
        }

        private static string Build()
        {
            var rules = TestWorld.Data.Rift;
            var schedule = rules.Schedule;
            var builder = new StringBuilder("{\n  \"version\": 1,\n  \"samples\": [\n");
            var times = SampleTimes();
            for (var i = 0; i < times.Length; i++)
            {
                var now = times[i];
                var window = schedule.WindowAt(now);
                var next = schedule.Next(now);
                builder.Append("    { \"nowUnixMs\": ").Append(Number(now));
                if (window == null)
                    builder.Append(", \"window\": null");
                else
                    builder.Append(", \"window\": { \"id\": ").Append(Number(window.Value.Id))
                        .Append(", \"startUnixMs\": ").Append(Number(window.Value.StartUnixMs))
                        .Append(", \"endUnixMs\": ").Append(Number(window.Value.EndUnixMs))
                        .Append(", \"modifiers\": [").Append(Modifiers(window.Value.Id)).Append("] }");
                builder.Append(", \"nextStartUnixMs\": ").Append(Number(next.StartUnixMs))
                    .Append(", \"nextModifiers\": [").Append(Modifiers(next.Id)).Append(']')
                    .Append(", \"milestoneGems\": [").Append(Quoted(rules.Milestones.GemsFor(next.Id))).Append(']')
                    .Append(", \"announced\": ").Append(schedule.Announced(now) ? "true" : "false")
                    .Append(i == times.Length - 1 ? " }\n" : " },\n");
            }
            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        private static long[] SampleTimes()
        {
            var start = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            const long step = 7L * 3600 * 1000 + 13 * 60 * 1000;
            var saturdayNoon = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var edges = new[] { -3_600_001L, -3_600_000L, -1_800_000L, -1L, 0L, 86_399_999L, 86_400_000L };
            var times = new long[60 + edges.Length];
            for (var i = 0; i < 60; i++)
                times[i] = start + i * step;
            for (var i = 0; i < edges.Length; i++)
                times[60 + i] = saturdayNoon + edges[i];
            return times;
        }

        private static string Modifiers(long windowId)
        {
            var picked = TestWorld.Data.Rift.ModifiersFor(windowId);
            var text = new StringBuilder();
            for (var i = 0; i < picked.Count; i++)
                text.Append(i == 0 ? "" : ", ").Append('"').Append(picked[i].Id).Append('"');
            return text.ToString();
        }

        private static string Quoted(System.Collections.Generic.IReadOnlyList<string> ids)
        {
            var text = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
                text.Append(i == 0 ? "" : ", ").Append('"').Append(ids[i]).Append('"');
            return text.ToString();
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
