using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class AuraShowRules
    {
        public const string FileName = "aura_show.json";
        public const int SupportedVersion = 1;
        public const string CommonSigil = "common";

        private const string ClassAuraMark = "_aura_";

        private readonly List<KeyValuePair<AuraTheme, HashSet<string>>> _themes;
        private readonly HashSet<string> _ignored;
        private readonly string[] _kinSuffixes;
        private readonly AuraTheme _fallback;
        private readonly Dictionary<string, AuraTheme> _overrides;
        private readonly Dictionary<string, AuraTheme> _resolved = new Dictionary<string, AuraTheme>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _sigils = new Dictionary<string, string>(StringComparer.Ordinal);

        private AuraShowRules(double warnSeconds, List<KeyValuePair<AuraTheme, HashSet<string>>> themes, HashSet<string> ignored, string[] kinSuffixes,
            AuraTheme fallback, Dictionary<string, AuraTheme> overrides)
        {
            WarnSeconds = warnSeconds;
            _themes = themes;
            _ignored = ignored;
            _kinSuffixes = kinSuffixes;
            _fallback = fallback;
            _overrides = overrides;
        }

        public static AuraShowRules Default => new AuraShowRules(3, new List<KeyValuePair<AuraTheme, HashSet<string>>>(), new HashSet<string>(StringComparer.Ordinal),
            Array.Empty<string>(), AuraTheme.Might, new Dictionary<string, AuraTheme>(StringComparer.Ordinal));

        public double WarnSeconds { get; }

        public AuraTheme ThemeOf(GemDef gem)
        {
            if (_resolved.TryGetValue(gem.Id, out var known))
                return known;
            return Resolve(gem);
        }

        public string SigilOf(GemDef gem) => _sigils.TryGetValue(gem.Id, out var known) ? known : OwnerOf(gem);

        private static string OwnerOf(GemDef gem)
        {
            var at = gem.Id.IndexOf(ClassAuraMark, StringComparison.Ordinal);
            if (at <= 0)
                return CommonSigil;
            var owner = gem.Id.Substring(0, at);
            return HeroClassNames.ByName.ContainsKey(owner) ? owner : CommonSigil;
        }

        private void Bind(GemCatalog gems, ContentNode source)
        {
            foreach (var pair in _overrides)
            {
                if (!gems.TryGet(pair.Key, out var gem) || gem.Type != SkillType.Aura)
                    throw source.Field("gems").Field(pair.Key).Error($"'{pair.Key}' is not an aura in {GemCatalog.FileName}");
            }
            foreach (var gem in gems.All)
            {
                if (gem.Type != SkillType.Aura)
                    continue;
                _resolved[gem.Id] = Resolve(gem);
                _sigils[gem.Id] = OwnerOf(gem);
            }
        }

        public static AuraShowRules Load(string json, GemCatalog gems)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var themes = ReadThemes(root.Field("themes"));
            var ignored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("ignoredStats").Items())
                ignored.Add(item.AsString());
            var suffixes = new List<string>();
            foreach (var item in root.Field("kinSuffixes").Items())
                suffixes.Add(item.AsString());
            var rules = new AuraShowRules(root.Field("warnSeconds").AsNumber(0, 60), themes, ignored, suffixes.ToArray(),
                ReadTheme(root.Field("fallback")), ReadOverrides(root.Field("gems")));
            rules.Bind(gems, root);
            return rules;
        }

        private AuraTheme Resolve(GemDef gem)
        {
            if (_overrides.TryGetValue(gem.Id, out var chosen))
                return chosen;
            if (_overrides.TryGetValue(gem.Root.Id, out var rootChosen))
                return rootChosen;
            if (!(gem.Skill?.Effect is AuraEffect aura))
                return _fallback;
            var modifiers = aura.Modifiers;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var stat = modifiers[i].Stat.BaseName;
                if (_ignored.Contains(stat))
                    continue;
                foreach (var theme in _themes)
                {
                    if (theme.Value.Contains(stat))
                        return theme.Key;
                }
            }
            for (var i = 0; i < modifiers.Count; i++)
            {
                var stat = modifiers[i].Stat.BaseName;
                if (!_ignored.Contains(stat) && EndsWithKinSuffix(stat))
                    return AuraTheme.Kin;
            }
            return _fallback;
        }

        private bool EndsWithKinSuffix(string stat)
        {
            foreach (var suffix in _kinSuffixes)
            {
                if (stat.EndsWith(suffix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static List<KeyValuePair<AuraTheme, HashSet<string>>> ReadThemes(ContentNode node)
        {
            var themes = new List<KeyValuePair<AuraTheme, HashSet<string>>>();
            var seen = new HashSet<AuraTheme>();
            foreach (var item in node.Items())
            {
                var theme = ReadTheme(item.Field("id"));
                if (!seen.Add(theme))
                    throw item.Field("id").Error($"theme '{AuraThemes.Key(theme)}' is listed twice");
                var stats = new HashSet<string>(StringComparer.Ordinal);
                foreach (var stat in item.Field("stats").Items())
                    stats.Add(stat.AsString());
                themes.Add(new KeyValuePair<AuraTheme, HashSet<string>>(theme, stats));
            }
            if (seen.Count != AuraThemes.Count)
                throw node.Error($"expected {AuraThemes.Count} themes, got {seen.Count}");
            return themes;
        }

        private static Dictionary<string, AuraTheme> ReadOverrides(ContentNode node)
        {
            var overrides = new Dictionary<string, AuraTheme>(StringComparer.Ordinal);
            foreach (var name in node.FieldNames())
                overrides[name] = ReadTheme(node.Field(name));
            return overrides;
        }

        private static AuraTheme ReadTheme(ContentNode node)
        {
            var key = node.AsString();
            if (!AuraThemes.TryParse(key, out var theme))
                throw node.Error($"unknown aura theme '{key}'");
            return theme;
        }
    }
}
