using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Headless
{
    public sealed class BotGear
    {
        public const ItemRarity SalvageUpTo = ItemRarity.Uncommon;
        private const double DustReserve = 0;
        private const double SupportGain = 1.01;
        private const int RerollsPerAct = 3;

        private readonly GameLoop _loop;
        private readonly NodeScore _score;
        private readonly BranchTrial? _trial;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private readonly List<GemDef> _candidates = new List<GemDef>();
        private string _supportsChecked = string.Empty;

        public BotGear(GameLoop loop, NodeScore score, BranchTrial? trial)
        {
            _loop = loop;
            _score = score;
            _trial = trial;
        }

        public void Act()
        {
            var drops = _loop.State.Drops;
            if (drops.SalvageUpTo == null)
                drops.SalvageUpTo = SalvageUpTo;
            WearBestGear();
            HoldBestWeapon();
            Reroll();
            PlaceSupports();
        }

        public double ScoreOf(GearItem? item)
        {
            if (item == null)
                return 0;
            _modifiers.Clear();
            item.AddModifiers(_modifiers);
            return _score.Of(_modifiers);
        }

        public double ScoreOf(WeaponItem? weapon)
        {
            if (weapon == null)
                return 0;
            _modifiers.Clear();
            _modifiers.AddRange(weapon.Innate);
            foreach (var affix in weapon.Affixes)
                affix.AddModifiers(_modifiers);
            if (weapon.Unique != null)
                _modifiers.AddRange(weapon.Unique.Lines);
            return _score.Of(_modifiers);
        }

        private void WearBestGear()
        {
            var gear = _loop.Gear;
            for (var i = gear.Bag.Count - 1; i >= 0; i--)
            {
                if (i >= gear.Bag.Count)
                    continue;
                var item = gear.Bag[i];
                if (!item.Wearable)
                    continue;
                var slot = WeakestSlotFor(item);
                if (ScoreOf(item) > ScoreOf(gear.In(slot)))
                    gear.Equip(slot, item);
            }
        }

        private GearSlot WeakestSlotFor(GearItem item)
        {
            if (item.Slot != ItemSlot.Ring)
                return GearState.SlotFor(item.Slot);
            var gear = _loop.Gear;
            return ScoreOf(gear.In(GearSlot.RingLeft)) <= ScoreOf(gear.In(GearSlot.RingRight)) ? GearSlot.RingLeft : GearSlot.RingRight;
        }

        private void HoldBestWeapon()
        {
            var weapons = _loop.Weapons;
            WeaponItem? best = null;
            var bestScore = ScoreOf(weapons.Held);
            foreach (var weapon in weapons.Bag)
            {
                if (!weapons.Fits(weapon))
                    continue;
                var score = ScoreOf(weapon);
                if (score > bestScore)
                {
                    best = weapon;
                    bestScore = score;
                }
            }
            if (best != null)
                weapons.Equip(best);
        }

        private void Reroll()
        {
            var reroll = _loop.Reroll;
            for (var i = 0; i < RerollsPerAct; i++)
            {
                if (_loop.State.Drops.Dust <= DustReserve || !RerollBest(reroll))
                    return;
            }
        }

        private bool RerollBest(RerollSystem reroll)
        {
            GearItem? target = null;
            foreach (var item in _loop.State.Gear.Worn)
            {
                if (item == null || reroll.Check(item) != RerollResult.Offered)
                    continue;
                if (target == null || item.Rarity > target.Rarity)
                    target = item;
            }
            if (target != null)
                return Decide(reroll, reroll.Reroll(target) == RerollResult.Offered, ScoreOf(target), offer => ScoreOf(target.WithAffixes(offer.After)));
            var held = _loop.Weapons.Held;
            if (held == null || reroll.Check(held) != RerollResult.Offered)
                return false;
            return Decide(reroll, reroll.Reroll(held) == RerollResult.Offered, ScoreOf(held), offer => ScoreOf(held.WithAffixes(offer.After)));
        }

        private static bool Decide(RerollSystem reroll, bool offered, double before, System.Func<RerollOffer, double> after)
        {
            if (!offered || reroll.Offer == null)
                return false;
            if (after(reroll.Offer) > before)
                reroll.TakeNew();
            else
                reroll.KeepOld();
            return true;
        }

        private void PlaceSupports()
        {
            if (_trial == null)
                return;
            var signature = Signature();
            if (signature == _supportsChecked)
                return;
            _supportsChecked = signature;
            var book = _loop.Book;
            var catalog = _loop.Data.Gems.All;
            for (var index = 0; index < _loop.State.Skills.Count; index++)
            {
                for (var socket = 0; socket < book.SocketsAt(index); socket++)
                {
                    if (_loop.State.Skills[index].Supports[socket] != null)
                        continue;
                    book.SupportsFor(index, catalog, _candidates);
                    PlaceBest(index, socket);
                }
            }
        }

        private void PlaceBest(int index, int socket)
        {
            var empty = _trial!.DamageAfter(_loop.State, loop => true);
            GemDef? best = null;
            var bestDamage = empty * BigNumber.FromDouble(SupportGain);
            foreach (var support in _candidates)
            {
                var damage = _trial.DamageAfter(_loop.State, loop => loop.Book.SetSupport(index, socket, support) == Build.BookResult.Done);
                if (damage > bestDamage)
                {
                    best = support;
                    bestDamage = damage;
                }
            }
            if (best != null)
                _loop.Book.SetSupport(index, socket, best);
        }

        private string Signature()
        {
            var parts = new System.Text.StringBuilder();
            _loop.Book.OpenSupports(_loop.Data.Gems.All, _candidates);
            parts.Append(_candidates.Count);
            for (var index = 0; index < _loop.State.Skills.Count; index++)
                parts.Append('|').Append(_loop.State.Skills[index].Skill.Id).Append(':').Append(_loop.Book.SocketsAt(index));
            return parts.ToString();
        }
    }
}
