using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class MasterCircle
    {
        public static void Hands(GameState state, List<WeaponHand> into)
        {
            into.Clear();
            if (state.Weapons.Right != null)
                into.Add(WeaponHand.Right);
            if (state.Weapons.Left != null)
                into.Add(WeaponHand.Left);
        }

        public static void Fill(WeaponItem weapon, int heroLevel, IReadOnlyList<int> unlockLevels, GemCatalog gems, SkillBook book, List<MasterPlace> into)
        {
            into.Clear();
            for (var place = 0; place < weapon.Skills.Count; place++)
            {
                if (!gems.TryGet(weapon.Skills[place], out var gem))
                    continue;
                var open = place >= unlockLevels.Count || heroLevel >= unlockLevels[place];
                into.Add(new MasterPlace(place, gem, open, book.RowOf(gem)));
            }
        }

        public static (double X, double Y) Position(int index, int count, double radius)
        {
            var angle = -System.Math.PI / 2 + index * 2 * System.Math.PI / System.Math.Max(1, count);
            return (radius * System.Math.Cos(angle), radius * System.Math.Sin(angle));
        }
    }
}
