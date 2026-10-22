using System;
using System.IO;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;

namespace Game.SaveCheck
{
    public static class Program
    {
        private const string Usage = "save-check write <folder> | save-check check <folder>";

        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            var data = GameData.Load(name => File.ReadAllText(Path.Combine(FindDataFolder(), name)));
            var store = new SaveStore(args[1], backupRotationSeconds: 1);
            switch (args[0])
            {
                case "write":
                    WriteForever(data, store);
                    return 0;
                case "check":
                    return Check(data, store);
                default:
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        private static void WriteForever(GameData data, SaveStore store)
        {
            var previous = store.Load(text => SaveCodec.Decode(text, data));
            var state = previous.Save?.State ?? new GameState();
            var loop = new GameLoop(data, new BotInput(new BotProfile(6, 2, 800), (ulong)state.Tick + 1), state, new StatSheet());
            var clock = ClockStamp.First(Now());
            while (true)
            {
                loop.Tick();
                loop.Events.Clear();
                clock = clock.Next(Now());
                store.Write(SaveCodec.Encode(state, clock));
            }
        }

        private static int Check(GameData data, SaveStore store)
        {
            var result = store.Load(text => SaveCodec.Decode(text, data));
            foreach (var problem in result.Problems)
                Console.WriteLine("skipped " + problem);
            if (!result.Found)
            {
                Console.WriteLine("FAIL: no readable save in " + store.Directory);
                return 1;
            }
            Console.WriteLine($"ok: tick {result.Save!.State.Tick}, stage {result.Save.State.Stage} from {Path.GetFileName(result.Path)}");
            return 0;
        }

        private static ClockReading Now() =>
            new ClockReading(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Environment.TickCount64, 0);

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
            throw new DirectoryNotFoundException("Assets/StreamingAssets/Data not found above the current folder");
        }
    }
}
