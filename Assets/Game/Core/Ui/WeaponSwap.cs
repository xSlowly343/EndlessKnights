using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Weapons;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class WeaponSwap
    {
        private WeaponSwap(WeaponItem? replaced, IReadOnlyList<string> lightUp, IReadOnlyList<string> goOut)
        {
            Replaced = replaced;
            LightUp = lightUp;
            GoOut = goOut;
        }

        public WeaponItem? Replaced { get; }
        public IReadOnlyList<string> LightUp { get; }
        public IReadOnlyList<string> GoOut { get; }

        public static WeaponSwap For(GameState state, WeaponItem candidate, WeaponHand hand, IReadOnlyList<int> unlockLevels)
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
                var heldNow = Opens(state, slot.Skill, weapons.Right, unlockLevels) || Opens(state, slot.Skill, weapons.Left, unlockLevels);
                var heldAfter = Opens(state, slot.Skill, candidate, unlockLevels) || Opens(state, slot.Skill, other, unlockLevels);
                var name = Texts.Pick(slot.Skill.NameRu, slot.Skill.NameEn);
                if (!heldNow && heldAfter && !lightUp.Contains(name))
                    lightUp.Add(name);
                else if (heldNow && !heldAfter && !goOut.Contains(name))
                    goOut.Add(name);
            }
            return new WeaponSwap(replaced, lightUp, goOut);
        }

        private static bool Opens(GameState state, GemDef skill, WeaponItem? weapon, IReadOnlyList<int> unlockLevels)
        {
            if (weapon == null)
                return false;
            if (skill.Ascendancy != null)
                return string.Equals(weapon.Type.Id, skill.Skill!.Weapon, StringComparison.Ordinal);
            return weapon.Opens(skill.Id, state.HeroLevel, unlockLevels);
        }
    }
}
