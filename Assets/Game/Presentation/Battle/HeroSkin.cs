using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class HeroSkin
    {
        private readonly HeroClip[] _idle;
        private readonly HeroClip[] _attack;
        private readonly float _seconds;

        public HeroSkin(HeroClip[] idle, HeroClip[] attack, float seconds)
        {
            _idle = idle;
            _attack = attack;
            _seconds = seconds;
        }

        public HeroClip Idle => _idle[0];
        public HeroClip Attack => _attack[0];
        public int Phases => _idle.Length;

        public HeroClip IdleAt(float time) => _idle[PhaseAt(time)];

        public HeroClip AttackAt(float time) => _attack[PhaseAt(time)];

        public int PhaseAt(float time)
        {
            if (_idle.Length == 1 || _seconds <= 0f)
                return 0;
            return Mathf.Min(_idle.Length - 1, (int)(Mathf.Repeat(time, _seconds) / _seconds * _idle.Length));
        }
    }
}
