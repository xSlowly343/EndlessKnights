using System;
using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Synergy
{
    public sealed class SynergyRules
    {
        public const string FileName = "synergy.json";
        public const int SupportedVersion = 1;

        private readonly int[] _roleMask;

        private SynergyRules(List<string> roles, int[] roleMask, IReadOnlyList<IReadOnlyList<Modifier>> roleBuffs)
        {
            Roles = roles;
            _roleMask = roleMask;
            RoleBuffs = roleBuffs;
        }

        public IReadOnlyList<string> Roles { get; }
        public IReadOnlyList<IReadOnlyList<Modifier>> RoleBuffs { get; }

        public int RoleMaskOf(HeroClass heroClass) => _roleMask[(int)heroClass];

        public int RoleMask(string role)
        {
            var index = IndexOf(role);
            return index < 0 ? 0 : 1 << index;
        }

        public int RoleMaskOf(IReadOnlyList<string> roles)
        {
            var mask = 0;
            for (var i = 0; i < roles.Count; i++)
                mask |= RoleMask(roles[i]);
            return mask;
        }

        public string RoleOf(HeroClass heroClass)
        {
            var mask = RoleMaskOf(heroClass);
            for (var index = 0; index < Roles.Count; index++)
            {
                if (mask == 1 << index)
                    return Roles[index];
            }
            return "";
        }

        public void RequirePartyRoles(Func<HeroClass, string> partyRoleOf)
        {
            foreach (HeroClass heroClass in Enum.GetValues(typeof(HeroClass)))
            {
                var expected = partyRoleOf(heroClass);
                if (RoleOf(heroClass) != expected)
                    throw new ContentException(FileName, "roles." + HeroClassNames.Of(heroClass), $"role '{RoleOf(heroClass)}', party.json says '{expected}'");
            }
        }

        private int IndexOf(string role)
        {
            for (var i = 0; i < Roles.Count; i++)
            {
                if (Roles[i] == role)
                    return i;
            }
            return -1;
        }

        public static SynergyRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var roles = new List<string>();
            var roleMask = new int[Enum.GetValues(typeof(HeroClass)).Length];
            var rolesNode = root.Field("roles");
            foreach (var className in rolesNode.FieldNames())
            {
                var heroClass = ClassOf(rolesNode.Field(className), className);
                var role = rolesNode.Field(className).AsString();
                if (!roles.Contains(role))
                    roles.Add(role);
                roleMask[(int)heroClass] = 1 << roles.IndexOf(role);
            }
            foreach (var heroClass in HeroClassNames.ByName.Values)
            {
                if (roleMask[(int)heroClass] == 0)
                    throw rolesNode.Error($"class '{HeroClassNames.Of(heroClass)}' has no role");
            }

            var buffsNode = root.Field("roleBuffs");
            var buffs = new IReadOnlyList<Modifier>[roles.Count];
            foreach (var role in buffsNode.FieldNames())
            {
                var index = roles.IndexOf(role);
                if (index < 0)
                    throw buffsNode.Field(role).Error($"unknown role '{role}', expected one of {string.Join(", ", roles)}");
                buffs[index] = ModifierReader.ReadList(buffsNode.Field(role));
            }
            for (var i = 0; i < roles.Count; i++)
            {
                if (buffs[i] == null)
                    throw buffsNode.Error($"role '{roles[i]}' has no buff");
            }
            return new SynergyRules(roles, roleMask, buffs);
        }

        private static HeroClass ClassOf(ContentNode node, string name)
        {
            if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                throw node.Error($"unknown class '{name}'");
            return heroClass;
        }
    }
}
