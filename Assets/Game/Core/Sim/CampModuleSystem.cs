using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum CampModuleResult
    {
        Done,
        NoSuchModule,
        ForgeClosed,
        NoBlueprint,
        AlreadyMade,
        NotEnough,
        NotMade,
        NotOpen,
        WrongBuilding,
        NoSuchSocket
    }

    public sealed class CampModuleSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly CampLevelSystem _levels;
        private readonly EventQueue _events;
        private readonly string _forge;
        private CampModuleRules _rules;

        public CampModuleSystem(CampModuleRules rules, GameState state, CampSystem camp, CampLevelSystem levels, EventQueue events, string forge)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _levels = levels;
            _events = events;
            _forge = forge;
            _levels.Modules = this;
            _levels.Apply();
        }

        public CampModuleRules Rules => _rules;
        public string LastBlueprint { get; private set; } = string.Empty;
        private CampState Camp => _state.Camp;

        public void UseRules(CampModuleRules rules)
        {
            _rules = rules;
            _levels.Apply();
        }

        public int Sockets(string building) => _rules.SocketsAt(_levels.Level(building));

        public bool Made(string module) => Camp.Modules.Contains(module);

        public bool HasBlueprint(string module) => Camp.Blueprints.Contains(module);

        public string InSocket(string building, int socket)
        {
            foreach (var pair in Camp.Installed)
            {
                if (pair.Value == socket && _rules.Of(pair.Key)?.Building == building)
                    return pair.Key;
            }
            return string.Empty;
        }

        public int SocketOf(string module) => Camp.Installed.TryGetValue(module, out var socket) ? socket : -1;

        public CampModuleResult CheckCraft(string module)
        {
            var def = _rules.Of(module);
            if (def == null)
                return CampModuleResult.NoSuchModule;
            if (Made(module))
                return CampModuleResult.AlreadyMade;
            if (!_camp.Open(_forge))
                return CampModuleResult.ForgeClosed;
            if (def.Blueprint && !HasBlueprint(module))
                return CampModuleResult.NoBlueprint;
            foreach (var material in def.Materials)
            {
                if (_camp.Count(material.Item) < material.Count)
                    return CampModuleResult.NotEnough;
            }
            return CampModuleResult.Done;
        }

        public CampModuleResult Craft(string module)
        {
            var result = CheckCraft(module);
            if (result != CampModuleResult.Done)
                return result;
            foreach (var material in _rules.Of(module)!.Materials)
                _camp.Take(material.Item, material.Count);
            Camp.Modules.Add(module);
            return CampModuleResult.Done;
        }

        public CampModuleResult CheckInstall(string module, string building, int socket)
        {
            var def = _rules.Of(module);
            if (def == null)
                return CampModuleResult.NoSuchModule;
            if (!Made(module))
                return CampModuleResult.NotMade;
            if (def.Building != building)
                return CampModuleResult.WrongBuilding;
            if (_levels.Level(building) == 0)
                return CampModuleResult.NotOpen;
            if (socket < 0 || socket >= Sockets(building))
                return CampModuleResult.NoSuchSocket;
            return CampModuleResult.Done;
        }

        public CampModuleResult Install(string module, string building, int socket)
        {
            var result = CheckInstall(module, building, socket);
            if (result != CampModuleResult.Done)
                return result;
            var current = InSocket(building, socket);
            if (current.Length > 0)
                Camp.Installed.Remove(current);
            Camp.Installed[module] = socket;
            _levels.Apply();
            return CampModuleResult.Done;
        }

        public bool Remove(string module)
        {
            if (!Camp.Installed.Remove(module))
                return false;
            _levels.Apply();
            return true;
        }

        public void NoteBosses(int firstEvent)
        {
            for (var i = firstEvent; i < _events.Count; i++)
            {
                if (_events[i].Kind != GameEventKind.BossDefeated)
                    continue;
                var roll = Mix((ulong)_state.BossKills * 2 + (ulong)i);
                if ((roll >> 11) * (1.0 / (1UL << 53)) < _rules.BlueprintChance)
                    DropBlueprint(Mix(roll));
            }
        }

        private static ulong Mix(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        public void AddPerks(double[] perks)
        {
            foreach (var pair in Camp.Installed)
            {
                var def = _rules.Of(pair.Key);
                if (def == null || pair.Value >= Sockets(def.Building))
                    continue;
                perks[(int)def.Perk.Perk] += def.Perk.Value;
            }
        }

        private void DropBlueprint(ulong roll)
        {
            var missing = 0;
            foreach (var def in _rules.Modules)
            {
                if (Missing(def))
                    missing++;
            }
            if (missing == 0)
                return;
            var pick = (int)(roll % (ulong)missing);
            foreach (var def in _rules.Modules)
            {
                if (!Missing(def))
                    continue;
                if (pick-- > 0)
                    continue;
                Camp.Blueprints.Add(def.Id);
                LastBlueprint = def.Id;
                return;
            }
        }

        private bool Missing(CampModuleDef def) => def.Blueprint && !Camp.Blueprints.Contains(def.Id);
    }
}
