using System;
using Game.Core.Input;

namespace Game.Presentation.Strip
{
    public struct InputTotals : IEquatable<InputTotals>
    {
        public int Keys;
        public int Enter;
        public int Backspace;
        public int Space;
        public int LeftClicks;
        public int RightClicks;
        public int MiddleClicks;
        public int WheelTicks;
        public int MousePx;
        public int Flicks;
        public int Circles;
        public float RhythmCv;

        private double _mouseDistance;
        private float _circleTurns;

        public void Add(InputSnapshot snapshot)
        {
            Keys += snapshot.KeyDowns;
            Enter += snapshot.EnterDowns;
            Backspace += snapshot.BackspaceDowns;
            Space += snapshot.SpaceDowns;
            LeftClicks += snapshot.LeftClicks;
            RightClicks += snapshot.RightClicks;
            MiddleClicks += snapshot.MiddleClicks;
            WheelTicks += snapshot.WheelTicks;
            Flicks += snapshot.Flicks;
            _mouseDistance += snapshot.MouseDistancePx;
            _circleTurns += snapshot.CircleTurns;
            MousePx = (int)_mouseDistance;
            Circles = (int)_circleTurns;
            RhythmCv = snapshot.RhythmCv;
        }

        public bool Equals(InputTotals other) =>
            Keys == other.Keys && Enter == other.Enter && Backspace == other.Backspace && Space == other.Space
            && LeftClicks == other.LeftClicks && RightClicks == other.RightClicks && MiddleClicks == other.MiddleClicks
            && WheelTicks == other.WheelTicks && MousePx == other.MousePx && Flicks == other.Flicks && Circles == other.Circles
            && System.Math.Abs(RhythmCv - other.RhythmCv) < 0.005f;

        public override bool Equals(object? obj) => obj is InputTotals other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Keys, LeftClicks, MousePx, Flicks, Circles);
    }
}
