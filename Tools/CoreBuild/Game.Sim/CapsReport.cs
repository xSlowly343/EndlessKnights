using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Sim.Headless;

namespace Game.Sim
{
    public static class CapsReport
    {
        private const int GrowthDays = 400;

        private sealed class Day
        {
            public int Number;
            public long Record;
            public double Points;
            public double Minutes;
            public double PeakPoints;
            public double OfflinePerSecond;
            public double PeakFar;
            public double PeakBosses;
            public long Far;
        }

        public static int Run(GameData data, BotProfile profile, SimOptions options)
        {
            var rules = data.Party.Growth.Colossus;
            var window = Math.Max(1, (int)Math.Round(rules.ReportSeconds / 60));
            var minutePoints = new Queue<double>();
            var minuteFar = new Queue<double>();
            var minuteBosses = new Queue<double>();
            var direct = new double[PartyTrialFight.Kinds];
            var background = new double[PartyTrialFight.Kinds];
            var days = new List<Day>();
            var today = new Day { Number = 1 };
            var bossesSeen = 0L;
            var simulation = new PlayerSimulation(data, profile, options.HeroClass, options.Seed, options.StuckMinutes, options.Path)
            {
                Gear = options.Gear,
                Prepare = loop =>
                {
                    loop.SetPartyStanding(true, 1, options.Members, 1);
                    loop.SetPartyColossusOpen(true);
                },
                MinutePassed = (day, loop) =>
                {
                    loop.PartyColossus.TakePending(direct, background);
                    var points = Sum(background);
                    var far = loop.PartyFar.Take(PartyFarMeter.Dungeons).Bosses + loop.PartyFar.Take(PartyFarMeter.Rift).Bosses;
                    var bosses = loop.State.BossKills - bossesSeen;
                    bossesSeen = loop.State.BossKills;
                    today.Points += points;
                    today.Minutes++;
                    today.Far += far;
                    today.PeakPoints = Math.Max(today.PeakPoints, Push(minutePoints, points, window) / (window * 60));
                    today.PeakFar = Math.Max(today.PeakFar, Push(minuteFar, far, window) / window);
                    today.PeakBosses = Math.Max(today.PeakBosses, Push(minuteBosses, bosses, window) / window);
                },
                DayEnded = (day, loop) =>
                {
                    today.Record = loop.State.RecordStage;
                    var offline = loop.PartyColossus.TakeOffline();
                    days.Add(today);
                    today = new Day { Number = day + 1 };
                    minutePoints.Clear();
                    minuteFar.Clear();
                    minuteBosses.Clear();
                    if (days.Count > 1)
                        days[days.Count - 2].OfflinePerSecond = offline.Seconds > 0 ? offline.Points / offline.Seconds : 0;
                }
            };
            simulation.Run(options.PartyCapsDays, options.ActiveHours);

            var totalPoints = 0.0;
            var totalMinutes = 0.0;
            foreach (var day in days)
            {
                totalPoints += day.Points;
                totalMinutes += day.Minutes;
            }
            var growth = new PartyGrowthModel(data, options.Members, options.ActiveHours, totalMinutes > 0 ? totalPoints / totalMinutes * 60 : 0).Run(GrowthDays);
            var power = 1.0;
            foreach (var day in growth)
                power = Math.Max(power, day.ColossusPower * day.Damage);

            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            output.WriteLine("day,record,background_per_s,background_peak_per_s,direct_peak_per_s,offline_per_s,far_bosses,far_peak_per_min,stage_bosses_peak_per_min");
            var peakBackground = 0.0;
            var peakOffline = 0.0;
            var peakFar = 0.0;
            var peakBosses = 0.0;
            foreach (var day in days)
            {
                output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.###},{3:0.###},{4:0.###},{5:0.###},{6},{7:0.##},{8:0.##}",
                    day.Number, day.Record, day.Minutes > 0 ? day.Points / (day.Minutes * 60) : 0, day.PeakPoints, day.PeakPoints / rules.Background,
                    day.OfflinePerSecond, day.Far, day.PeakFar, day.PeakBosses));
                peakBackground = Math.Max(peakBackground, day.PeakPoints);
                peakOffline = Math.Max(peakOffline, day.OfflinePerSecond);
                peakFar = Math.Max(peakFar, day.PeakFar);
                peakBosses = Math.Max(peakBosses, day.PeakBosses);
            }
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} days: colossus points per second at party power x1 - background peak {1:0.##}, direct peak {2:0.##}, offline {3:0.###}; party power on the colossus up to x{4:0.##} by day {5}: direct x{4:0.##} = {6:0.#}/s (cap {7}), offline x{4:0.##} = {8:0.##}/s (cap {9}); far bosses peak {10:0.##}/min, stage bosses peak {11:0.##}/min",
                days.Count, peakBackground, peakBackground / rules.Background, peakOffline, power, GrowthDays, peakBackground / rules.Background * power, rules.MaxPointsPerSecond,
                peakOffline * power, rules.MaxOfflinePointsPerSecond, peakFar, peakBosses));
            return 0;
        }

        private static double Push(Queue<double> values, double value, int window)
        {
            values.Enqueue(value);
            while (values.Count > window)
                values.Dequeue();
            var sum = 0.0;
            foreach (var each in values)
                sum += each;
            return values.Count == window ? sum : 0;
        }

        private static double Sum(double[] values)
        {
            var total = 0.0;
            foreach (var value in values)
                total += value;
            return total;
        }
    }
}
