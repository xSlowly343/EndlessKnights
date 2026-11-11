using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class StarCost
    {
        public StarCost(int sparks, int souls)
        {
            Sparks = sparks;
            Souls = souls;
        }

        public int Sparks { get; }
        public int Souls { get; }
    }

    public sealed class CompanionStarRules
    {
        public IReadOnlyList<StarCost> Costs { get; private set; } = Array.Empty<StarCost>();
        public int LevelsPerStar { get; private set; }
        public double TraitsPerStar { get; private set; }
        public IReadOnlyDictionary<string, int> StartStars { get; private set; } = new Dictionary<string, int>();
        public int SoulsPerHost { get; private set; }
        public int SoulsPerTripHours { get; private set; }
        public int SoulsPerPartyTrip { get; private set; }
        public int SoulsPerWeeklyTrial { get; private set; }

        public int MaxStars => Costs.Count;
        public int StartOf(string rarity) => StartStars.TryGetValue(rarity, out var stars) ? stars : 0;

        internal static CompanionStarRules Load(ContentNode node, IReadOnlyList<string> rarities)
        {
            var costs = new List<StarCost>();
            foreach (var item in node.Field("costs").Items())
                costs.Add(new StarCost(item.Field("sparks").AsInt(1, 100_000), item.Field("souls").AsInt(1, 100_000)));
            var start = new Dictionary<string, int>(StringComparer.Ordinal);
            var startNode = node.Field("startStars");
            foreach (var name in startNode.FieldNames())
            {
                if (!Contains(rarities, name))
                    throw startNode.Field(name).Error($"unknown rarity '{name}'");
                start[name] = startNode.Field(name).AsInt(0, costs.Count);
            }
            var souls = node.Field("souls");
            return new CompanionStarRules
            {
                Costs = costs,
                LevelsPerStar = node.Field("levelsPerStar").AsInt(0, 100),
                TraitsPerStar = node.Field("traitsPerStar").AsNumber(0, 10),
                StartStars = start,
                SoulsPerHost = souls.Field("perHostDaily").AsInt(0, 1000),
                SoulsPerTripHours = souls.Field("tripHoursPerSoul").AsInt(1, 100_000),
                SoulsPerPartyTrip = souls.Field("perPartyTrip").AsInt(0, 1000),
                SoulsPerWeeklyTrial = souls.Field("perWeeklyTrial").AsInt(0, 1000)
            };
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }
    }

    public sealed class CompanionForm
    {
        public CompanionForm(string id, int stage, int stars, double loot, double speed, double home)
        {
            Id = id;
            Stage = stage;
            Stars = stars;
            Loot = loot;
            Speed = speed;
            Home = home;
        }

        public string Id { get; }
        public int Stage { get; }
        public int Stars { get; }
        public double Loot { get; }
        public double Speed { get; }
        public double Home { get; }
    }

    public sealed class CompanionFormRules
    {
        public IReadOnlyList<CompanionForm> Forms { get; private set; } = Array.Empty<CompanionForm>();
        public int ChangeSouls { get; private set; }

        public CompanionForm? Form(string id)
        {
            for (var i = 0; i < Forms.Count; i++)
            {
                if (Forms[i].Id == id)
                    return Forms[i];
            }
            return null;
        }

        internal static CompanionFormRules Load(ContentNode node, int stages, int maxStars)
        {
            var forms = new List<CompanionForm>();
            foreach (var item in node.Field("forms").Items())
            {
                var id = item.Field("id").AsString();
                if (forms.Exists(form => form.Id == id))
                    throw item.Field("id").Error($"form '{id}' is listed twice");
                forms.Add(new CompanionForm(id, item.Field("stage").AsInt(0, stages - 1), item.HasField("stars") ? item.Field("stars").AsInt(0, maxStars) : 0,
                    item.HasField("loot") ? item.Field("loot").AsNumber(0, 10) : 0, item.HasField("speed") ? item.Field("speed").AsNumber(0, 0.9) : 0,
                    item.HasField("home") ? item.Field("home").AsNumber(0, 10) : 0));
            }
            return new CompanionFormRules { Forms = forms, ChangeSouls = node.Field("changeSouls").AsInt(0, 1_000_000) };
        }
    }

    public enum CompanionMilestone
    {
        TripSlot,
        HomeSlot,
        CampLook
    }

    public sealed class ResonanceStep
    {
        public ResonanceStep(int levels, CompanionMilestone kind)
        {
            Levels = levels;
            Kind = kind;
        }

        public int Levels { get; }
        public CompanionMilestone Kind { get; }
    }

    public sealed class MasteryStep
    {
        public MasteryStep(int trips, double experience)
        {
            Trips = trips;
            Experience = experience;
        }

        public int Trips { get; }
        public double Experience { get; }
    }

    public sealed class CompanionResonanceRules
    {
        public int LevelsPerStep { get; private set; } = int.MaxValue;
        public double Step { get; private set; }
        public double Cap { get; private set; }
        public IReadOnlyList<MuseumPerk> Perks { get; private set; } = Array.Empty<MuseumPerk>();
        public IReadOnlyList<ResonanceStep> Milestones { get; private set; } = Array.Empty<ResonanceStep>();
        public IReadOnlyList<MasteryStep> Mastery { get; private set; } = Array.Empty<MasteryStep>();

        private static readonly Dictionary<string, CompanionMilestone> Kinds = new Dictionary<string, CompanionMilestone>(StringComparer.Ordinal)
        {
            ["tripSlot"] = CompanionMilestone.TripSlot,
            ["homeSlot"] = CompanionMilestone.HomeSlot,
            ["campLook"] = CompanionMilestone.CampLook
        };

        internal static CompanionResonanceRules Load(ContentNode node, ContentNode mastery)
        {
            var perks = new List<MuseumPerk>();
            foreach (var item in node.Field("perks").Items())
            {
                var name = item.AsString();
                if (!MuseumRules.TryPerk(name, out var perk))
                    throw item.Error($"unknown perk '{name}'");
                perks.Add(perk);
            }
            var milestones = new List<ResonanceStep>();
            foreach (var item in node.Field("milestones").Items())
            {
                var kind = item.Field("kind").AsString();
                if (!Kinds.TryGetValue(kind, out var parsed))
                    throw item.Field("kind").Error($"unknown milestone '{kind}'");
                milestones.Add(new ResonanceStep(item.Field("levels").AsInt(1, 1_000_000), parsed));
            }
            var steps = new List<MasteryStep>();
            var last = 0;
            foreach (var item in mastery.Items())
            {
                var trips = item.Field("trips").AsInt(1, 1_000_000);
                if (trips <= last)
                    throw item.Field("trips").Error("mastery steps must go up in trips");
                last = trips;
                steps.Add(new MasteryStep(trips, item.Field("experience").AsNumber(0, 10)));
            }
            return new CompanionResonanceRules
            {
                LevelsPerStep = node.Field("levelsPerStep").AsInt(1, 1_000_000),
                Step = node.Field("step").AsNumber(0, 1),
                Cap = node.Field("cap").AsNumber(0, 1),
                Perks = perks,
                Milestones = milestones,
                Mastery = steps
            };
        }
    }
}
