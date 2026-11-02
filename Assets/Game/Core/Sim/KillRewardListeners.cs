namespace Game.Core.Sim
{
    public sealed class KillRewardListeners : IKillRewardListener
    {
        private readonly IKillRewardListener[] _listeners;

        public KillRewardListeners(params IKillRewardListener[] listeners)
        {
            _listeners = listeners;
        }

        public void OnKillRewarded()
        {
            for (var i = 0; i < _listeners.Length; i++)
                _listeners[i].OnKillRewarded();
        }
    }
}
