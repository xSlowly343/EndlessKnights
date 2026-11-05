using System;
using System.Collections.Generic;

namespace Game.Core.Content.Tree
{
    public sealed class AscendancyActivity
    {
        public AscendancyActivity(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class SubclassDef
    {
        public SubclassDef(string id, string heroClass, string nameRu, string nameEn, string blurbRu = "", string blurbEn = "", TraitDef? trait = null)
        {
            Id = id;
            HeroClass = heroClass;
            NameRu = nameRu;
            NameEn = nameEn;
            BlurbRu = blurbRu;
            BlurbEn = blurbEn;
            Trait = trait;
        }

        public string Id { get; }
        public string HeroClass { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string BlurbRu { get; }
        public string BlurbEn { get; }
        public TraitDef? Trait { get; }
        public Game.Core.Party.PartyGiveMode PartyMode { get; set; }
    }

    public sealed class AscendancyCatalog
    {
        public const string FileName = "ascendancy.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, SubclassDef> _byId;

        private AscendancyCatalog(IReadOnlyList<AscendancyActivity> activities, IReadOnlyList<SubclassDef> subclasses, double freeChangeHours, int crystalChangeCost, int freeClassChanges,
            IReadOnlyList<double> forkScale)
        {
            ForkScale = forkScale;
            FreeClassChanges = freeClassChanges;
            Activities = activities;
            Subclasses = subclasses;
            FreeChangeHours = freeChangeHours;
            CrystalChangeCost = crystalChangeCost;
            _byId = new Dictionary<string, SubclassDef>(StringComparer.Ordinal);
            foreach (var subclass in subclasses)
                _byId.Add(subclass.Id, subclass);
        }

        public IReadOnlyList<AscendancyActivity> Activities { get; }
        public IReadOnlyList<SubclassDef> Subclasses { get; }
        public double FreeChangeHours { get; }
        public int CrystalChangeCost { get; }
        public int FreeClassChanges { get; }
        public int MaxPoints => Activities.Count;
        public IReadOnlyList<double> ForkScale { get; }

        public double ForkScaleAt(int traitLevel) =>
            ForkScale.Count == 0 ? 1 : ForkScale[System.Math.Clamp(traitLevel, 1, ForkScale.Count) - 1];

        public bool TryGet(string id, out SubclassDef subclass) => _byId.TryGetValue(id, out subclass!);

        public bool IsActivity(string id)
        {
            foreach (var activity in Activities)
            {
                if (string.Equals(activity.Id, id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public IReadOnlyList<SubclassDef> SubclassesOf(string heroClass)
        {
            var found = new List<SubclassDef>();
            foreach (var subclass in Subclasses)
            {
                if (string.Equals(subclass.HeroClass, heroClass, StringComparison.Ordinal))
                    found.Add(subclass);
            }
            return found;
        }

        public void RequireKnownClasses()
        {
            foreach (var subclass in Subclasses)
            {
                if (!Sim.HeroClassNames.ByName.ContainsKey(subclass.HeroClass))
                    throw new ContentException(FileName, subclass.Id, $"unknown class '{subclass.HeroClass}'");
            }
        }

        public void RequirePathSkills(Gems.GemCatalog gems, Weapons.WeaponCatalog weapons)
        {
            var skills = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var gem in gems.All)
            {
                if (gem.Ascendancy == null)
                    continue;
                var where = "gems." + gem.Id;
                if (!TryGet(gem.Ascendancy, out var path))
                    throw new ContentException(Gems.GemCatalog.FileName, where, $"unknown path '{gem.Ascendancy}', expected a subclass id from {FileName}");
                if (!weapons.TryGetType(gem.Skill!.Weapon!, out var weapon) || Sim.HeroClassNames.Of(weapon.HeroClass) != path.HeroClass)
                    throw new ContentException(Gems.GemCatalog.FileName, where, $"path '{path.Id}' belongs to '{path.HeroClass}', the skill needs the weapon of that class");
                skills[path.Id] = skills.TryGetValue(path.Id, out var count) ? count + 1 : 1;
            }
            foreach (var path in Subclasses)
            {
                var count = skills.TryGetValue(path.Id, out var found) ? found : 0;
                if (count != 1)
                    throw new ContentException(FileName, path.Id, $"a path has exactly one path skill, got {count}");
            }
        }

        public static AscendancyCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var activities = ReadActivities(root.Field("activities"));
            var change = root.Field("change");
            var subclasses = new List<SubclassDef>();
            var subclassIds = new HashSet<string>(StringComparer.Ordinal);
            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("subclasses").Items())
            {
                var subclass = ReadSubclass(item, optionIds);
                if (!subclassIds.Add(subclass.Id))
                    throw item.Field("id").Error($"subclass id '{subclass.Id}' is used twice");
                subclasses.Add(subclass);
            }
            return new AscendancyCatalog(activities, subclasses,
                change.Field("freeEveryHours").AsNumber(0, 24 * 365),
                change.Field("crystalCost").AsInt(0, 1_000_000),
                change.Field("freeClassChanges").AsInt(0, 100),
                ReadForkScale(root));
        }

        private static IReadOnlyList<double> ReadForkScale(ContentNode root)
        {
            var scale = new List<double>();
            if (!root.HasField("forkScale"))
                return scale;
            var node = root.Field("forkScale");
            foreach (var item in node.Items())
                scale.Add(item.AsNumber(1, 10));
            if (scale.Count != TraitDef.MaxLevel)
                throw node.Error($"forkScale needs {TraitDef.MaxLevel} values, one per trait level");
            for (var i = 1; i < scale.Count; i++)
            {
                if (scale[i] < scale[i - 1])
                    throw node.Error("forkScale must not fall with the trait level");
            }
            return scale;
        }

        private static List<AscendancyActivity> ReadActivities(ContentNode node)
        {
            var activities = new List<AscendancyActivity>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"activity id '{id}' is used twice");
                var name = item.Field("name");
                activities.Add(new AscendancyActivity(id, name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            if (activities.Count == 0)
                throw node.Error("at least one activity is required, each one gives a point");
            return activities;
        }

        private static SubclassDef ReadSubclass(ContentNode item, HashSet<string> optionIds)
        {
            var id = item.Field("id").AsString();
            foreach (var field in new[] { "nodes", "links" })
            {
                if (item.HasField(field))
                    throw item.Field(field).Error($"subclass '{id}' has '{field}', a path has no node tree: its strength is the 'trait'");
            }
            var name = item.Field("name");
            var blurbRu = item.HasField("blurb") ? item.Field("blurb").Field("ru").AsString() : string.Empty;
            var blurbEn = item.HasField("blurb") ? item.Field("blurb").Field("en").AsString() : string.Empty;
            var trait = item.HasField("trait") ? TraitDef.Read(item.Field("trait"), optionIds) : null;
            return new SubclassDef(id, item.Field("class").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), blurbRu, blurbEn, trait)
            {
                PartyMode = item.HasField("partyMode") ? Game.Core.Party.CoopRules.ReadMode(item.Field("partyMode")) : Game.Core.Party.PartyGiveMode.Together
            };
        }
    }
}
