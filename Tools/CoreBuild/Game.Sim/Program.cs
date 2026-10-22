using System;
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
