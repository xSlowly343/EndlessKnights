using Game.Core.Stats;

namespace Game.Core.Sim
{
    public readonly struct DamageBreakdown
    {
        public readonly double Attribute;
        public readonly double WeaponDamage;
        public readonly double FlatAttack;
        public readonly double Attack;
        public readonly double Strike;
        public readonly double Increased;
        public readonly double TreeBasket;
        public readonly double More;
        public readonly double Additional;
        public readonly double VsBoss;
        public readonly double Crit;
        public readonly double Defense;
        public readonly double Element;
        public readonly double Fracture;
        public readonly double Tempo;
        public readonly double Pillar;

        public DamageBreakdown(double attribute, double weaponDamage, double flatAttack, double attack, double strike, double increased, double treeBasket, double more, double additional, double vsBoss, double crit, double defense, double element, double fracture, double tempo, double pillar)
        {
            Attribute = attribute;
            WeaponDamage = weaponDamage;
            FlatAttack = flatAttack;
            Attack = attack;
            Strike = strike;
            Increased = increased;
            TreeBasket = treeBasket;
            More = more;
            Additional = additional;
            VsBoss = vsBoss;
            Crit = crit;
            Defense = defense;
            Element = element;
            Fracture = fracture;
            Tempo = tempo;
            Pillar = pillar;
        }

        public double Groups => Increased * TreeBasket * More * Additional * Crit * Defense * Element * Fracture * Tempo * Pillar;

        public double Hit => Strike * Groups;

        public double BossHit => Hit * VsBoss;

        public static DamageBreakdown Of(GameLoop loop)
        {
            var combat = loop.Combat;
            var hero = loop.Hero;
            const StatTags tags = StatTags.Strike;
            var totals = hero.TotalsOf(CombatStats.Damage, tags);
            var tree = 1 + hero.IncreasedIn(CombatStats.Damage, tags, ModifierPool.Tree);
            var critChance = combat.CritChance(tags);
            var critMultiplier = combat.CritMultiplier(tags);
            var crit = 1 + critChance * (critMultiplier - 1) + combat.Overcrit(tags) * critMultiplier * (combat.OvercritMultiplier(tags) - 1);
            var strike = (combat.BaseKeyDamage + totals.Flat) / combat.Additional;
            return new DamageBreakdown(
                combat.AttributeTotal,
                combat.WeaponDamage,
                combat.FlatAttack,
                combat.Attack,
                strike,
                totals.IncreasedMultiplier / tree,
                tree,
                totals.MoreMultiplier,
                combat.Additional,
                System.Math.Max(0, hero.Value(CombatStats.DamageVsBoss, 1, tags)),
                crit,
                combat.DefenseGain(tags),
                combat.ElementFactor(tags),
                combat.FractureTaken(),
                1 + combat.TempoBonus,
                combat.PillarMultiplier());
        }
    }
}
