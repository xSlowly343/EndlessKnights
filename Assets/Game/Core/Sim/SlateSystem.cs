using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Slates;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class SlateSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private readonly int[] _pending = new int[SlateRarityNames.Count];
        private SlateRules _rules;

        public SlateSystem(SlateRules rules, GameState state, StatSheet hero, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _events = events;
            _layer = new ModifierLayer(hero);
        }

        public SlateRules Rules
        {
            get => _rules;
            set
            {
                _rules = value;
                Rebuild();
            }
        }

        public Unlocks? Unlocks { get; set; }
        public bool Open => Unlocks?.IsOpen(Feature.Slates) != false;
        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;
        public IReadOnlyList<SlateItem> Bag => _state.Slates.Bag;
        public IReadOnlyList<SlatePlacement> Board => _state.Slates.Board;
        public (int Width, int Height) Size => _rules.BoardSize(_state.GatesCleared.Count);
        public double DropBonus => System.Math.Max(0, _hero.Value(CombatStats.SlateChance, 1));

        public void OnRecordBoss(long stage, EnemyType type, int biome)
        {
            if (!Open)
                return;
            var drops = _rules.Drops;
            if (type == EnemyType.Guardian)
                RollDrop(drops.GuardianChance, drops.GuardianMinRarity);
            else
                RollDrop(drops.BossChanceAt(biome), SlateRarity.Common);
        }

        public void OnDungeon() => RollIfOpen(_rules.Drops.DungeonChance);

        public void OnRiftBoss() => RollIfOpen(_rules.Drops.RiftChance);

        private void RollIfOpen(double chance)
        {
            if (Open)
                RollDrop(chance, SlateRarity.Common);
        }

        private void RollDrop(double chance, SlateRarity minRarity)
        {
            if (chance <= 0 || !_state.Slates.Rng.Chance(System.Math.Min(1, chance * DropBonus)))
                return;
            _pending[(int)minRarity]++;
        }

        public int Pending
        {
            get
            {
                var count = 0;
                foreach (var pending in _pending)
                    count += pending;
                return count;
            }
        }

        public void Flush()
        {
            for (var rarity = 0; rarity < _pending.Length; rarity++)
            {
                for (; _pending[rarity] > 0; _pending[rarity]--)
                    Give(Roll((SlateRarity)rarity));
            }
        }

        public SlateItem Roll(SlateRarity minRarity)
        {
            var rng = _state.Slates.Rng;
            var shape = _rules.Shapes[rng.NextInt(0, _rules.Shapes.Count)];
            var theme = _rules.Themes[rng.NextInt(0, _rules.Themes.Count)];
            var rarity = RollRarity(minRarity, rng);
            return Roll(shape, theme, rarity, rng);
        }

        public SlateItem Roll(SlateShape shape, SlateTheme theme, SlateRarity rarity) => Roll(shape, theme, rarity, _state.Slates.Rng);

        private SlateItem Roll(SlateShape shape, SlateTheme theme, SlateRarity rarity, Rng rng)
        {
            var rules = _rules.Rarity(rarity);
            var count = rules.LinesFor(shape);
            var lines = new List<SlateLine>(count);
            if (rules.Rule)
                lines.Add(theme.Rule);
            while (lines.Count < count)
                lines.Add(PickLine(theme, lines, rng));
            var classLines = _state.ClassChosen ? _rules.ClassLines(_state.Class) : System.Array.Empty<SlateLine>();
            if (classLines.Count > 0 && rng.Chance(_rules.ClassLineChance))
                lines[lines.Count - 1] = classLines[rng.NextInt(0, classLines.Count)];
            return new SlateItem(shape, theme, rarity, lines, false);
        }

        private static SlateLine PickLine(SlateTheme theme, List<SlateLine> taken, Rng rng)
        {
            var free = 0;
            foreach (var line in theme.Lines)
            {
                if (!taken.Contains(line))
                    free++;
            }
            if (free == 0)
                return theme.Lines[rng.NextInt(0, theme.Lines.Count)];
            var pick = rng.NextInt(0, free);
            foreach (var line in theme.Lines)
            {
                if (taken.Contains(line))
                    continue;
                if (pick-- == 0)
                    return line;
            }
            return theme.Lines[0];
        }

        private SlateRarity RollRarity(SlateRarity minRarity, Rng rng)
        {
            var find = System.Math.Max(0, _hero.Value(CombatStats.SlateRarity, 1));
            var total = 0.0;
            for (var i = (int)minRarity; i < _rules.Rarities.Count; i++)
                total += Weight(i, find);
            if (total <= 0)
                return minRarity;
            var roll = rng.NextDouble() * total;
            for (var i = (int)minRarity; i < _rules.Rarities.Count; i++)
            {
                roll -= Weight(i, find);
                if (roll < 0)
                    return (SlateRarity)i;
            }
            return (SlateRarity)(_rules.Rarities.Count - 1);
        }

        private double Weight(int rarity, double find) => _rules.Rarities[rarity].Weight * (rarity > (int)Content.Slates.SlateRarity.Common ? find : 1);

        public void Give(SlateItem slate)
        {
            var slates = _state.Slates;
            slates.Found++;
            if (slates.Bag.Count >= _rules.BagSize)
                _state.Drops.Dust += _rules.Rarity(slate.Rarity).Dust;
            else
                slates.Bag.Add(slate);
            _events.Add(GameEvent.WithAmount(GameEventKind.SlateDropped, _state.Tick, BigNumber.FromDouble((int)slate.Rarity), _state.Stage));
        }

        public SlateResult Place(SlateItem slate, int x, int y, int rotation)
        {
            if (!Open)
                return SlateResult.Locked;
            if (!_state.Slates.Bag.Contains(slate))
                return SlateResult.NotInBag;
            var fit = Fits(slate, x, y, rotation, null);
            if (fit != SlateResult.Done)
                return fit;
            _state.Slates.Bag.Remove(slate);
            _state.Slates.Board.Add(new SlatePlacement(slate, x, y, rotation));
            Rebuild();
            return SlateResult.Done;
        }

        public SlateResult Fits(SlateItem slate, int x, int y, int rotation) => Fits(slate, x, y, rotation, null);

        private SlateResult Fits(SlateItem slate, int x, int y, int rotation, SlatePlacement? ignore)
        {
            var (width, height) = Size;
            foreach (var (cellX, cellY) in slate.Shape.Cells(rotation))
            {
                var cx = x + cellX;
                var cy = y + cellY;
                if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                    return SlateResult.OutOfBoard;
                if (At(cx, cy, ignore) != null)
                    return SlateResult.Overlaps;
            }
            return SlateResult.Done;
        }

        public SlatePlacement? At(int x, int y) => At(x, y, null);

        private SlatePlacement? At(int x, int y, SlatePlacement? ignore)
        {
            foreach (var placement in _state.Slates.Board)
            {
                if (placement == ignore)
                    continue;
                foreach (var (cellX, cellY) in placement.Slate.Shape.Cells(placement.Rotation))
                {
                    if (placement.X + cellX == x && placement.Y + cellY == y)
                        return placement;
                }
            }
            return null;
        }

        public SlateResult Remove(SlateItem slate)
        {
            var placement = PlacementOf(slate);
            if (placement == null)
                return SlateResult.NotOnBoard;
            _state.Slates.Board.Remove(placement);
            _state.Slates.Bag.Add(slate);
            Rebuild();
            return SlateResult.Done;
        }

        public SlatePlacement? PlacementOf(SlateItem slate)
        {
            foreach (var placement in _state.Slates.Board)
            {
                if (placement.Slate == slate)
                    return placement;
            }
            return null;
        }

        public SlateResult Temper(SlateItem slate)
        {
            if (!Open)
                return SlateResult.Locked;
            if (!_state.Slates.Bag.Contains(slate) && PlacementOf(slate) == null)
                return SlateResult.NotInBag;
            if (slate.Tempered)
                return SlateResult.AlreadyTempered;
            if (_state.Drops.Shards < _rules.TemperShards)
                return SlateResult.NotEnoughShards;
            _state.Drops.Shards -= _rules.TemperShards;
            slate.Tempered = true;
            if (!_state.Slates.Rng.Chance(_rules.TemperChance))
                return SlateResult.TemperFailed;
            slate.Lines.Add(PickLine(slate.Theme, slate.Lines, _state.Slates.Rng));
            Rebuild();
            return SlateResult.Done;
        }

        public SlateResult Dismantle(SlateItem slate)
        {
            if (!_state.Slates.Bag.Remove(slate))
                return SlateResult.NotInBag;
            _state.Drops.Dust += _rules.Rarity(slate.Rarity).Dust;
            return SlateResult.Done;
        }

        public int SameThemeNeighbours(SlatePlacement placement)
        {
            var count = 0;
            foreach (var other in _state.Slates.Board)
            {
                if (other != placement && other.Slate.Theme == placement.Slate.Theme && Active(other) && Touch(placement, other))
                    count++;
            }
            return count;
        }

        public double AdjacencyBonus(SlatePlacement placement) =>
            System.Math.Min(_rules.AdjacencyMax, _rules.AdjacencyPerNeighbour * SameThemeNeighbours(placement));

        public bool HasPattern(SlateTheme theme)
        {
            var seen = new HashSet<SlatePlacement>();
            foreach (var placement in _state.Slates.Board)
            {
                if (placement.Slate.Theme != theme || !Active(placement) || seen.Contains(placement))
                    continue;
                if (GroupSize(placement, seen) >= _rules.PatternSize)
                    return true;
            }
            return false;
        }

        private int GroupSize(SlatePlacement start, HashSet<SlatePlacement> seen)
        {
            var size = 0;
            var queue = new Queue<SlatePlacement>();
            queue.Enqueue(start);
            seen.Add(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                size++;
                foreach (var other in _state.Slates.Board)
                {
                    if (seen.Contains(other) || other.Slate.Theme != start.Slate.Theme || !Active(other) || !Touch(current, other))
                        continue;
                    seen.Add(other);
                    queue.Enqueue(other);
                }
            }
            return size;
        }

        private static bool Touch(SlatePlacement a, SlatePlacement b)
        {
            foreach (var (ax, ay) in a.Slate.Shape.Cells(a.Rotation))
            {
                foreach (var (bx, by) in b.Slate.Shape.Cells(b.Rotation))
                {
                    var dx = System.Math.Abs(a.X + ax - b.X - bx);
                    var dy = System.Math.Abs(a.Y + ay - b.Y - by);
                    if (dx + dy == 1)
                        return true;
                }
            }
            return false;
        }

        public bool Active(SlatePlacement placement)
        {
            var (width, height) = Size;
            foreach (var (cellX, cellY) in placement.Slate.Shape.Cells(placement.Rotation))
            {
                var x = placement.X + cellX;
                var y = placement.Y + cellY;
                if (x < 0 || y < 0 || x >= width || y >= height)
                    return false;
            }
            return true;
        }

        public void ModifiersOf(SlateItem slate, double adjacency, List<Modifier> into)
        {
            var scale = slate.Shape.Power * _rules.Rarity(slate.Rarity).Power * (1 + adjacency) * Effect;
            foreach (var line in slate.Lines)
                AddLine(line, scale, into);
        }

        private double Effect => System.Math.Max(0, _hero.Value(CombatStats.SlateEffect, 1));

        private void AddLine(SlateLine line, double scale, List<Modifier> into)
        {
            if (line.HeroClass != null && !_state.Is(line.HeroClass.Value))
                return;
            foreach (var modifier in line.Modifiers)
                into.Add(new Modifier(modifier.Stat, modifier.Kind, line.Scaled(modifier.Value, scale), modifier.Tags, ModifierPool.Tree));
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            if (Open)
            {
                foreach (var placement in _state.Slates.Board)
                {
                    if (Active(placement))
                        ModifiersOf(placement.Slate, AdjacencyBonus(placement), modifiers);
                }
                foreach (var theme in _rules.Themes)
                {
                    if (HasPattern(theme))
                        AddLine(theme.Pattern, Effect, modifiers);
                }
            }
            _layer.Set(modifiers);
        }
    }
}
