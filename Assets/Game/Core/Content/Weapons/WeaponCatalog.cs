using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponCatalog
    {
        public const string FileName = "weapons.json";
        public const int SupportedVersion = 1;

        private static readonly HashSet<string> Colors = new HashSet<string>(StringComparer.Ordinal) { "red", "green", "blue", "any" };

        private readonly Dictionary<string, WeaponType> _types;
        private readonly Dictionary<string, WeaponBase> _bases;

        private WeaponCatalog(List<WeaponType> types, List<WeaponBase> bases, double ownTypeWeight, double otherTypeWeight, IReadOnlyList<double> baseWeights)
        {
            Types = types;
            Bases = bases;
            OwnTypeWeight = ownTypeWeight;
            OtherTypeWeight = otherTypeWeight;
            BaseWeights = baseWeights;
            _types = new Dictionary<string, WeaponType>(StringComparer.Ordinal);
            foreach (var type in types)
                _types[type.Id] = type;
            _bases = new Dictionary<string, WeaponBase>(StringComparer.Ordinal);
            foreach (var weaponBase in bases)
                _bases[weaponBase.Id] = weaponBase;
        }

        public IReadOnlyList<WeaponType> Types { get; }
        public IReadOnlyList<WeaponBase> Bases { get; }
        public double OwnTypeWeight { get; }
        public double OtherTypeWeight { get; }
        public IReadOnlyList<double> BaseWeights { get; }

        public bool TryGetType(string id, out WeaponType type) => _types.TryGetValue(id, out type!);

        public bool TryGetBase(string id, out WeaponBase weaponBase) => _bases.TryGetValue(id, out weaponBase!);

        public WeaponType TypeOf(HeroClass heroClass)
        {
            foreach (var type in Types)
            {
                if (type.HeroClass == heroClass)
                    return type;
            }
            throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, "Class has no weapon type.");
        }

        public WeaponBase StarterFor(HeroClass heroClass)
        {
            var type = TypeOf(heroClass);
            WeaponBase? starter = null;
            foreach (var weaponBase in Bases)
            {
                if (weaponBase.Type == type && (starter == null || weaponBase.FromBiome < starter.FromBiome))
                    starter = weaponBase;
            }
            return starter!;
        }

        public void RequireGemWeapons(GemCatalog gems)
        {
            foreach (var gem in gems.All)
            {
                var weapon = gem.Skill?.Weapon;
                if (weapon != null && !_types.ContainsKey(weapon))
                    throw new ContentException(GemCatalog.FileName, "gems." + gem.Id, $"unknown weapon type '{weapon}', expected one of {string.Join(", ", _types.Keys)} from {FileName}");
            }
        }

        public static WeaponCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var types = new List<WeaponType>();
            var byId = new Dictionary<string, WeaponType>(StringComparer.Ordinal);
            var classes = new HashSet<HeroClass>();
            foreach (var node in root.Field("types").Items())
            {
                var type = ReadType(node);
                if (byId.ContainsKey(type.Id))
                    throw node.Field("id").Error($"duplicate weapon type '{type.Id}'");
                if (!classes.Add(type.HeroClass))
                    throw node.Field("class").Error("this class already has a weapon type");
                byId[type.Id] = type;
                types.Add(type);
            }
            foreach (var heroClass in HeroClassNames.ByName.Values)
            {
                if (!classes.Contains(heroClass))
                    throw root.Field("types").Error($"class '{HeroClassNames.Of(heroClass)}' has no weapon type");
            }

            var bases = new List<WeaponBase>();
            var baseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("bases").Items())
            {
                var weaponBase = ReadBase(node, byId);
                if (!baseIds.Add(weaponBase.Id))
                    throw node.Field("id").Error($"duplicate weapon base '{weaponBase.Id}'");
                bases.Add(weaponBase);
            }
            foreach (var type in types)
            {
                if (!bases.Exists(weaponBase => weaponBase.Type == type))
                    throw root.Field("bases").Error($"weapon type '{type.Id}' has no base");
            }

            var weights = root.Field("typeWeights");
            var baseWeights = new List<double>();
            foreach (var item in root.Field("baseWeights").Items())
                baseWeights.Add(item.AsNumber(0, 1000));
            return new WeaponCatalog(types, bases, weights.Field("own").AsNumber(0, 1000), weights.Field("other").AsNumber(0, 1000), baseWeights);
        }

        private static WeaponType ReadType(ContentNode node)
        {
            var classNode = node.Field("class");
            if (!HeroClassNames.ByName.TryGetValue(classNode.AsString(), out var heroClass))
                throw classNode.Error($"unknown class '{classNode.AsString()}'");
            var color = node.Field("color");
            if (!Colors.Contains(color.AsString()))
                throw color.Error($"unknown color '{color.AsString()}', expected red, green, blue or any");
            var name = node.Field("name");
            return new WeaponType(node.Field("id").AsString(), heroClass, color.AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                ModifierReader.ReadList(node.Field("innate")));
        }

        private static WeaponBase ReadBase(ContentNode node, Dictionary<string, WeaponType> types)
        {
            var typeNode = node.Field("type");
            if (!types.TryGetValue(typeNode.AsString(), out var type))
                throw typeNode.Error($"unknown weapon type '{typeNode.AsString()}'");
            var name = node.Field("name");
            var innate = node.HasField("innate") ? ModifierReader.ReadList(node.Field("innate")) : Array.Empty<Modifier>();
            return new WeaponBase(node.Field("id").AsString(), type, node.Field("fromBiome").AsInt(1, 100), name.Field("ru").AsString(), name.Field("en").AsString(), innate);
        }
    }
}
