using System;
using System.Collections.Generic;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class WeaponSwap
    {
        private WeaponSwap(WeaponBase? replaced, IReadOnlyList<string> lightUp, IReadOnlyList<string> goOut)
        {
            Replaced = replaced;
            LightUp = lightUp;
            GoOut = goOut;
        }

        public WeaponBase? Replaced { get; }
        public IReadOnlyList<string> LightUp { get; }
        public IReadOnlyList<string> GoOut { get; }

        public static WeaponSwap For(GameState state, WeaponBase candidate, WeaponHand hand)
        {
            var weapons = state.Weapons;
            var replaced = weapons.In(hand);
            var other = weapons.In(hand == WeaponHand.Right ? WeaponHand.Left : WeaponHand.Right);
            var lightUp = new List<string>();
            var goOut = new List<string>();
            foreach (var slot in state.Skills)
            {
                var type = slot.Skill.Skill?.Weapon;
                if (type == null)
                    continue;
                var heldNow = weapons.Holds(type);
                var heldAfter = Is(candidate, type) || Is(other, type);
                var name = Texts.Pick(slot.Skill.NameRu, slot.Skill.NameEn);
                if (!heldNow && heldAfter && !lightUp.Contains(name))
                    lightUp.Add(name);
                else if (heldNow && !heldAfter && !goOut.Contains(name))
                    goOut.Add(name);
            }
            return new WeaponSwap(replaced, lightUp, goOut);
        }

        private static bool Is(WeaponBase? weapon, string typeId) =>
            weapon != null && string.Equals(weapon.Type.Id, typeId, StringComparison.Ordinal);
    }
}
