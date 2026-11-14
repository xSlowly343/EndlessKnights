using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public enum PartyGrowthSource
    {
        Tree,
        Seals,
        Relics,
        Mascot,
        Rites,
        Other
    }

    public readonly struct PartyGrowthSummary
    {
        public const int Sources = 6;
        private static readonly StatId VsBossStat = new StatId("damageVsBoss");
        private static readonly StatId VsGuardianStat = new StatId("damageVsGuardian");

        private readonly double[] _bySource;

        private PartyGrowthSummary(PartyGrowthLayer layer)
        {
            InParty = layer.InParty;
            Share = layer.Share;
            Capped = layer.Capped;
            Damage = layer.DamageMultiplier;
            Peaceful = layer.Peaceful;
            Colossus = layer.ColossusPower;
            MascotAura = layer.MascotAura;
            var applied = layer.Applied;
            VsBoss = Factor(applied, VsBossStat, StatTags.None, ModifierCondition.Always);
            VsGuardian = Factor(applied, VsGuardianStat, StatTags.None, ModifierCondition.Always);
            Keys = Factor(applied, CombatStats.Damage, StatTags.Strike, ModifierCondition.Always);
            Mouse = Factor(applied, CombatStats.Damage, StatTags.Mouse, ModifierCondition.Always);
            Skills = Factor(applied, CombatStats.Damage, StatTags.Skill, ModifierCondition.Always);
            Together = Factor(applied, CombatStats.Damage, StatTags.None, ModifierCondition.InParty);
            RoleBuffs = Factor(applied, CombatStats.RoleBuffEffect, StatTags.None, ModifierCondition.Always);
            CritChance = Flat(applied, CombatStats.CritChance);
            var share = layer.Share;
            _bySource = new double[Sources];
            _bySource[(int)PartyGrowthSource.Tree] = Scaled(layer.Tree, share);
            _bySource[(int)PartyGrowthSource.Seals] = 1 + layer.Seals * share;
            _bySource[(int)PartyGrowthSource.Relics] = Scaled(layer.Relics, share);
            _bySource[(int)PartyGrowthSource.Mascot] = Scaled(layer.Mascot, share);
            _bySource[(int)PartyGrowthSource.Rites] = Scaled(layer.Rites, share);
            _bySource[(int)PartyGrowthSource.Other] = Scaled(layer.Sources, share);
        }

        public bool InParty { get; }
        public double Share { get; }
        public bool Capped { get; }
        public double Damage { get; }
        public double Peaceful { get; }
        public double Colossus { get; }
        public double MascotAura { get; }
        public double VsBoss { get; }
        public double VsGuardian { get; }
        public double Keys { get; }
        public double Mouse { get; }
        public double Skills { get; }
        public double Together { get; }
        public double RoleBuffs { get; }
        public double CritChance { get; }

        public double Of(PartyGrowthSource source) => _bySource == null ? 1 : _bySource[(int)source];

        public static PartyGrowthSummary Of(PartyGrowthLayer layer) => new PartyGrowthSummary(layer);

        private static double Scaled(IReadOnlyList<Modifier> lines, double share)
        {
            var increased = 0.0;
            var more = 1.0;
            foreach (var line in lines)
            {
                if (line.Stat != CombatStats.Damage || line.Tags != StatTags.None || line.When != ModifierCondition.Always)
                    continue;
                if (line.Kind == ModifierKind.Increased)
                    increased += line.Value * share;
                else if (line.Kind == ModifierKind.More)
                    more *= 1 + line.Value * share;
            }
            return (1 + increased) * more;
        }

        private static double Factor(IReadOnlyList<Modifier> applied, StatId stat, StatTags tags, ModifierCondition when)
        {
            var increased = 0.0;
            var more = 1.0;
            foreach (var line in applied)
            {
                if (line.Stat != stat || line.Tags != tags || line.When != when)
                    continue;
                if (line.Kind == ModifierKind.Increased)
                    increased += line.Value;
                else if (line.Kind == ModifierKind.More)
                    more *= 1 + line.Value;
            }
            return System.Math.Max(0, (1 + increased) * more);
        }

        private static double Flat(IReadOnlyList<Modifier> applied, StatId stat)
        {
            var total = 0.0;
            foreach (var line in applied)
            {
                if (line.Stat == stat && line.Kind == ModifierKind.Flat && line.Tags == StatTags.None && line.When == ModifierCondition.Always)
                    total += line.Value;
            }
            return total;
        }
    }
}
