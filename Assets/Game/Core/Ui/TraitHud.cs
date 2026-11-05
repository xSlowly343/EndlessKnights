using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public enum TraitHudKind
    {
        Count,
        Bar,
        Timer,
        Flag
    }

    public struct TraitHudValue
    {
        public TraitHudKind Kind;
        public string Key;
        public string Label;
        public double Value;
        public double Max;

        public double Fill => Kind == TraitHudKind.Flag ? Value : Max > 0 ? System.Math.Min(1, System.Math.Max(0, Value / Max)) : 0;
    }

    public sealed class TraitHudLine
    {
        public TraitHudLine(string key, TraitHudKind kind, Func<GameState, CombatSystem, ClassRules, double> value, Func<GameState, CombatSystem, ClassRules, double> max)
        {
            Key = key;
            Kind = kind;
            Value = value;
            Max = max;
            LabelKey = "trait.hud." + key;
        }

        public string Key { get; }
        public TraitHudKind Kind { get; }
        public Func<GameState, CombatSystem, ClassRules, double> Value { get; }
        public Func<GameState, CombatSystem, ClassRules, double> Max { get; }
        public string LabelKey { get; }
    }

    public static class TraitHud
    {
        public const int MaxLines = 3;

        private static readonly Dictionary<string, TraitHudLine[]> Lines = new Dictionary<string, TraitHudLine[]>(StringComparer.Ordinal)
        {
            ["runedancer_trait_1"] = new[]
            {
                Line("runes", TraitHudKind.Count, (s, c, r) => s.Runedancer.Runes, (s, c, r) => c.Runedancer.WordLength),
                Line("seal", TraitHudKind.Timer, (s, c, r) => s.Runedancer.SealTicks * GameLoop.TickSeconds, (s, c, r) => r.Runedancer.SealSeconds)
            },
            ["runedancer_trait_2"] = new[]
            {
                Line("blades", TraitHudKind.Count, (s, c, r) => s.Runedancer.Blades, (s, c, r) => c.Runedancer.BladeMax)
            },
            ["runedancer_trait_3"] = new[]
            {
                Line("whirl", TraitHudKind.Count, (s, c, r) => s.Runedancer.WhirlBlades, (s, c, r) => c.Runedancer.WhirlMax),
                Line("whirlFull", TraitHudKind.Flag, (s, c, r) => s.Runedancer.WhirlFull ? 1 : 0, (s, c, r) => 1)
            },
            ["duelist_trait_1"] = new[]
            {
                Line("breach", TraitHudKind.Flag, (s, c, r) => s.Duelist.BreachOpen ? 1 : 0, (s, c, r) => 1),
                Line("lunges", TraitHudKind.Count, (s, c, r) => s.Duelist.LungesInBreach, (s, c, r) => 0)
            },
            ["duelist_trait_2"] = new[]
            {
                Line("marks", TraitHudKind.Count, (s, c, r) => s.Duelist.Marks, (s, c, r) => c.Duelist.MarksNeeded),
                Line("touch", TraitHudKind.Count, (s, c, r) => s.Duelist.TouchProgress, (s, c, r) => c.Duelist.TouchEvery)
            },
            ["duelist_trait_3"] = new[]
            {
                Line("phantom", TraitHudKind.Flag, (s, c, r) => s.Duelist.PhantomFighting ? 1 : 0, (s, c, r) => 1),
                Line("pace", TraitHudKind.Count, (s, c, r) => s.Duelist.HitsLastSecond, (s, c, r) => c.Duelist.PhantomThreshold)
            },
            ["windshot_trait_1"] = new[]
            {
                Line("shot", TraitHudKind.Count, (s, c, r) => s.Windshot.ShotProgress, (s, c, r) => c.Windshot.ShotEvery),
                Line("tension", TraitHudKind.Bar, (s, c, r) => s.ResourceFill, (s, c, r) => 1)
            },
            ["windshot_trait_2"] = new[]
            {
                Line("calm", TraitHudKind.Flag, (s, c, r) => s.Windshot.Calm ? 1 : 0, (s, c, r) => 1),
                Line("still", TraitHudKind.Timer, (s, c, r) => s.Windshot.StillTicks * GameLoop.TickSeconds, (s, c, r) => c.Windshot.StillTicksNeeded * GameLoop.TickSeconds)
            },
            ["windshot_trait_3"] = new[]
            {
                Line("gusts", TraitHudKind.Count, (s, c, r) => s.Windshot.Gusts, (s, c, r) => c.Windshot.GustMax),
                Line("gustPx", TraitHudKind.Bar, (s, c, r) => s.Windshot.GustPxCarry, (s, c, r) => c.Windshot.GustPxNeeded),
                Line("gustHits", TraitHudKind.Count, (s, c, r) => s.Windshot.GustHitsLeft, (s, c, r) => 0)
            },
            ["shardcaller_trait_1"] = new[]
            {
                Line("crystals", TraitHudKind.Count, (s, c, r) => s.Shardcaller.Crystals, (s, c, r) => c.Shardcaller.CrystalMax),
                Line("shardHits", TraitHudKind.Count, (s, c, r) => s.Shardcaller.StrikeProgress, (s, c, r) => c.Shardcaller.StrikesPerCrystal)
            },
            ["shardcaller_trait_2"] = new[]
            {
                Line("crystals", TraitHudKind.Count, (s, c, r) => s.Shardcaller.Crystals, (s, c, r) => c.Shardcaller.CrystalMax),
                Line("size", TraitHudKind.Count, (s, c, r) => s.Shardcaller.Size, (s, c, r) => c.Shardcaller.SizeMax),
                Line("grow", TraitHudKind.Timer, (s, c, r) => (c.Shardcaller.GrowTicks - s.Shardcaller.GrowTicks) * GameLoop.TickSeconds, (s, c, r) => c.Shardcaller.GrowTicks * GameLoop.TickSeconds)
            },
            ["shardcaller_trait_3"] = new[]
            {
                Line("cracked", TraitHudKind.Flag, (s, c, r) => s.Shardcaller.Cracked ? 1 : 0, (s, c, r) => 1),
                Line("crack", TraitHudKind.Timer, (s, c, r) => (c.Shardcaller.ShellEveryTicks - s.Shardcaller.ShellTicks) * GameLoop.TickSeconds, (s, c, r) => c.Shardcaller.ShellEveryTicks * GameLoop.TickSeconds)
            },
            ["sandwalker_trait_1"] = new[]
            {
                Line("bottom", TraitHudKind.Flag, (s, c, r) => s.Sandwalker.Bottom ? 1 : 0, (s, c, r) => 1),
                Line("grains", TraitHudKind.Count, (s, c, r) => s.Sandwalker.Grains, (s, c, r) => c.Sandwalker.FlipEvery)
            },
            ["sandwalker_trait_2"] = new[]
            {
                Line("road", TraitHudKind.Bar, (s, c, r) => s.Sandwalker.RoadPx, (s, c, r) => c.Sandwalker.CampPx)
            },
            ["sandwalker_trait_3"] = new[]
            {
                Line("mirages", TraitHudKind.Count, (s, c, r) => s.Sandwalker.Mirages, (s, c, r) => c.Sandwalker.MirageMax),
                Line("haze", TraitHudKind.Bar, (s, c, r) => s.Sandwalker.HazePx, (s, c, r) => c.Sandwalker.MiragePx)
            },
            ["minstrel_trait_1"] = new[]
            {
                Line("verses", TraitHudKind.Count, (s, c, r) => s.Minstrel.Verses, (s, c, r) => c.Minstrel.VerseMax),
                Line("lines", TraitHudKind.Count, (s, c, r) => s.Minstrel.Lines, (s, c, r) => r.Minstrel.LinesPerVerse),
                Line("chorus", TraitHudKind.Timer, (s, c, r) => s.Minstrel.ChorusTicks * GameLoop.TickSeconds, (s, c, r) => r.Minstrel.ChorusSeconds)
            },
            ["minstrel_trait_2"] = new[]
            {
                Line("melody", TraitHudKind.Count, (s, c, r) => s.Minstrel.Melody + 1, (s, c, r) => MinstrelState.MelodyCount),
                Line("songHits", TraitHudKind.Count, (s, c, r) => s.Minstrel.SongHits, (s, c, r) => c.Minstrel.SongHits)
            },
            ["minstrel_trait_3"] = new[]
            {
                Line("notes", TraitHudKind.Count, (s, c, r) => s.Minstrel.Notes, (s, c, r) => c.Minstrel.SleepNotes),
                Line("sleep", TraitHudKind.Timer, (s, c, r) => s.Minstrel.SleepTicks * GameLoop.TickSeconds, (s, c, r) => c.Minstrel.SleepTicks * GameLoop.TickSeconds)
            },
            ["dreamcatcher_trait_1"] = new[]
            {
                Line("nets", TraitHudKind.Count, (s, c, r) => s.Dreamcatcher.Nets, (s, c, r) => c.Dreamcatcher.NetMax),
                Line("netPx", TraitHudKind.Bar, (s, c, r) => s.Dreamcatcher.NetPx, (s, c, r) => c.Dreamcatcher.NetPx)
            },
            ["dreamcatcher_trait_2"] = new[]
            {
                Line("album", TraitHudKind.Count, (s, c, r) => (s.Dreamcatcher.EnemyIndex >= 0 ? s.Dreamcatcher.Album[s.Dreamcatcher.EnemyIndex] : 0), (s, c, r) => c.Dreamcatcher.PageMin),
                Line("prophecy", TraitHudKind.Timer, (s, c, r) => s.Dreamcatcher.ProphecyTicks * GameLoop.TickSeconds, (s, c, r) => r.Dreamcatcher.ProphecySeconds)
            },
            ["dreamcatcher_trait_3"] = new[]
            {
                Line("bait", TraitHudKind.Count, (s, c, r) => s.Dreamcatcher.Bait, (s, c, r) => c.Dreamcatcher.BaitHits),
                Line("spirit", TraitHudKind.Flag, (s, c, r) => s.Dreamcatcher.SpiritAlive ? 1 : 0, (s, c, r) => 1)
            },
            ["lanternbearer_trait_1"] = new[]
            {
                Line("light", TraitHudKind.Bar, (s, c, r) => s.Lanternbearer.Light, (s, c, r) => 1),
                Line("shadow", TraitHudKind.Timer, (s, c, r) => (c.Lanternbearer.ShadowTicks - s.Lanternbearer.ShadowTicks) * GameLoop.TickSeconds, (s, c, r) => c.Lanternbearer.ShadowTicks * GameLoop.TickSeconds)
            },
            ["lanternbearer_trait_2"] = new[]
            {
                Line("fireflies", TraitHudKind.Count, (s, c, r) => s.Lanternbearer.Fireflies, (s, c, r) => c.Lanternbearer.FireflyMax),
                Line("fireflyHits", TraitHudKind.Count, (s, c, r) => s.Lanternbearer.FireflyHits, (s, c, r) => c.Lanternbearer.FireflyHits)
            },
            ["lanternbearer_trait_3"] = new[]
            {
                Line("beacon", TraitHudKind.Timer, (s, c, r) => (c.Lanternbearer.BeaconTicks - s.Lanternbearer.BeaconTicks) * GameLoop.TickSeconds, (s, c, r) => c.Lanternbearer.BeaconTicks * GameLoop.TickSeconds)
            },
            ["whisperer_trait_1"] = new[]
            {
                Line("knots", TraitHudKind.Count, (s, c, r) => s.Whisperer.Knots, (s, c, r) => WhispererState.MaxKnots),
                Line("knotHits", TraitHudKind.Count, (s, c, r) => s.Whisperer.KnotHits, (s, c, r) => c.Whisperer.KnotHits)
            },
            ["whisperer_trait_2"] = new[]
            {
                Line("name", TraitHudKind.Count, (s, c, r) => s.Whisperer.BossHits, (s, c, r) => c.Whisperer.NameHits),
                Line("named", TraitHudKind.Flag, (s, c, r) => s.Whisperer.Named ? 1 : 0, (s, c, r) => 1)
            },
            ["whisperer_trait_3"] = new[]
            {
                Line("hush", TraitHudKind.Count, (s, c, r) => s.Whisperer.SilentHits, (s, c, r) => c.Whisperer.HushHits),
                Line("hushes", TraitHudKind.Count, (s, c, r) => s.Boons.Hushes, (s, c, r) => r.Whisperer.HushMax),
                Line("curse", TraitHudKind.Timer, (s, c, r) => s.Whisperer.CurseTicks * GameLoop.TickSeconds, (s, c, r) => r.Whisperer.CurseSeconds)
            },
            ["clockwright_trait_1"] = new[]
            {
                Line("wind", TraitHudKind.Bar, (s, c, r) => c.Clockwright.Wind, (s, c, r) => 1)
            },
            ["clockwright_trait_2"] = new[]
            {
                Line("loop", TraitHudKind.Timer, (s, c, r) => (c.Clockwright.LoopTicks - s.Clockwright.LoopTicks) * GameLoop.TickSeconds, (s, c, r) => c.Clockwright.LoopTicks * GameLoop.TickSeconds)
            },
            ["clockwright_trait_3"] = new[]
            {
                Line("gear", TraitHudKind.Count, (s, c, r) => s.Clockwright.GearHits, (s, c, r) => c.Clockwright.GearHits)
            },
            ["alchemist_trait_1"] = new[]
            {
                Line("flask", TraitHudKind.Count, (s, c, r) => s.Alchemist.InFlask, (s, c, r) => AlchemistState.FlaskSize),
                Line("reagent", TraitHudKind.Count, (s, c, r) => s.Alchemist.ReagentHits, (s, c, r) => c.Alchemist.ReagentHits)
            },
            ["alchemist_trait_2"] = new[]
            {
                Line("layers", TraitHudKind.Count, (s, c, r) => c.Alchemist.LayersOff, (s, c, r) => r.Alchemist.Layers),
                Line("layer", TraitHudKind.Bar, (s, c, r) => s.Alchemist.LayerProgress, (s, c, r) => c.Alchemist.LayerHits)
            },
            ["alchemist_trait_3"] = new[]
            {
                Line("elixir", TraitHudKind.Count, (s, c, r) => s.Alchemist.ElixirHits, (s, c, r) => c.Alchemist.ElixirHits)
            },
            ["dowser_trait_1"] = new[]
            {
                Line("vein", TraitHudKind.Bar, (s, c, r) => c.Dowser.VeinCloseness, (s, c, r) => 1)
            },
            ["dowser_trait_2"] = new[]
            {
                Line("luck", TraitHudKind.Bar, (s, c, r) => c.Dowser.LuckChance, (s, c, r) => r.Dowser.LuckMax)
            },
            ["dowser_trait_3"] = new[]
            {
                Line("nuggets", TraitHudKind.Count, (s, c, r) => s.Dowser.Nuggets, (s, c, r) => c.Dowser.ArtelNuggets)
            },
            ["soulbinder_trait_1"] = new[]
            {
                Line("souls", TraitHudKind.Count, (s, c, r) => s.Soulbinder.Souls, (s, c, r) => c.Soulbinder.SphereSize)
            },
            ["soulbinder_trait_2"] = new[]
            {
                Line("souls", TraitHudKind.Count, (s, c, r) => s.Soulbinder.Souls, (s, c, r) => c.Soulbinder.SphereSize),
                Line("cold", TraitHudKind.Timer, (s, c, r) => s.Soulbinder.ColdTicks * GameLoop.TickSeconds, (s, c, r) => r.Soulbinder.ColdSeconds)
            },
            ["soulbinder_trait_3"] = new[]
            {
                Line("lend", TraitHudKind.Count, (s, c, r) => s.Soulbinder.LendHits, (s, c, r) => c.Soulbinder.LendHits)
            },
            ["ghostcaptain_trait_1"] = new[]
            {
                Line("cannons", TraitHudKind.Count, (s, c, r) => s.Ghostcaptain.Loaded, (s, c, r) => c.Ghostcaptain.Cannons),
                Line("load", TraitHudKind.Count, (s, c, r) => s.Ghostcaptain.LoadHits, (s, c, r) => c.Ghostcaptain.CannonHits),
                Line("hole", TraitHudKind.Timer, (s, c, r) => s.Ghostcaptain.BreachTicks * GameLoop.TickSeconds, (s, c, r) => r.Ghostcaptain.BreachSeconds)
            },
            ["ghostcaptain_trait_2"] = new[]
            {
                Line("sailors", TraitHudKind.Count, (s, c, r) => s.Ghostcaptain.Sailors, (s, c, r) => 0),
                Line("dawn", TraitHudKind.Timer, (s, c, r) => s.Ghostcaptain.DawnTicks * GameLoop.TickSeconds, (s, c, r) => c.Ghostcaptain.DawnSeconds)
            },
            ["ghostcaptain_trait_3"] = new[]
            {
                Line("parrot", TraitHudKind.Count, (s, c, r) => s.Ghostcaptain.ParrotCasts, (s, c, r) => c.Ghostcaptain.ParrotEvery)
            },
            ["mothcaller_trait_1"] = new[]
            {
                Line("moths", TraitHudKind.Count, (s, c, r) => s.Mothcaller.Moths, (s, c, r) => c.Mothcaller.SwarmMax)
            },
            ["mothcaller_trait_2"] = new[]
            {
                Line("moths", TraitHudKind.Count, (s, c, r) => s.Mothcaller.Moths, (s, c, r) => c.Mothcaller.SwarmMax),
                Line("pollen", TraitHudKind.Count, (s, c, r) => s.Boons.Pollen, (s, c, r) => 0)
            },
            ["mothcaller_trait_3"] = new[]
            {
                Line("awake", TraitHudKind.Flag, (s, c, r) => c.Mothcaller.Awake ? 1 : 0, (s, c, r) => 1),
                Line("wake", TraitHudKind.Timer, (s, c, r) => c.Mothcaller.SecondsToWake, (s, c, r) => c.Mothcaller.MothEverySeconds)
            },
            ["cartomancer_trait_1"] = new[]
            {
                Line("hand", TraitHudKind.Count, (s, c, r) => s.Cartomancer.HandCount, (s, c, r) => r.Cartomancer.HandMax),
                Line("draw", TraitHudKind.Count, (s, c, r) => s.Cartomancer.DrawHits, (s, c, r) => c.Cartomancer.DrawHits),
                Line("arcana", TraitHudKind.Timer, (s, c, r) => Longest(s.Cartomancer.ArcanaTicks) * GameLoop.TickSeconds, (s, c, r) => c.Cartomancer.ArcanaSeconds)
            },
            ["cartomancer_trait_2"] = new[]
            {
                Line("term", TraitHudKind.Timer, (s, c, r) => s.Cartomancer.TermTicks * GameLoop.TickSeconds, (s, c, r) => 0),
                Line("streak", TraitHudKind.Count, (s, c, r) => s.Cartomancer.Streak, (s, c, r) => r.Cartomancer.StreakMax)
            },
            ["cartomancer_trait_3"] = new[]
            {
                Line("laid", TraitHudKind.Count, (s, c, r) => s.Cartomancer.LaidCount, (s, c, r) => c.Cartomancer.LaidMax),
                Line("lay", TraitHudKind.Count, (s, c, r) => s.Cartomancer.LayHits, (s, c, r) => c.Cartomancer.LayHits)
            },
            ["herbwitch_trait_1"] = new[]
            {
                Line("vines", TraitHudKind.Count, (s, c, r) => s.Herbwitch.Vines, (s, c, r) => c.Herbwitch.MaxVines),
                Line("seed", TraitHudKind.Count, (s, c, r) => s.Herbwitch.SeedHits, (s, c, r) => c.Herbwitch.SeedHits)
            },
            ["herbwitch_trait_2"] = new[]
            {
                Line("venom", TraitHudKind.Count, (s, c, r) => s.Herbwitch.Venom, (s, c, r) => c.Herbwitch.VenomMax)
            },
            ["herbwitch_trait_3"] = new[]
            {
                Line("sapVenom", TraitHudKind.Count, (s, c, r) => s.Herbwitch.Venom, (s, c, r) => c.Herbwitch.VenomMax),
                Line("drink", TraitHudKind.Count, (s, c, r) => s.Herbwitch.DrinkHits, (s, c, r) => c.Herbwitch.DrinkHits),
                Line("brew", TraitHudKind.Count, (s, c, r) => s.Herbwitch.Brew, (s, c, r) => r.Herbwitch.BrewMax)
            },
            ["golemancer_trait_1"] = new[]
            {
                Line("golemSeal", TraitHudKind.Count, (s, c, r) => s.Golemancer.SealHits, (s, c, r) => c.Golemancer.SealHits),
                Line("golem", TraitHudKind.Timer, (s, c, r) => s.Golemancer.GolemTicks * GameLoop.TickSeconds, (s, c, r) => c.Golemancer.GolemSeconds)
            },
            ["golemancer_trait_2"] = new[]
            {
                Line("golemRunes", TraitHudKind.Count, (s, c, r) => s.Golemancer.Runes, (s, c, r) => c.Golemancer.RunesMax),
                Line("rune", TraitHudKind.Count, (s, c, r) => s.Golemancer.RuneHits, (s, c, r) => c.Golemancer.RuneHits)
            },
            ["golemancer_trait_3"] = new[]
            {
                Line("minors", TraitHudKind.Count, (s, c, r) => s.Golemancer.Minors, (s, c, r) => c.Golemancer.MinorMax),
                Line("minor", TraitHudKind.Count, (s, c, r) => s.Golemancer.MinorHits, (s, c, r) => c.Golemancer.MinorHits)
            }
        };

        public static IReadOnlyCollection<string> Traits => Lines.Keys;

        public static IReadOnlyList<TraitHudLine> LinesOf(string trait) => Lines.TryGetValue(trait, out var lines) ? lines : Array.Empty<TraitHudLine>();

        public static int Fill(GameLoop loop, TraitHudValue[] into)
        {
            var trait = loop.State.Ascendancy.Subclass;
            if (trait == null || !Lines.TryGetValue(trait, out var lines))
                return 0;
            var state = loop.State;
            var combat = loop.Combat;
            var rules = loop.Data.Combat.Classes;
            var count = System.Math.Min(lines.Length, into.Length);
            for (var i = 0; i < count; i++)
            {
                var line = lines[i];
                into[i].Kind = line.Kind;
                into[i].Key = line.Key;
                into[i].Label = Texts.Get(line.LabelKey);
                into[i].Value = System.Math.Max(0, line.Value(state, combat, rules));
                into[i].Max = System.Math.Max(0, line.Max(state, combat, rules));
            }
            return count;
        }

        private static TraitHudLine Line(string key, TraitHudKind kind, Func<GameState, CombatSystem, ClassRules, double> value, Func<GameState, CombatSystem, ClassRules, double> max) =>
            new TraitHudLine(key, kind, value, max);

        private static int Longest(int[] ticks)
        {
            var longest = 0;
            for (var i = 0; i < ticks.Length; i++)
                longest = System.Math.Max(longest, ticks[i]);
            return longest;
        }
    }
}
