using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Content;
using Game.Core.Sim.Headless;

namespace Game.Sim
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            SimOptions options;
            try
            {
                options = SimOptions.Parse(args);
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                Console.Error.WriteLine(error.Message);
                Console.Error.WriteLine(SimOptions.Usage);
                return 2;
            }

            var folder = options.DataFolder ?? FindDataFolder();
            GameData data;
            try
            {
                data = GameData.Load(name => File.ReadAllText(Path.Combine(folder, name)));
            }
            catch (ContentException error)
            {
                Console.Error.WriteLine(error.Message);
                return 1;
            }

            var profile = new BotProfile(options.KeysPerSecond, options.ClicksPerSecond, options.MousePxPerSecond);
            if (options.Days > 0)
                return RunPlayer(data, profile, options);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = new Simulation(data, profile, options.HeroClass, options.Seed, options.DamageMultiplier).Run(options.Hours * 3600, options.StopAfterFails);
            watch.Stop();

            if (options.OutFile == null)
            {
                WriteCsv(Console.Out, result);
            }
            else
            {
                using var file = new StreamWriter(options.OutFile);
                WriteCsv(file, result);
            }

            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.##} h, {1} at {2:0.#} keys/s, {3:0.#} clicks/s, {4:0} px/s: stage {5} (best {6}), gold earned {7}, boss fails {8}{9}; simulated in {10:0.00} s",
                result.Seconds / 3600, options.HeroClass, options.KeysPerSecond, options.ClicksPerSecond, options.MousePxPerSecond,
                result.FinalStage, result.HighestStage, StageRecord.Number(result.GoldEarned), result.BossFails, result.Stuck ? ", stuck" : string.Empty,
                watch.Elapsed.TotalSeconds));
            return 0;
        }

        private static int RunPlayer(GameData data, BotProfile profile, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var days = new PlayerSimulation(data, profile, options.HeroClass, options.Seed, options.StuckMinutes).Run(options.Days, options.ActiveHours);
            watch.Stop();

            if (options.OutFile == null)
            {
                WriteDays(Console.Out, days);
            }
            else
            {
                using var file = new StreamWriter(options.OutFile);
                WriteDays(file, days);
            }

            var last = days[days.Count - 1];
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} days x {1:0.##} h active, {2} at {3:0.#} keys/s, {4:0.#} clicks/s: record stage {5}, level {6}, prestiges {7}, artifacts {8}; simulated in {9:0.00} s",
                days.Count, options.ActiveHours, options.HeroClass, options.KeysPerSecond, options.ClicksPerSecond,
                last.RecordStage, last.HeroLevel, last.Prestiges, last.Artifacts, watch.Elapsed.TotalSeconds));
            return 0;
        }

        private static void WriteDays(TextWriter output, IReadOnlyList<DayRecord> days)
        {
            output.WriteLine(DayRecord.CsvHeader);
            foreach (var day in days)
                output.WriteLine(day.ToCsv());
        }

        private static void WriteCsv(TextWriter output, SimulationResult result)
        {
            output.WriteLine(StageRecord.CsvHeader);
            foreach (var stage in result.Stages)
                output.WriteLine(stage.ToCsv());
        }

        private static string FindDataFolder()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "Data");
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Assets/StreamingAssets/Data not found above the current folder, pass --data");
        }
    }
}
