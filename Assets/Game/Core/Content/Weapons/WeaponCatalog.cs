using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Math;
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
        private Dictionary<string, IReadOnlyList<GemDef>> _pools = new Dictionary<string, IReadOnlyList<GemDef>>(StringComparer.Ordinal);

        private WeaponCatalog(List<WeaponType> types, List<WeaponBase> bases, double ownTypeWeight, double otherTypeWeight, IReadOnlyList<double> baseWeights, WeaponSkillRules skillRules, ReforgeTable reforge)
        {
            Reforge = reforge;
            SkillRules = skillRules;
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
        public WeaponSkillRules SkillRules { get; }
        public ReforgeTable Reforge { get; }

        public IReadOnlyList<UniqueWeaponDef> Uniques { get; private set; } = Array.Empty<UniqueWeaponDef>();

        public bool TryGetUnique(string id, out UniqueWeaponDef unique)
        {
            foreach (var candidate in Uniques)
            {
                if (candidate.Id == id)
                {
                    unique = candidate;
                    return true;
                }
            }
            unique = null!;
            return false;
        }

        public UniqueWeaponDef? PickUnique(ItemRarity ceiling, Rng rng)
        {
            var count = 0;
            foreach (var unique in Uniques)
            {
                if (unique.Rarity <= ceiling)
                    count++;
            }
            if (count == 0)
                return null;
            var pick = rng.NextInt(0, count);
            foreach (var unique in Uniques)
            {
                if (unique.Rarity <= ceiling && pick-- == 0)
                    return unique;
            }
            return null;
        }

        public WeaponItem RollUnique(UniqueWeaponDef unique, Rng rng)
        {
            var first = RollSkills(unique.Base.Type, unique.Rarity, rng);
            if (unique.SecondType == null)
                return new WeaponItem(unique.Base, unique.Rarity, first, unique);
            var second = RollSkills(unique.SecondType, unique.Rarity, rng);
            var skills = new List<string>();
            for (var pair = 0; skills.Count < SkillRules.Places && (pair * 2 < first.Count || pair * 2 < second.Count); pair++)
            {
                Take(first, pair * 2, skills);
                Take(first, pair * 2 + 1, skills);
                Take(second, pair * 2, skills);
                Take(second, pair * 2 + 1, skills);
            }
            return new WeaponItem(unique.Base, unique.Rarity, skills, unique);
        }

        private void Take(IReadOnlyList<string> from, int index, List<string> skills)
        {
            if (index < from.Count && skills.Count < SkillRules.Places)
                skills.Add(from[index]);
        }

        public IReadOnlyList<GemDef> PoolOf(WeaponType type) =>
            _pools.TryGetValue(type.Id, out var pool) ? pool : Array.Empty<GemDef>();

        public WeaponItem Starter(WeaponBase weaponBase) => new WeaponItem(weaponBase, ItemRarity.Simple, weaponBase.Type.StarterSkills);

        public WeaponItem Roll(WeaponBase weaponBase, ItemRarity rarity, Rng rng) =>
            new WeaponItem(weaponBase, rarity, RollSkills(weaponBase.Type, rarity, rng));

        public IReadOnlyList<string> RollSkills(WeaponType type, ItemRarity rarity, Rng rng)
        {
            var tiers = RollTiers(rarity, rng);
            var pool = PoolOf(type);
            var chosen = new List<GemDef>();
            var mythic = 0;
            for (var place = 0; place < tiers.Length; place++)
            {
                var tier = tiers[place] == SkillTier.Mythic && mythic >= SkillRules.MaxMythic ? SkillTier.Legendary : tiers[place];
                var skill = PickSkill(pool, tier, RoleOf(place), chosen, rng) ?? PickSkill(pool, tier, SkillRole.Other, chosen, rng);
                if (skill == null)
                    continue;
                chosen.Add(skill);
                if (skill.Skill!.Tier == SkillTier.Mythic)
                    mythic++;
            }
            var ids = new string[chosen.Count];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = chosen[i].Id;
            return ids;
        }

        private SkillTier[] RollTiers(ItemRarity rarity, Rng rng)
        {
            var tiers = new SkillTier[SkillRules.Places];
            for (var place = 0; place < tiers.Length; place++)
                tiers[place] = SkillRules.RollTier(rarity, rng);
            if (SkillRules.GuaranteesMythic(rarity) && Array.IndexOf(tiers, SkillTier.Mythic, FreePlaces) < 0)
                tiers[rng.NextInt(FreePlaces, tiers.Length)] = SkillTier.Mythic;
            return tiers;
        }

        private const int FreePlaces = 2;

        private static SkillRole RoleOf(int place) =>
            place == 0 ? SkillRole.Fast : place == 1 ? SkillRole.Heavy : SkillRole.Other;

        private static GemDef? PickSkill(IReadOnlyList<GemDef> pool, SkillTier tier, SkillRole role, List<GemDef> chosen, Rng rng)
        {
            var candidates = new List<GemDef>();
            for (var current = (int)tier; current >= 0 && candidates.Count == 0; current--)
            {
                foreach (var gem in pool)
                {
                    var skill = gem.Skill!;
                    if ((int)skill.Tier == current && (role == SkillRole.Other || skill.Role == role) && !chosen.Contains(gem))
                        candidates.Add(gem);
                }
            }
            return candidates.Count == 0 ? null : candidates[rng.NextInt(0, candidates.Count)];
        }

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

        public WeaponItem Reforged(WeaponItem weapon, WeaponType to)
        {
            var from = weapon.Type;
            if (weapon.Unique != null || from == to)
                return weapon;
            var skills = new string[weapon.Skills.Count];
            for (var i = 0; i < skills.Length; i++)
                skills[i] = Reforge.SkillId(weapon.Skills[i], from, to);
            return new WeaponItem(BaseAtStep(to, StepOf(weapon.Base)), weapon.Rarity, skills, null, weapon.Set);
        }

        private int StepOf(WeaponBase weaponBase)
        {
            var step = 0;
            foreach (var other in Bases)
            {
                if (other.Type == weaponBase.Type && other.FromBiome < weaponBase.FromBiome)
                    step++;
            }
            return step;
        }

        private WeaponBase BaseAtStep(WeaponType type, int step)
        {
            var steps = new List<WeaponBase>();
            foreach (var weaponBase in Bases)
            {
                if (weaponBase.Type == type)
                    steps.Add(weaponBase);
            }
            steps.Sort((a, b) => a.FromBiome.CompareTo(b.FromBiome));
            return steps[System.Math.Min(step, steps.Count - 1)];
        }

        public bool HasType(HeroClass heroClass)
        {
            foreach (var type in Types)
            {
                if (type.HeroClass == heroClass)
                    return true;
            }
            return false;
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

        public void BindGems(GemCatalog gems)
        {
            var pools = new Dictionary<string, List<GemDef>>(StringComparer.Ordinal);
            var classGems = new Dictionary<string, List<GemDef>>(StringComparer.Ordinal);
            foreach (var type in Types)
            {
                pools[type.Id] = new List<GemDef>();
                classGems[type.Id] = new List<GemDef>();
            }
            foreach (var gem in gems.All)
            {
                var weapon = gem.Skill?.Weapon;
                if (weapon == null)
                    continue;
                if (!pools.TryGetValue(weapon, out var pool))
                    throw new ContentException(GemCatalog.FileName, "gems." + gem.Id, $"unknown weapon type '{weapon}', expected one of {string.Join(", ", _types.Keys)} from {FileName}");
                if (gem.Ascendancy != null)
                    continue;
                classGems[weapon].Add(gem);
                if (!gem.Locked)
                    pool.Add(gem);
            }
            Reforge.Bind(Types, classGems);
            foreach (var type in Types)
                RequireStarterSkills(type, pools[type.Id]);
            var bound = new Dictionary<string, IReadOnlyList<GemDef>>(StringComparer.Ordinal);
            foreach (var pair in pools)
                bound[pair.Key] = pair.Value;
            _pools = bound;
        }

        private void RequireStarterSkills(WeaponType type, List<GemDef> pool)
        {
            var where = "types." + type.Id + ".starterSkills";
            if (type.StarterSkills.Count > SkillRules.Places)
                throw new ContentException(FileName, where, $"a weapon holds at most {SkillRules.Places} skills");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in type.StarterSkills)
            {
                if (!seen.Add(id))
                    throw new ContentException(FileName, where, $"skill '{id}' is listed twice");
                if (!pool.Exists(gem => gem.Id == id))
                    throw new ContentException(FileName, where, $"skill '{id}' is not a class skill of '{type.Id}' in {GemCatalog.FileName}");
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
            var catalog = new WeaponCatalog(types, bases, weights.Field("own").AsNumber(0, 1000), weights.Field("other").AsNumber(0, 1000), baseWeights,
                WeaponSkillRules.Read(root.Field("skills")), ReforgeTable.Read(root.Field("reforge")));
            catalog.Uniques = ReadUniques(root.Field("uniques"), catalog, byId);
            return catalog;
        }

        private static List<UniqueWeaponDef> ReadUniques(ContentNode node, WeaponCatalog catalog, Dictionary<string, WeaponType> types)
        {
            var uniques = new List<UniqueWeaponDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"unique weapon id '{id}' is used twice");
                var baseNode = item.Field("base");
                if (!catalog.TryGetBase(baseNode.AsString(), out var weaponBase))
                    throw baseNode.Error($"unknown weapon base '{baseNode.AsString()}'");
                WeaponType? second = null;
                if (item.HasField("alsoType"))
                {
                    var typeNode = item.Field("alsoType");
                    if (!types.TryGetValue(typeNode.AsString(), out var found) || found == weaponBase.Type)
                        throw typeNode.Error($"'{typeNode.AsString()}' is not a second weapon type");
                    second = found;
                }
                var rarityNode = item.Field("rarity");
                if (!ItemRarityNames.ByName.TryGetValue(rarityNode.AsString(), out var rarity))
                    throw rarityNode.Error($"unknown rarity '{rarityNode.AsString()}'");
                var name = item.Field("name");
                var rule = item.Field("rule");
                uniques.Add(new UniqueWeaponDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), weaponBase, second, rarity,
                    ModifierReader.ReadList(item.Field("lines")), rule.Field("ru").AsString(), rule.Field("en").AsString()));
            }
            return uniques;
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
            var starterSkills = new List<string>();
            foreach (var item in node.Field("starterSkills").Items())
                starterSkills.Add(item.AsString());
            return new WeaponType(node.Field("id").AsString(), heroClass, color.AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                ModifierReader.ReadList(node.Field("innate")), starterSkills);
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
