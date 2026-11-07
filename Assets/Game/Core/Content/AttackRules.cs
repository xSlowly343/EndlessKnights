using System;
using System.Collections.Generic;
using Game.Core.Content.Items;

namespace Game.Core.Content
{
    public sealed class AttackRules
    {
        public double BaseAttribute { get; private set; } = 1;
        public double BaseWeaponDamage { get; private set; } = 6;
        public double Divisor { get; private set; } = 6;
        public double PerItemLevel { get; private set; } = 1;
        public IReadOnlyList<double> WeaponRarity { get; private set; } = Array.Empty<double>();
        public double GearAttribute { get; private set; }
        public IReadOnlyList<double> GearRarity { get; private set; } = Array.Empty<double>();

        public double BaseAttack => System.Math.Sqrt(BaseAttribute * BaseWeaponDamage / Divisor);

        public double Attack(double attribute, double weaponDamage, double flat) =>
            System.Math.Sqrt(System.Math.Max(0, attribute) * System.Math.Max(0, weaponDamage) / Divisor) + flat;

        public double WeaponDamage(ItemRarity rarity, int itemLevel) => BaseWeaponDamage * Of(WeaponRarity, rarity) * Level(itemLevel);

        public double Attribute(ItemRarity rarity, int itemLevel) => GearAttribute * Of(GearRarity, rarity) * Level(itemLevel);

        private double Level(int itemLevel) => System.Math.Pow(PerItemLevel, System.Math.Max(0, itemLevel - 1));

        private static double Of(IReadOnlyList<double> table, ItemRarity rarity) =>
            table.Count == 0 ? 1 : table[System.Math.Min(table.Count - 1, (int)rarity)];

        internal static AttackRules Read(ContentNode node) => new AttackRules
        {
            BaseAttribute = node.Field("baseAttribute").AsNumber(1, 1e9),
            BaseWeaponDamage = node.Field("weaponDamage").AsNumber(1, 1e9),
            Divisor = node.Field("divisor").AsNumber(0.001, 1e6),
            PerItemLevel = node.Field("perItemLevel").AsNumber(1, 2),
            WeaponRarity = Table(node.Field("weaponRarity")),
            GearAttribute = node.Field("gearAttribute").AsNumber(0, 1e9),
            GearRarity = Table(node.Field("gearRarity"))
        };

        private static IReadOnlyList<double> Table(ContentNode node)
        {
            var list = new List<double>();
            foreach (var item in node.Items())
                list.Add(item.AsNumber(0, 1e6));
            return list;
        }
    }
}
