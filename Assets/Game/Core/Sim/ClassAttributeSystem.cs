using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClassAttributeSystem
    {
        private readonly StatSheet _hero;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>(4);
        private ClassAttributeDef _applied = ClassAttributeDef.None;
        private double _appliedPoints;

        public ClassAttributeSystem(StatSheet hero)
        {
            _hero = hero;
            _layer = new ModifierLayer(hero);
        }

        public double Points => System.Math.Max(0, _hero.Value(ClassAttributeDef.Stat, 0));

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public void Refresh(ClassAttributeDef attribute)
        {
            var points = Points;
            if (ReferenceEquals(attribute, _applied) && points == _appliedPoints)
                return;
            _applied = attribute;
            _appliedPoints = points;
            _modifiers.Clear();
            if (points > 0)
            {
                foreach (var modifier in attribute.PerPoint)
                    _modifiers.Add(modifier.WithValue(modifier.Value * points));
            }
            _layer.Set(_modifiers);
        }
    }
}
