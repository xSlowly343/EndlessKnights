using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Content;
using Game.Core.Sim;
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
            if (options.SupportHours > 0)
                return RunSupports(data, options);
            if (options.Days > 0)
                return RunPlayer(data, profile, options);
            if (options.StandardParties || options.Party != null)
                return RunParties(data, profile, options);
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

        private const double SupportLow = 1.15;
        private const double SupportHigh = 1.40;

        private static readonly (string Name, BotProfile Profile)[] SupportProfiles =
        {
            ("typist", new BotProfile(8, 0, 300)),
            ("mouse", new BotProfile(1, 0, 1500)),
            ("idle", new BotProfile(0.5, 0, 100))
        };

        private static int RunSupports(GameData data, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var balance = new SupportBalance(data, options.Seed);
            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            output.WriteLine("support,color,typist,mouse,idle,average,verdict");
            var outside = 0;
            foreach (var gem in data.Gems.All)
            {
                if (gem.Kind != Game.Core.Content.Gems.GemKind.Support || gem.Support == null)
                    continue;
                var sum = 0.0;
                var cells = new List<string>();
                foreach (var (_, profile) in SupportProfiles)
                {
                    var multiplier = balance.Multiplier(gem, profile, options.SupportHours * 3600);
                    sum += multiplier;
                    cells.Add(multiplier.ToString("0.###", CultureInfo.InvariantCulture));
                }
                var average = sum / SupportProfiles.Length;
                var fits = average >= SupportLow && average <= SupportHigh;
                if (!fits)
                    outside++;
                output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:0.###},{4}",
                    gem.Id, gem.Color.ToString().ToLowerInvariant(), string.Join(",", cells), average, fits ? "ok" : "review"));
            }
            watch.Stop();
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "supports on {0} at stage {1}, {2:0.##} h per profile: {3} outside x{4:0.00}-x{5:0.00}; simulated in {6:0.00} s",
                SupportBalance.HostSkill, SupportBalance.Stage, options.SupportHours, outside, SupportLow, SupportHigh, watch.Elapsed.TotalSeconds));
            return 0;
        }

        private static readonly (string Name, HeroClass[] Classes)[] StandardParties =
        {
            ("solo", new[] { HeroClass.Viking }),
            ("4 dd", new[] { HeroClass.Viking, HeroClass.Ronin, HeroClass.Amazon, HeroClass.Stormweaver }),
            ("2 dd + 2 support", new[] { HeroClass.Viking, HeroClass.Ronin, HeroClass.Metalhead, HeroClass.Voodoo })
        };

        private static int RunParties(GameData data, BotProfile profile, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var parties = options.StandardParties
                ? StandardParties
                : new[] { ("custom", new List<HeroClass>(options.Party!).ToArray()) };
            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            output.WriteLine("scenario,class,final_stage,highest_stage,kills,boss_fails,party_kill_share,gold_earned,party_seconds_per_kill");
            foreach (var (name, classes) in parties)
            {
                var result = new PartySimulation(data, profile, classes, options.Seed, options.Loss).Run(options.Hours * 3600);
                foreach (var member in result.Members)
                {
                    output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6:0.###},{7},{8:0.###}",
                        name, HeroClassNames.Of(member.HeroClass), member.FinalStage, member.HighestStage, member.Kills, member.BossFails,
                        member.ShareOfKills, StageRecord.Number(member.GoldEarned), result.SecondsPerKill));
                }
            }
            watch.Stop();
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} parties x {1:0.##} h at {2:0.#} keys/s, loss {3:0.##}; simulated in {4:0.00} s",
                parties.Length, options.Hours, options.KeysPerSecond, options.Loss, watch.Elapsed.TotalSeconds));
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
