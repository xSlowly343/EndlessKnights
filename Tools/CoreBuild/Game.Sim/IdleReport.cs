using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Sim.Headless;

namespace Game.Sim
{
    public static class IdleReport
    {
        private const double SampleSeconds = 600;

        public static int Run(GameData data, BotProfile profile, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var classes = new List<HeroClass>(options.Party!).ToArray();
            var seconds = options.Hours * 3600;
            var idleAfter = options.IdleAfterMinutes * 60;
            var alone = new PartySimulation(data, profile, new[] { classes[0] }, options.Seed, options.Loss, options.DamageMultiplier).Run(seconds);
            var party = new PartySimulation(data, profile, classes, options.Seed, options.Loss, options.DamageMultiplier)
            {
                IdleMembers = options.IdleMembers,
                IdleAfterSeconds = idleAfter,
                SampleSeconds = SampleSeconds
            }.Run(seconds);

            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            var first = classes.Length - options.IdleMembers;
            output.WriteLine("minute,kills,human_rewards,twink_rewards,twink_rewarded_share,twink_rest_bank_min,twink_damage_share");
            var previous = new PartySample(0, 0, new long[classes.Length], new double[classes.Length], new long[classes.Length]);
            foreach (var sample in party.Samples)
            {
                var kills = sample.HostKills - previous.HostKills;
                var twinkRewards = 0.0;
                var twinkShares = 0.0;
                var allShares = 0.0;
                var bank = 0.0;
                for (var i = 0; i < classes.Length; i++)
                {
                    var shares = sample.HostShares[i] - previous.HostShares[i];
                    allShares += shares;
                    if (i < first)
                        continue;
                    twinkRewards += sample.Rewards[i] - previous.Rewards[i];
                    twinkShares += shares;
                    bank += sample.RestBankSeconds[i];
                }
                var twinks = Math.Max(1, options.IdleMembers);
                output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0},{1},{2},{3:0.#},{4:0.##},{5:0.#},{6:0.###}",
                    sample.Seconds / 60, kills, sample.Rewards[0] - previous.Rewards[0], twinkRewards / twinks,
                    kills > 0 ? twinkRewards / twinks / kills : 0, bank / twinks / 60, allShares > 0 ? twinkShares / allShares : 0));
                previous = sample;
            }
            var last = party.Samples.Count > 0 ? party.Samples[party.Samples.Count - 1] : previous;
            var twinkTotal = 0L;
            for (var i = first; i < classes.Length; i++)
                twinkTotal += last.Rewards[i];
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "summary,human alone {0} kills stage {1},human with twinks {2} kills stage {3},twink rewards {4:0.##} of party kills",
                alone.HostKills, alone.Members[0].HighestStage, party.HostKills, party.Members[0].HighestStage,
                party.HostKills > 0 ? twinkTotal / (double)Math.Max(1, options.IdleMembers) / party.HostKills : 0));
            watch.Stop();
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "1 human + {0} twinks idle after {1:0} min, {2:0.##} h; simulated in {3:0.0} s",
                options.IdleMembers, options.IdleAfterMinutes, options.Hours, watch.Elapsed.TotalSeconds));
            return 0;
        }
    }
}
