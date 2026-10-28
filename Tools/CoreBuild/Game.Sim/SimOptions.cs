using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Sim;

namespace Game.Sim
{
    public sealed class SimOptions
    {
        public const string Usage =
            "sim [--kps 6] [--cps 0] [--mouse 0] [--hours 1] [--class viking|ronin|amazon|stormweaver|nomad|metalhead|oracle|bannerbearer|voodoo|clockmaker|plague_doctor|moonshiner|gravedigger|captain] [--seed 1] [--damage 1] [--stop-after-fails 0] [--days 0 [--active-hours 2] [--stuck-minutes 20]] [--party viking,ronin,... | standard [--loss 0]] [--supports <hours>] [--data <folder>] [--out <file.csv>]";

        public double KeysPerSecond { get; private set; } = 6;
        public double ClicksPerSecond { get; private set; }
        public double MousePxPerSecond { get; private set; }
        public double Hours { get; private set; } = 1;
        public HeroClass HeroClass { get; private set; } = HeroClass.Viking;
        public ulong Seed { get; private set; } = 1;
        public int StopAfterFails { get; private set; }
        public double DamageMultiplier { get; private set; } = 1;
        public int Days { get; private set; }
        public double ActiveHours { get; private set; } = 2;
        public double StuckMinutes { get; private set; } = Game.Core.Sim.Headless.PlayerSimulation.DefaultStuckMinutes;
        public IReadOnlyList<HeroClass>? Party { get; private set; }
        public bool StandardParties { get; private set; }
        public double Loss { get; private set; }
        public double SupportHours { get; private set; }
        public string? DataFolder { get; private set; }
        public string? OutFile { get; private set; }

        public static SimOptions Parse(IReadOnlyList<string> args)
        {
            var options = new SimOptions();
            for (var i = 0; i < args.Count; i += 2)
            {
                if (i + 1 >= args.Count)
                    throw new ArgumentException($"option {args[i]} needs a value");
                var value = args[i + 1];
                switch (args[i])
                {
                    case "--kps": options.KeysPerSecond = Number(value); break;
                    case "--cps": options.ClicksPerSecond = Number(value); break;
                    case "--mouse": options.MousePxPerSecond = Number(value); break;
                    case "--hours": options.Hours = Number(value); break;
                    case "--class": options.HeroClass = Class(value); break;
                    case "--seed": options.Seed = ulong.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--damage": options.DamageMultiplier = Number(value); break;
                    case "--stop-after-fails": options.StopAfterFails = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--days": options.Days = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--active-hours": options.ActiveHours = Number(value); break;
                    case "--stuck-minutes": options.StuckMinutes = Number(value); break;
                    case "--party": options.ReadParty(value); break;
                    case "--loss": options.Loss = Number(value); break;
                    case "--supports": options.SupportHours = Number(value); break;
                    case "--data": options.DataFolder = value; break;
                    case "--out": options.OutFile = value; break;
                    default: throw new ArgumentException($"unknown option {args[i]}");
                }
            }
            return options;
        }

        private void ReadParty(string value)
        {
            if (value == "standard")
            {
                StandardParties = true;
                return;
            }
            var classes = new List<HeroClass>();
            foreach (var name in value.Split(','))
                classes.Add(Class(name.Trim()));
            if (classes.Count < 1 || classes.Count > Game.Core.Party.PartyRules.MaxSlots)
                throw new ArgumentException($"a party has 1 to {Game.Core.Party.PartyRules.MaxSlots} members");
            Party = classes;
        }

        private static double Number(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static HeroClass Class(string value)
        {
            if (HeroClassNames.ByName.TryGetValue(value.ToLowerInvariant(), out var heroClass))
                return heroClass;
            throw new ArgumentException($"unknown class {value}, expected one of: {string.Join(", ", HeroClassNames.ByName.Keys)}");
        }
    }
}
