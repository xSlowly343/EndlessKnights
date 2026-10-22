using System.Collections.Generic;

namespace Game.Core.Stats
{
    public sealed class ModifierLayer
    {
        private readonly StatSheet _sheet;
        private readonly List<Modifier> _applied = new List<Modifier>();

        public ModifierLayer(StatSheet sheet)
        {
            _sheet = sheet;
        }

        public IReadOnlyList<Modifier> Applied => _applied;

        public void Set(IReadOnlyList<Modifier> modifiers)
        {
            foreach (var modifier in _applied)
                _sheet.Remove(modifier);
            _applied.Clear();
            _applied.AddRange(modifiers);
            _sheet.AddRange(modifiers);
        }
    }
}
