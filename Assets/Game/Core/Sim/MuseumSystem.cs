using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public enum MuseumResult
    {
        Done,
        Unknown,
        NotCollected,
        ShelfFull,
        NoTitle
    }

    public sealed class MuseumSystem
    {
        private const int CheckEveryTicks = 10;

        private readonly GameState _state;
        private readonly AlchemySystem _alchemy;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _buffer = new List<Modifier>(16);
        private MuseumRules _rules;

        public MuseumSystem(MuseumRules rules, GameState state, StatSheet hero, AlchemySystem alchemy, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _alchemy = alchemy;
            _events = events;
            _layer = new ModifierLayer(hero);
            ApplyBonus();
        }

        public MuseumRules Rules => _rules;
        public MuseumState State => _state.Museum;

        public void UseRules(MuseumRules rules)
        {
            _rules = rules;
            ApplyBonus();
        }

        public int Collected(CollectionDef collection)
        {
            var count = 0;
            for (var i = 0; i < collection.Items.Count; i++)
            {
                if (Has(collection.Kind, collection.Items[i]))
                    count++;
            }
            return count;
        }

        public bool Complete(string id) => State.Done.Contains(id);

        public void Tick()
        {
            if (_state.Tick % CheckEveryTicks == 0)
                Check();
        }

        public void Check()
        {
            var changed = false;
            var collections = _rules.Collections;
            for (var i = 0; i < collections.Count; i++)
            {
                var collection = collections[i];
                if (State.Done.Contains(collection.Id) || Collected(collection) < collection.Items.Count)
                    continue;
                State.Done.Add(collection.Id);
                changed = true;
                _events.Add(GameEvent.ForStage(GameEventKind.CollectionCompleted, _state.Tick, i));
            }
            if (changed)
                ApplyBonus();
        }

        public MuseumResult Place(string collectionId)
        {
            var collection = _rules.Collection(collectionId);
            if (collection == null)
                return MuseumResult.Unknown;
            if (!Complete(collectionId))
                return MuseumResult.NotCollected;
            if (State.Shelf.Contains(collectionId))
                return MuseumResult.Done;
            if (State.Shelf.Count >= _rules.Shelf)
                return MuseumResult.ShelfFull;
            State.Shelf.Add(collectionId);
            return MuseumResult.Done;
        }

        public void Remove(string collectionId) => State.Shelf.Remove(collectionId);

        public MuseumResult SetTitle(string collectionId)
        {
            if (collectionId.Length == 0)
            {
                State.Title = string.Empty;
                return MuseumResult.Done;
            }
            var collection = _rules.Collection(collectionId);
            if (collection == null)
                return MuseumResult.Unknown;
            if (!collection.HasTitle)
                return MuseumResult.NoTitle;
            if (!Complete(collectionId))
                return MuseumResult.NotCollected;
            State.Title = collectionId;
            return MuseumResult.Done;
        }

        private bool Has(CollectionKind kind, string id) => kind switch
        {
            CollectionKind.Fish => _state.Fishing.Album.ContainsKey(id),
            CollectionKind.Plants => _state.Garden.Album.Contains(id),
            CollectionKind.Dishes => _state.Cooking.Book.Contains(id),
            CollectionKind.Recipes => _alchemy.Knows(id),
            _ => _state.Expeditions.Finds.Contains(id)
        };

        private void ApplyBonus()
        {
            _buffer.Clear();
            var collections = _rules.Collections;
            for (var i = 0; i < collections.Count; i++)
            {
                if (State.Done.Contains(collections[i].Id))
                    _buffer.AddRange(collections[i].Bonus);
            }
            _layer.Set(_buffer);
        }
    }
}
