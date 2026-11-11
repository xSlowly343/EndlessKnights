using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class HeroClip
    {
        private readonly Sprite[] _frames;
        private readonly float[] _ends;

        public HeroClip(Sprite[] frames, float[] seconds, bool loops)
        {
            _frames = frames;
            _ends = new float[seconds.Length];
            var total = 0f;
            for (var i = 0; i < seconds.Length; i++)
                _ends[i] = total += seconds[i];
            Duration = total;
            Loops = loops;
        }

        public float Duration { get; }

        public bool Loops { get; }

        public int FrameCount => _frames.Length;

        public int FrameAt(float time)
        {
            var into = Loops ? Mathf.Repeat(time, Duration) : time;
            for (var i = 0; i < _ends.Length; i++)
            {
                if (into < _ends[i])
                    return i;
            }
            return _ends.Length - 1;
        }

        public Sprite SpriteAt(float time) => _frames[FrameAt(time)];

        public Sprite Frame(int index) => _frames[index];
    }
}
