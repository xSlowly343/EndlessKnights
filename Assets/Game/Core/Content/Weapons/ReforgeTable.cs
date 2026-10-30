using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;

namespace Game.Core.Content.Weapons
{
    public enum ReforgeKey
    {
        Tier,
        Role,
        Color
    }

    public sealed class ReforgeTable
    {
        private static readonly Dictionary<string, ReforgeKey> KeyNames = new Dictionary<string, ReforgeKey>(StringComparer.Ordinal)
        {
            ["tier"] = ReforgeKey.Tier,
            ["role"] = ReforgeKey.Role,
            ["color"] = ReforgeKey.Color
        };

        private readonly Dictionary<string, Dictionary<string, GemDef>> _maps = new Dictionary<string, Dictionary<string, GemDef>>(StringComparer.Ordinal);

        public ReforgeTable(IReadOnlyList<ReforgeKey> keys)
        {
            Keys = keys;
        }

        public IReadOnlyList<ReforgeKey> Keys { get; }

        public static ReforgeTable Read(ContentNode node)
        {
            var keys = new List<ReforgeKey>();
            foreach (var item in node.Field("gemKeys").Items())
            {
                if (!KeyNames.TryGetValue(item.AsString(), out var key))
                    throw item.Error($"unknown reforge key '{item.AsString()}', expected tier, role or color");
                if (keys.Contains(key))
                    throw item.Error($"reforge key '{item.AsString()}' is listed twice");
                keys.Add(key);
            }
            if (keys.Count == 0 || keys[0] != ReforgeKey.Tier)
                throw node.Field("gemKeys").Error("the first reforge key must be tier");
            return new ReforgeTable(keys);
        }

        public void Bind(IReadOnlyList<WeaponType> types, IReadOnlyDictionary<string, List<GemDef>> classGems)
        {
            _maps.Clear();
            for (var left = 0; left < types.Count; left++)
            {
                for (var right = left + 1; right < types.Count; right++)
                {
                    var pairs = Match(classGems[types[left].Id], classGems[types[right].Id]);
                    var forward = new Dictionary<string, GemDef>(StringComparer.Ordinal);
                    var backward = new Dictionary<string, GemDef>(StringComparer.Ordinal);
                    foreach (var pair in pairs)
                    {
                        forward[pair.Key.Id] = pair.Value;
                        backward[pair.Value.Id] = pair.Key;
                    }
                    _maps[MapKey(types[left], types[right])] = forward;
                    _maps[MapKey(types[right], types[left])] = backward;
                }
            }
        }

        public GemDef? Gem(GemDef gem, WeaponType from, WeaponType to)
        {
            if (gem.Ascendancy != null || !string.Equals(gem.Skill?.Weapon, from.Id, StringComparison.Ordinal))
                return null;
            return _maps.TryGetValue(MapKey(from, to), out var map) && map.TryGetValue(gem.Id, out var found) ? found : null;
        }

        public string SkillId(string skillId, WeaponType from, WeaponType to) =>
            _maps.TryGetValue(MapKey(from, to), out var map) && map.TryGetValue(skillId, out var found) ? found.Id : skillId;

        private static string MapKey(WeaponType from, WeaponType to) => from.Id + ">" + to.Id;

        private Dictionary<GemDef, GemDef> Match(List<GemDef> left, List<GemDef> right)
        {
            var leftOrder = UnlockedFirst(left);
            var rightOrder = UnlockedFirst(right);
            var pairs = new Dictionary<GemDef, GemDef>();
            var taken = new HashSet<GemDef>();
            for (var depth = Keys.Count; depth >= 1; depth--)
            {
                foreach (var gem in leftOrder)
                {
                    if (pairs.ContainsKey(gem))
                        continue;
                    foreach (var candidate in rightOrder)
                    {
                        if (!taken.Contains(candidate) && Same(gem, candidate, depth))
                        {
                            pairs[gem] = candidate;
                            taken.Add(candidate);
                            break;
                        }
                    }
                }
            }
            foreach (var gem in leftOrder)
            {
                if (pairs.ContainsKey(gem))
                    continue;
                var nearest = Nearest(gem, rightOrder, taken);
                if (nearest == null)
                    continue;
                pairs[gem] = nearest;
                taken.Add(nearest);
            }
            return pairs;
        }

        private static GemDef? Nearest(GemDef gem, List<GemDef> candidates, HashSet<GemDef> taken)
        {
            GemDef? best = null;
            var bestScore = int.MaxValue;
            foreach (var candidate in candidates)
            {
                if (taken.Contains(candidate))
                    continue;
                var gap = (int)candidate.Skill!.Tier - (int)gem.Skill!.Tier;
                var score = System.Math.Abs(gap) * 2 + (gap > 0 ? 1 : 0);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool Same(GemDef a, GemDef b, int depth)
        {
            for (var i = 0; i < depth; i++)
            {
                var same = Keys[i] switch
                {
                    ReforgeKey.Tier => a.Skill!.Tier == b.Skill!.Tier,
                    ReforgeKey.Role => a.Skill!.Role == b.Skill!.Role,
                    _ => a.Color == b.Color
                };
                if (!same)
                    return false;
            }
            return true;
        }

        private static List<GemDef> UnlockedFirst(List<GemDef> gems)
        {
            var ordered = new List<GemDef>(gems.Count);
            foreach (var gem in gems)
            {
                if (!gem.Locked)
                    ordered.Add(gem);
            }
            foreach (var gem in gems)
            {
                if (gem.Locked)
                    ordered.Add(gem);
            }
            return ordered;
        }
    }
}
