using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Rift;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Content
{
    public class RiftRulesTests
    {
        private static RiftRules Rules => TestWorld.Data.Rift;

        private static long Utc(int year, int month, int day, int hour, int minute = 0) =>
            new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        [Test]
        public void TuesdayWindowRunsFromNoonToNoon()
        {
            var start = Utc(2026, 10, 27, 12);

            Assert.That(Rules.Schedule.WindowAt(start - 1), Is.Null);
            Assert.That(Rules.Schedule.WindowAt(start)!.Value.StartUnixMs, Is.EqualTo(start));
            Assert.That(Rules.Schedule.WindowAt(Utc(2026, 10, 28, 11, 59))!.Value.EndUnixMs, Is.EqualTo(Utc(2026, 10, 28, 12)));
            Assert.That(Rules.Schedule.WindowAt(Utc(2026, 10, 28, 12)), Is.Null);
        }

        [Test]
        public void NextWindowFromFridayIsSaturday()
        {
            var next = Rules.Schedule.Next(Utc(2026, 10, 23, 20));

            Assert.That(next.StartUnixMs, Is.EqualTo(Utc(2026, 10, 24, 12)));
            Assert.That(next.Id, Is.EqualTo(Utc(2026, 10, 24, 12) / 1000));
        }

        [Test]
        public void SundayMorningIsStillInsideTheSaturdayWindow()
        {
            Assert.That(Rules.Schedule.WindowAt(Utc(2026, 10, 25, 9))!.Value.StartUnixMs, Is.EqualTo(Utc(2026, 10, 24, 12)));
        }

        [Test]
        public void AnnouncedAnHourBefore()
        {
            Assert.That(Rules.Schedule.Announced(Utc(2026, 10, 24, 10, 59)), Is.False);
            Assert.That(Rules.Schedule.Announced(Utc(2026, 10, 24, 11, 0)), Is.True);
            Assert.That(Rules.Schedule.Announced(Utc(2026, 10, 24, 12, 30)), Is.False);
        }

        [Test]
        public void EachWindowPicksDistinctModifiersTheSameWayEveryTime()
        {
            var seen = new HashSet<string>();
            var time = Utc(2026, 10, 23, 0);
            for (var i = 0; i < 40; i++)
            {
                var window = Rules.Schedule.Next(time);
                var picked = Rules.ModifiersFor(window.Id);
                Assert.That(picked.Count, Is.EqualTo(Rules.ModifiersPerWindow));
                Assert.That(picked[0].Id, Is.Not.EqualTo(picked[1].Id));
                Assert.That(Rules.ModifiersFor(window.Id)[0].Id, Is.EqualTo(picked[0].Id));
                foreach (var modifier in picked)
                    seen.Add(modifier.Id);
                time = window.EndUnixMs;
            }

            Assert.That(seen.Count, Is.EqualTo(Rules.Modifiers.Count));
        }

        [Test]
        public void UnknownMarkIsRejected()
        {
            var json = TestData.ReadGameDataFile(RiftRules.FileName).Replace("\"favours\": [\"summon\"]", "\"favours\": [\"summonz\"]");
            var rules = RiftRules.Load(json);

            var error = Assert.Throws<ContentException>(() => rules.RequireMarks(TestWorld.Data.Synergy))!;

            Assert.That(error.Message, Does.Contain("summonz"));
        }

        [Test]
        public void ZoneMustBeShorterThanPeriod()
        {
            var json = TestData.ReadGameDataFile(RiftRules.FileName).Replace("\"zoneLength\": 3", "\"zoneLength\": 25");

            Assert.Throws<ContentException>(() => RiftRules.Load(json));
        }
    }
}
