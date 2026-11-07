using Game.Core.Content;
using Game.Core.Content.Items;
using Game.Core.Content.Weapons;

namespace Game.Core.Sim
{
    public enum ForgeResult
    {
        Done,
        Closed,
        NoItem,
        Busy,
        NoOre,
        Maxed,
        NotSlain
    }

    public sealed class ForgeSystem
    {
        public const string Activity = "forge";

        private readonly GameState _state;
        private readonly StageTable _stages;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly WeaponSystem _weapons;
        private readonly GearSystem _gear;
        private readonly EventQueue _events;
        private ForgeRules _rules;

        public ForgeSystem(ForgeRules rules, GameState state, StageTable stages, CampSystem camp, PeacefulSystem peaceful, WeaponSystem weapons, GearSystem gear, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _stages = stages;
            _camp = camp;
            _peaceful = peaceful;
            _weapons = weapons;
            _gear = gear;
            _events = events;
        }

        public ForgeRules Rules => _rules;
        public ForgeState State => _state.Forge;
        public HideoutCrafts? Shared { get; set; }
        public bool Open => _peaceful.InHideout ? _state.Hideout.Forge.Built : _camp.Open(_rules.Building);
        public bool Together => _peaceful.InHideout && _state.Hideout.Forge.Together && Shared != null;
        public bool Busy => State.Target != ForgeTarget.None;

        public void UseRules(ForgeRules rules) => _rules = rules;

        public string OreOf(int itemLevel)
        {
            var rotation = _stages.Rotation;
            return rotation.Count == 0 ? string.Empty : _rules.OreOf(rotation[(System.Math.Max(1, itemLevel) - 1) % rotation.Count]);
        }

        public int SmeltYield(ItemRarity rarity) => (int)System.Math.Round((_rules.SmeltBase + _rules.SmeltPerRarity * (int)rarity) * (1 + _state.Perk(MuseumPerk.MoreOre)) * _state.PeacefulBonus.YieldFactor);

        public double HammersNeeded => System.Math.Ceiling(_rules.Hammers * System.Math.Max(0.1, 1 - _state.Perk(MuseumPerk.FewerHammers)) * _state.PeacefulBonus.TimeFactor * (Together ? 1 - Shared!.HammersShare : 1));

        public double ZoneFrom => System.Math.Max(0, _rules.ZoneFrom - _state.Perk(MuseumPerk.WiderZone) / 2);

        public double ZoneTo => System.Math.Min(1, _rules.ZoneTo + _state.Perk(MuseumPerk.WiderZone) / 2);

        public bool InZone(double heat) => heat >= ZoneFrom && heat <= ZoneTo;

        public ForgeResult SmeltGear(int index)
        {
            if (!Open)
                return ForgeResult.Closed;
            var bag = _state.Drops.Gear;
            if (index < 0 || index >= bag.Count)
                return ForgeResult.NoItem;
            var item = bag[index];
            bag.RemoveAt(index);
            Smelt(item.ItemLevel, item.Rarity);
            return ForgeResult.Done;
        }

        public ForgeResult SmeltWeapon(int index)
        {
            if (!Open)
                return ForgeResult.Closed;
            var bag = _state.Weapons.Bag;
            if (index < 0 || index >= bag.Count)
                return ForgeResult.NoItem;
            var weapon = bag[index];
            bag.RemoveAt(index);
            Smelt(weapon.ItemLevel, weapon.Rarity);
            return ForgeResult.Done;
        }

        public ForgeResult TemperWeapon() => Start(ForgeTarget.Weapon, default, ForgeWork.Temper);

        public ForgeResult TemperGear(GearSlot slot) => Start(ForgeTarget.Gear, slot, ForgeWork.Temper);

        public ForgeResult HoneWeapon() => Start(ForgeTarget.Weapon, default, ForgeWork.Hone);

        public ForgeResult HoneGear(GearSlot slot) => Start(ForgeTarget.Gear, slot, ForgeWork.Hone);

        public ForgeMark? TargetMark => State.Target switch
        {
            ForgeTarget.Weapon => _state.Weapons.Held?.Forge,
            ForgeTarget.Gear => _state.Gear.In(State.Slot)?.Forge,
            _ => null
        };

        public ForgeResult Brand(string enemyId)
        {
            if (!Open)
                return ForgeResult.Closed;
            var weapon = _state.Weapons.Held;
            if (weapon == null)
                return ForgeResult.NoItem;
            if (!weapon.Forge.Slain.Contains(enemyId))
                return ForgeResult.NotSlain;
            weapon.Forge.Brand = enemyId;
            return ForgeResult.Done;
        }

        public void Tick()
        {
            if (!Busy || !_peaceful.InCamp || _peaceful.Activity != Activity || (_peaceful.InHideout && !_state.Hideout.Forge.Built))
                return;
            var mark = TargetMark;
            if (mark == null)
            {
                Clear();
                return;
            }
            var work = _peaceful.Work(Activity);
            State.Hammers = System.Math.Min(HammersNeeded, State.Hammers + work.Keys);
            State.Heat = System.Math.Max(0, System.Math.Min(1, State.Heat + work.MouseMeters * _rules.HeatPerMeter - _rules.HeatDecayPerSecond * System.Math.Max(0, 1 - _state.Perk(MuseumPerk.SlowerCooling)) * GameLoop.TickSeconds));
            var quench = State.Hammers >= HammersNeeded && work.Clicks > 0;
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            if (quench)
                Quench(mark);
        }

        public void GuestStrike(int hammers)
        {
            if (Busy)
                State.Hammers = System.Math.Min(_rules.Hammers, State.Hammers + hammers);
            else
                State.GuestHammers = System.Math.Min(_rules.Hammers, State.GuestHammers + hammers);
        }

        public void NoteKills(int firstEvent)
        {
            var weapon = _state.Weapons.Held;
            if (weapon == null)
                return;
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind != GameEventKind.EnemyKilled || gameEvent.Enemy?.Type != EnemyType.Guardian)
                    continue;
                var id = gameEvent.Enemy.Id;
                if (!weapon.Forge.Slain.Contains(id))
                    weapon.Forge.Slain.Add(id);
            }
        }

        public void SalvageOre(ItemDrop drop)
        {
            var itemLevel = drop.Weapon?.ItemLevel ?? drop.Gear?.ItemLevel ?? 1;
            var ore = SmeltYield(drop.Rarity);
            _camp.Add(OreOf(itemLevel), ore);
            State.SalvageOre += ore;
        }

        private void Smelt(int itemLevel, ItemRarity rarity)
        {
            var ore = OreOf(itemLevel);
            _camp.Add(ore, SmeltYield(rarity));
            State.Smelted++;
            _events.Add(GameEvent.ForStage(GameEventKind.ItemSmelted, _state.Tick, SmeltYield(rarity)));
        }

        private ForgeResult Start(ForgeTarget target, GearSlot slot, ForgeWork work)
        {
            if (!Open)
                return ForgeResult.Closed;
            if (Busy)
                return ForgeResult.Busy;
            var itemLevel = target == ForgeTarget.Weapon ? _state.Weapons.Held?.ItemLevel : _state.Gear.In(slot)?.ItemLevel;
            var mark = target == ForgeTarget.Weapon ? _state.Weapons.Held?.Forge : _state.Gear.In(slot)?.Forge;
            if (itemLevel == null || mark == null || (target == ForgeTarget.Gear && !_state.Gear.In(slot)!.Wearable))
                return ForgeResult.NoItem;
            var hone = work == ForgeWork.Hone;
            if (hone ? mark.Hone >= _rules.MaxHone - 1e-12 : mark.Temper >= _rules.MaxTemper - 1e-12)
                return ForgeResult.Maxed;
            var ore = OreOf(itemLevel.Value);
            if (!_camp.Take(ore, hone ? _rules.HoneOre : _rules.TemperOre))
                return ForgeResult.NoOre;
            State.Work = work;
            State.Target = target;
            State.Slot = slot;
            State.Hammers = State.GuestHammers;
            State.GuestHammers = 0;
            State.Heat = 0;
            return ForgeResult.Done;
        }

        private void Quench(ForgeMark mark)
        {
            var sure = State.Work == ForgeWork.Temper && _state.Companions.SureForge;
            if (sure)
                _state.Companions.SureForge = false;
            var inZone = sure || InZone(State.Heat) || _state.PeacefulBonus.RollMastery(_state.Rng);
            var boost = 1 + _state.Perk(MuseumPerk.TemperStep);
            var masterpiece = false;
            if (Together)
            {
                boost *= 1 + Shared!.StepBoost;
                masterpiece = _state.Rng.NextDouble() < Shared.MasterpieceChance;
                if (masterpiece)
                {
                    boost *= Shared.MasterpieceBoost;
                    mark.Masters.Clear();
                    mark.Masters.AddRange(_state.Hideout.Forge.Partners);
                    State.Masterpieces++;
                }
            }
            if (State.Work == ForgeWork.Hone)
                mark.Hone = System.Math.Min(_rules.MaxHone, mark.Hone + (inZone ? _rules.HoneStepInZone : _rules.HoneStepOutside) * boost);
            else
                mark.Temper = System.Math.Min(_rules.MaxTemper, mark.Temper + (inZone ? _rules.StepInZone : _rules.StepOutside) * boost);
            if (State.Target == ForgeTarget.Weapon)
                _weapons.Refresh();
            else
                _gear.Rebuild();
            State.Tempered++;
            _events.Add(GameEvent.ForStage(GameEventKind.ItemTempered, _state.Tick, inZone ? 1 : 0));
            if (masterpiece)
                _events.Add(GameEvent.ForStage(GameEventKind.PartyMasterpiece, _state.Tick, 0));
            Clear();
        }

        private void Clear()
        {
            State.Target = ForgeTarget.None;
            State.Hammers = 0;
            State.Heat = 0;
        }
    }
}
