using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Sim;

namespace Game.Sim
{
    public sealed class SimOptions
    {
        public const string Usage =
            "sim [--kps 6] [--cps 0] [--mouse 0] [--hours 1] [--class typist|bard] [--seed 1] [--stop-after-fails 0] [--data <folder>] [--out <file.csv>]";

        public double KeysPerSecond { get; private set; } = 6;
        public double ClicksPerSecond { get; private set; }
        public double MousePxPerSecond { get; private set; }
        public double Hours { get; private set; } = 1;
        public HeroClass HeroClass { get; private set; } = HeroClass.Typist;
        public ulong Seed { get; private set; } = 1;
        public int StopAfterFails { get; private set; }
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
                    case "--stop-after-fails": options.StopAfterFails = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--data": options.DataFolder = value; break;
                    case "--out": options.OutFile = value; break;
                    default: throw new ArgumentException($"unknown option {args[i]}");
                }
            }
            return options;
        }

        private static double Number(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static HeroClass Class(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "typist": return HeroClass.Typist;
                case "bard": return HeroClass.Bard;
                default: throw new ArgumentException($"unknown class {value}, expected typist or bard");
            }
        }
    }
}
