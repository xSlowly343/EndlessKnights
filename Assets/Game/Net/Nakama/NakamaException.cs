using System;

namespace Game.Net.Nakama
{
    public sealed class NakamaException : Exception
    {
        public NakamaException(int status, string message) : base(message)
        {
            Status = status;
        }

        public int Status { get; }
    }
}
