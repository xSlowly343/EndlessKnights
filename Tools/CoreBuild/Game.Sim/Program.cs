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
            ("mouse", new BotProfile(1, 0, 1500, 0.4, 0.3, 0.3)),
            ("idle", new BotProfile(0.5, 0, 100))
        };

        private static int RunSupports(GameData data, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var hosts = new[]
            {
                new SupportBalance(data, options.Seed, SupportBalance.FastHost),
                new SupportBalance(data, options.Seed, SupportBalance.HeavyHost),
                new SupportBalance(data, options.Seed, SupportBalance.DurationHost),
                new SupportBalance(data, options.Seed, SupportBalance.MouseHost),
                new SupportBalance(data, options.Seed, SupportBalance.SummonHost),
                new SupportBalance(data, options.Seed, SupportBalance.PoisonHost)
            };
            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            output.WriteLine("support,color,fast_typist,fast_mouse,fast_idle,fast_average,heavy_typist,heavy_mouse,heavy_idle,heavy_average,duration_typist,duration_mouse,duration_idle,duration_average,mouse_typist,mouse_mouse,mouse_idle,mouse_average,summon_typist,summon_mouse,summon_idle,summon_average,poison_typist,poison_mouse,poison_idle,poison_average,best,verdict");
            var outside = 0;
            foreach (var gem in data.Gems.All)
            {
                if (gem.Kind != Game.Core.Content.Gems.GemKind.Support || gem.Support == null)
                    continue;
                var cells = new List<string>();
                var best = 0.0;
                foreach (var host in hosts)
                {
                    var sum = 0.0;
                    foreach (var (_, profile) in SupportProfiles)
                    {
                        var multiplier = host.Multiplier(gem, profile, options.SupportHours * 3600);
                        sum += multiplier;
                        cells.Add(multiplier.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                    var average = sum / SupportProfiles.Length;
                    cells.Add(average.ToString("0.###", CultureInfo.InvariantCulture));
                    best = System.Math.Max(best, average);
                }
                var verdict = Verdict(gem.Support, best);
                if (verdict == "review")
                    outside++;
                output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:0.###},{4}",
                    gem.Id, gem.Color.ToString().ToLowerInvariant(), string.Join(",", cells), best, verdict));
            }
            watch.Stop();
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "supports on {0}, {1}, {8}, {9}, {10} and {11} at stage {2}, {3:0.##} h per profile: {4} outside x{5:0.00}-x{6:0.00}; simulated in {7:0.00} s",
                SupportBalance.FastHost, SupportBalance.HeavyHost, SupportBalance.Stage, options.SupportHours, outside, SupportLow, SupportHigh, watch.Elapsed.TotalSeconds, SupportBalance.DurationHost, SupportBalance.MouseHost, SupportBalance.SummonHost, SupportBalance.PoisonHost));
            return 0;
        }

        private static string Verdict(Game.Core.Content.Gems.SupportDef support, double best)
        {
            if (support.ExperienceMultiplier != 1 || support.KillRewardMultiplier != 1)
                return "utility";
            if (support.VulnerabilityShare > 0 || support.PartyPower != 1)
                return "party";
            if (support.QuietSeconds > 0 || support.CirclesForPower > 0)
                return "conditional";
            return best >= SupportLow && best <= SupportHigh ? "ok" : "review";
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
