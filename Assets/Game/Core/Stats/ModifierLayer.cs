using System.Collections.Generic;

namespace Game.Core.Stats
{
    public sealed class ModifierLayer
    {
        private readonly StatSheet _sheet;
        private readonly List<Modifier> _applied = new List<Modifier>();
        private readonly ModifierPool _pool;

        public ModifierLayer(StatSheet sheet, ModifierPool pool = ModifierPool.General)
        {
            _sheet = sheet;
            _pool = pool;
        }

        public ModifierPool Pool => _pool;

        public IReadOnlyList<Modifier> Applied => _applied;

        public void Set(IReadOnlyList<Modifier> modifiers)
        {
            foreach (var modifier in _applied)
                _sheet.Remove(modifier);
            _applied.Clear();
            for (var i = 0; i < modifiers.Count; i++)
                _applied.Add(_pool == ModifierPool.General ? modifiers[i] : modifiers[i].InPool(_pool));
            _sheet.AddRange(_applied);
        }
    }
}
