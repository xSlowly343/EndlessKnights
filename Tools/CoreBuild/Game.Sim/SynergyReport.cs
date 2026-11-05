using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Content.Synergy;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Sim.Headless;

namespace Game.Sim
{
    public static class SynergyReport
    {
        private const int RandomParties = 12;

        private static readonly (string Name, HeroClass[] Classes)[] Combos =
        {
            ("poison_fire_lightning", new[] { HeroClass.Alchemist, HeroClass.Dowser, HeroClass.Shardcaller, HeroClass.Sandwalker }),
            ("army", new[] { HeroClass.Soulbinder, HeroClass.Whisperer, HeroClass.Windshot, HeroClass.Sandwalker }),
            ("stun_and_finish", new[] { HeroClass.Runedancer, HeroClass.Lanternbearer, HeroClass.Whisperer, HeroClass.Sandwalker }),
            ("time_and_series", new[] { HeroClass.Duelist, HeroClass.Clockwright, HeroClass.Minstrel, HeroClass.Sandwalker })
        };

        public static int Run(GameData data, string dataFolder, BotProfile profile, SimOptions options)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var synergyJson = File.ReadAllText(Path.Combine(dataFolder, SynergyRules.FileName));
            var off = SynergyRules.Load(Strip(synergyJson, null));
            var kits = Kits(data);
            var seconds = options.SynergyHours * 3600;
            using var file = options.OutFile == null ? null : new StreamWriter(options.OutFile);
            var output = file ?? Console.Out;
            output.WriteLine("scenario,classes,uplift,damage_on,damage_off,stage_on,stage_off,reactions,constellations,responses");

            var parties = new List<(string Name, HeroClass[] Classes)>(Combos);
            var random = new Random((int)options.Seed);
            for (var i = 0; i < RandomParties; i++)
                parties.Add(("random_" + (i + 1), RandomParty(random)));
            parties.AddRange(Roster());

            var seen = new List<PartyMemberResult>();
            var comboUplift = new List<double>();
            var randomUplift = new List<double>();
            foreach (var (name, classes) in parties)
            {
                var on = Party(data, profile, classes, options, kits, null).Run(seconds);
                seen.AddRange(on.Members);
                var without = Party(data, profile, classes, options, kits, off).Run(seconds);
                var uplift = Ratio(Damage(on), Damage(without)) - 1;
                (name.StartsWith("random_", StringComparison.Ordinal) || name.StartsWith("roster_", StringComparison.Ordinal) ? randomUplift : comboUplift).Add(uplift);
                output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                    name, Names(classes), Percent(uplift), StageRecord.Number(Damage(on)), StageRecord.Number(Damage(without)), Stage(on), Stage(without),
                    Reactions(on), Constellations(on), Responses(on)));
            }
            output.WriteLine(string.Format(CultureInfo.InvariantCulture, "summary,combos {0},random {1},difference {2}",
                Percent(Average(comboUplift)), Percent(Average(randomUplift)), Percent(Average(comboUplift) - Average(randomUplift))));

            WriteMarks(output, data.Synergy, seen);
            WriteUnseen(output, data.Synergy, seen);

            if (options.SynergyDrops)
            {
                output.WriteLine("drop,reaction,combo_uplift_without");
                var rules = data.Synergy;
                foreach (var reaction in rules.Reactions)
                {
                    var without = SynergyRules.Load(Strip(synergyJson, reaction.Id));
                    var combo = new List<double>();
                    foreach (var (_, classes) in Combos)
                    {
                        var on = Party(data, profile, classes, options, kits, without).Run(seconds);
                        var none = Party(data, profile, classes, options, kits, off).Run(seconds);
                        combo.Add(Ratio(Damage(on), Damage(none)) - 1);
                    }
                    output.WriteLine(string.Format(CultureInfo.InvariantCulture, "drop,{0},{1}", reaction.Id, Percent(Average(combo))));
                }
            }
            watch.Stop();
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} parties x {1:0.##} h on and off; simulated in {2:0.0} s", parties.Count, options.SynergyHours, watch.Elapsed.TotalSeconds));
            return 0;
        }

        private static PartySimulation Party(GameData data, BotProfile profile, HeroClass[] classes, SimOptions options,
            IReadOnlyDictionary<HeroClass, IReadOnlyList<GemDef>> kits, SynergyRules? synergy) =>
            new PartySimulation(data, profile, classes, options.Seed, options.Loss, options.DamageMultiplier) { Kits = kits, Synergy = synergy, HeroLevel = options.HeroLevel };

        private static string Strip(string json, string? reaction)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            if (reaction == null)
            {
                root["responses"] = new JsonArray();
                root["reactions"] = new JsonArray();
                root["constellations"] = new JsonArray();
                return root.ToJsonString();
            }
            var reactions = root["reactions"]!.AsArray();
            for (var i = reactions.Count - 1; i >= 0; i--)
            {
                if ((string?)reactions[i]!["id"] == reaction)
                    reactions.RemoveAt(i);
            }
            return root.ToJsonString();
        }

        private static IReadOnlyDictionary<HeroClass, IReadOnlyList<GemDef>> Kits(GameData data)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "party-kits.json");
            var kits = new Dictionary<HeroClass, IReadOnlyList<GemDef>>();
            foreach (var (name, ids) in JsonNode.Parse(File.ReadAllText(path))!.AsObject())
            {
                if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                    throw new ArgumentException($"party-kits.json: unknown class '{name}'");
                var gems = new List<GemDef>();
                foreach (var id in ids!.AsArray())
                    gems.Add(data.Gems[(string)id!]);
                kits[heroClass] = gems;
            }
            return kits;
        }

        private static IEnumerable<(string Name, HeroClass[] Classes)> Roster()
        {
            var all = new List<HeroClass>(HeroClassNames.ByName.Values);
            for (var first = 0; first < all.Count; first += 4)
            {
                var classes = new HeroClass[4];
                for (var i = 0; i < classes.Length; i++)
                    classes[i] = all[(first + i) % all.Count];
                yield return ("roster_" + (first / 4 + 1), classes);
            }
        }

        private static HeroClass[] RandomParty(Random random)
        {
            var all = new List<HeroClass>(HeroClassNames.ByName.Values);
            var picked = new List<HeroClass>();
            while (picked.Count < 4)
            {
                var heroClass = all[random.Next(all.Count)];
                if (!picked.Contains(heroClass))
                    picked.Add(heroClass);
            }
            return picked.ToArray();
        }

        private static BigNumber Damage(PartySimulationResult result)
        {
            var total = BigNumber.Zero;
            foreach (var member in result.Members)
                total += member.Damage;
            return total;
        }

        private static double Ratio(BigNumber a, BigNumber b) => b.Sign <= 0 ? 1 : System.Math.Pow(10, a.Log10() - b.Log10());

        private static long Stage(PartySimulationResult result)
        {
            var stage = 0L;
            foreach (var member in result.Members)
                stage = System.Math.Max(stage, member.HighestStage);
            return stage;
        }

        private static string Names(HeroClass[] classes)
        {
            var names = new string[classes.Length];
            for (var i = 0; i < classes.Length; i++)
                names[i] = HeroClassNames.Of(classes[i]);
            return string.Join("+", names);
        }

        private static string Reactions(PartySimulationResult result)
        {
            var uptime = result.Members[0].Synergy!;
            var parts = new List<string>();
            for (var i = 0; i < uptime.Rules.Reactions.Count; i++)
            {
                var best = 0.0;
                foreach (var member in result.Members)
                    best = System.Math.Max(best, member.Synergy!.Reaction(i));
                if (best > 0)
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0%}", uptime.Rules.Reactions[i].Id, best));
            }
            return parts.Count == 0 ? "-" : string.Join(" ", parts);
        }

        private static string Constellations(PartySimulationResult result)
        {
            var uptime = result.Members[0].Synergy!;
            var parts = new List<string>();
            for (var i = 0; i < uptime.Rules.Constellations.Count; i++)
            {
                if (uptime.Constellation(i) > 0)
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0%}", uptime.Rules.Constellations[i].Id, uptime.Constellation(i)));
            }
            return parts.Count == 0 ? "-" : string.Join(" ", parts);
        }

        private static string Responses(PartySimulationResult result)
        {
            var parts = new List<string>();
            foreach (var member in result.Members)
            {
                var uptime = member.Synergy!;
                for (var i = 0; i < uptime.Rules.Responses.Count; i++)
                {
                    if (uptime.Response(i) > 0)
                        parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0%}", uptime.Rules.Responses[i].Id, uptime.Response(i)));
                }
            }
            return parts.Count == 0 ? "-" : string.Join(" ", parts);
        }

        private static void WriteMarks(TextWriter output, SynergyRules rules, List<PartyMemberResult> members)
        {
            output.WriteLine("marks,class,own_mark_uptime");
            foreach (var heroClass in HeroClassNames.ByName.Values)
            {
                var parts = new List<string>();
                var runs = 0;
                var uptime = new double[rules.Marks.Count];
                foreach (var member in members)
                {
                    if (member.HeroClass != heroClass)
                        continue;
                    runs++;
                    for (var mark = 0; mark < uptime.Length; mark++)
                        uptime[mark] += member.Synergy!.OwnMark(mark);
                }
                if (runs == 0)
                    continue;
                for (var mark = 0; mark < uptime.Length; mark++)
                {
                    if (uptime[mark] > 0)
                        parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0.#%}", rules.Marks[mark], uptime[mark] / runs));
                }
                output.WriteLine("marks,{0},{1}", HeroClassNames.Of(heroClass), parts.Count == 0 ? "-" : string.Join(" ", parts));
            }
        }

        private static void WriteUnseen(TextWriter output, SynergyRules rules, List<PartyMemberResult> members)
        {
            var marks = new List<string>();
            for (var mark = 0; mark < rules.Marks.Count; mark++)
            {
                if (!members.Exists(member => member.Synergy!.OwnMark(mark) > 0))
                    marks.Add(rules.Marks[mark]);
            }
            var responses = new List<string>();
            for (var i = 0; i < rules.Responses.Count; i++)
            {
                if (!members.Exists(member => member.Synergy!.Response(i) > 0))
                    responses.Add(rules.Responses[i].Id);
            }
            var reactions = new List<string>();
            for (var i = 0; i < rules.Reactions.Count; i++)
            {
                if (!members.Exists(member => member.Synergy!.Reaction(i) > 0))
                    reactions.Add(rules.Reactions[i].Id);
            }
            output.WriteLine("unseen,marks,{0}", marks.Count == 0 ? "-" : string.Join(" ", marks));
            output.WriteLine("unseen,responses,{0}", responses.Count == 0 ? "-" : string.Join(" ", responses));
            output.WriteLine("unseen,reactions,{0}", reactions.Count == 0 ? "-" : string.Join(" ", reactions));
        }

        private static string Percent(double share) => (share * 100).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";

        private static double Average(List<double> values)
        {
            var sum = 0.0;
            foreach (var value in values)
                sum += value;
            return values.Count > 0 ? sum / values.Count : 0;
        }
    }
}
