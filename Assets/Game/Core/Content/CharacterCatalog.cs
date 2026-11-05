using System;
using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Content
{
    public sealed class CharacterAxis
    {
        public CharacterAxis(string id, string nameRu, string nameEn)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class CharacterPathDef
    {
        public CharacterPathDef(string id, IReadOnlyList<int> radar, IReadOnlyList<string> prosRu, IReadOnlyList<string> prosEn, IReadOnlyList<string> consRu, IReadOnlyList<string> consEn)
        {
            Id = id;
            Radar = radar;
            ProsRu = prosRu;
            ProsEn = prosEn;
            ConsRu = consRu;
            ConsEn = consEn;
        }

        public string Id { get; }
        public IReadOnlyList<int> Radar { get; }
        public IReadOnlyList<string> ProsRu { get; }
        public IReadOnlyList<string> ProsEn { get; }
        public IReadOnlyList<string> ConsRu { get; }
        public IReadOnlyList<string> ConsEn { get; }
    }

    public sealed class CharacterDef
    {
        public CharacterDef(string id, string role, string aboutRu, string aboutEn, IReadOnlyList<int> radar, IReadOnlyList<string> partners,
            IReadOnlyList<CharacterPathDef> paths)
        {
            Paths = paths;
            Id = id;
            Role = role;
            AboutRu = aboutRu;
            AboutEn = aboutEn;
            Radar = radar;
            Partners = partners;
        }

        public string Id { get; }
        public string Role { get; }
        public string AboutRu { get; }
        public string AboutEn { get; }
        public IReadOnlyList<int> Radar { get; }
        public IReadOnlyList<string> Partners { get; }
        public IReadOnlyList<CharacterPathDef> Paths { get; }

        public bool TryGetPath(string id, out CharacterPathDef path)
        {
            foreach (var candidate in Paths)
            {
                if (candidate.Id == id)
                {
                    path = candidate;
                    return true;
                }
            }
            path = null!;
            return false;
        }
    }

    public sealed class CharacterCatalog
    {
        public const string FileName = "characters.json";
        public const int SupportedVersion = 1;
        public const int Axes = 4;
        public const int MaxPathLines = 3;
        public const int MinRadar = 1;
        public const int MaxRadar = 5;
        public const int MinPartners = 2;
        public const int MaxPartners = 3;

        private readonly Dictionary<string, CharacterDef> _byId;

        private CharacterCatalog(IReadOnlyList<CharacterAxis> axes, IReadOnlyList<CharacterDef> characters)
        {
            AxisList = axes;
            All = characters;
            _byId = new Dictionary<string, CharacterDef>(StringComparer.Ordinal);
            foreach (var character in characters)
                _byId.Add(character.Id, character);
        }

        public IReadOnlyList<CharacterAxis> AxisList { get; }
        public IReadOnlyList<CharacterDef> All { get; }

        public bool TryGet(string id, out CharacterDef character) => _byId.TryGetValue(id, out character!);

        public void RequireRoster(IReadOnlyList<string> partyRoles)
        {
            foreach (var name in HeroClassNames.ByName.Keys)
            {
                if (!_byId.ContainsKey(name))
                    throw new ContentException(FileName, "characters", $"character '{name}' is missing");
            }
            foreach (var character in All)
            {
                var where = "characters." + character.Id;
                if (!HeroClassNames.ByName.ContainsKey(character.Id))
                    throw new ContentException(FileName, where, $"unknown character '{character.Id}'");
                if (character.Role == Party.PartySearchRules.AnyRole || !Contains(partyRoles, character.Role))
                    throw new ContentException(FileName, where, $"role '{character.Role}' is not a role of party.json search.roles");
                foreach (var partner in character.Partners)
                {
                    if (partner == character.Id || !_byId.ContainsKey(partner))
                        throw new ContentException(FileName, where, $"partner '{partner}' must be another character");
                }
            }
        }

        public void RequirePaths(Tree.AscendancyCatalog ascendancy)
        {
            foreach (var character in All)
            {
                var where = "characters." + character.Id + ".paths";
                var own = ascendancy.SubclassesOf(character.Id);
                foreach (var path in own)
                {
                    if (!character.TryGetPath(path.Id, out _))
                        throw new ContentException(FileName, where, $"path '{path.Id}' has no star, pros and cons");
                }
                foreach (var path in character.Paths)
                {
                    if (!ascendancy.TryGet(path.Id, out var subclass) || subclass.HeroClass != character.Id)
                        throw new ContentException(FileName, where, $"'{path.Id}' is not a path of {character.Id}");
                }
            }
        }

        public static CharacterCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var axes = new List<CharacterAxis>();
            foreach (var item in root.Field("axes").Items())
            {
                var name = item.Field("name");
                axes.Add(new CharacterAxis(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            if (axes.Count != Axes)
                throw root.Field("axes").Error($"the radar has exactly {Axes} axes, got {axes.Count}");
            var characters = new List<CharacterDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("characters").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"character '{id}' is listed twice");
                var radar = ReadRadar(item.Field("radar"));
                var partners = new List<string>();
                foreach (var partner in item.Field("partners").Items())
                    partners.Add(partner.AsString());
                if (partners.Count < MinPartners || partners.Count > MaxPartners)
                    throw item.Field("partners").Error($"a character has {MinPartners}-{MaxPartners} partners, got {partners.Count}");
                var about = item.Field("about");
                characters.Add(new CharacterDef(id, item.Field("role").AsString(), about.Field("ru").AsString(), about.Field("en").AsString(), radar, partners,
                    ReadPaths(item.Field("paths"))));
            }
            return new CharacterCatalog(axes, characters);
        }

        private static List<CharacterPathDef> ReadPaths(ContentNode node)
        {
            var paths = new List<CharacterPathDef>();
            foreach (var id in node.FieldNames())
            {
                var path = node.Field(id);
                paths.Add(new CharacterPathDef(id, ReadRadar(path.Field("radar")),
                    Lines(path.Field("pros"), "ru"), Lines(path.Field("pros"), "en"), Lines(path.Field("cons"), "ru"), Lines(path.Field("cons"), "en")));
            }
            return paths;
        }

        private static List<int> ReadRadar(ContentNode node)
        {
            var radar = new List<int>();
            foreach (var value in node.Items())
                radar.Add(value.AsInt(MinRadar, MaxRadar));
            if (radar.Count != Axes)
                throw node.Error($"the radar has {Axes} values, got {radar.Count}");
            return radar;
        }

        private static List<string> Lines(ContentNode node, string language)
        {
            var lines = new List<string>();
            foreach (var line in node.Field(language).Items())
                lines.Add(line.AsString());
            if (lines.Count < 1 || lines.Count > MaxPathLines)
                throw node.Field(language).Error($"1-{MaxPathLines} lines, got {lines.Count}");
            return lines;
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var item in list)
            {
                if (item == value)
                    return true;
            }
            return false;
        }
    }
}
