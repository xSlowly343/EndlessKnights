using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class GhostSystem
    {
        public const int Slot = PartyRules.MaxSlots - 1;

        private readonly GameState _state;
        private readonly CombatSystem _combat;
        private readonly StageSystem _stage;
        private readonly SynergySystem _synergy;
        private PartyGhostRules _rules;
        private GhostSnapshot _ghost;
        private bool _present;
        private bool _marked;
        private long _activeTicks;
        private long _seenBossKills;

        public GhostSystem(PartyGhostRules rules, GameState state, CombatSystem combat, StageSystem stage, SynergySystem synergy)
        {
            _rules = rules;
            _state = state;
            _combat = combat;
            _stage = stage;
            _synergy = synergy;
        }

        public bool Present => _present;
        public GhostSnapshot Ghost => _ghost;
        public bool Fighting => _present && !_stage.Linked;
        public BigNumber Damage { get; private set; }
        public long BossesHelped { get; private set; }

        public double OwnShareRate
        {
            get
            {
                var hp = _stage.Table.EnemyHp(_state.Stage);
                return hp.Sign > 0 ? System.Math.Max(0, (_state.RecentDps / hp).ToDouble()) : 0;
            }
        }

        public double ShareRate => Fighting ? _rules.Power * _ghost.Activity * _ghost.ShareRate : 0;

        public void UseRules(PartyGhostRules rules) => _rules = rules;

        public void Use(in GhostSnapshot ghost)
        {
            _ghost = ghost;
            _present = ghost.ShareRate > 0 && ghost.Activity > 0;
        }

        public void Clear()
        {
            _present = false;
            _ghost = default;
        }

        public long TakeActiveSeconds()
        {
            var seconds = _activeTicks / GameLoop.TicksPerSecond;
            _activeTicks -= seconds * GameLoop.TicksPerSecond;
            return seconds;
        }

        public void NoteInput(bool hasInput)
        {
            if (hasInput)
                _activeTicks++;
        }

        public void Tick()
        {
            ShowMarks();
            var bossKills = _state.BossKills;
            if (Fighting && bossKills > _seenBossKills)
                BossesHelped += bossKills - _seenBossKills;
            _seenBossKills = bossKills;
            if (!Fighting || !_state.EnemyAlive)
                return;
            var share = ShareRate * GameLoop.TickSeconds;
            if (!(share > 0))
                return;
            var damage = _stage.Table.EnemyHp(_state.Stage) * share;
            _combat.DirectHit(damage);
            Damage += damage;
        }

        private void ShowMarks()
        {
            var wanted = Fighting;
            if (wanted == _marked && !wanted)
                return;
            _marked = wanted;
            _synergy.SetAlly(Slot, wanted, _ghost.HeroClass, wanted ? _ghost.Marks : 0);
        }
    }
}
