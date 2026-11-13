using Game.Core.Sim;

namespace Game.Core.Ui
{
    public enum PortalPart
    {
        None,
        Open,
        Idle,
        Close
    }

    public static class TransitionLook
    {
        public const double SmallestHero = 0.35;

        public static PortalPart Portal(StripTransition transition) => transition.Phase switch
        {
            TransitionPhase.Open => PortalPart.Open,
            TransitionPhase.Enter => PortalPart.Idle,
            TransitionPhase.Arrive => PortalPart.Idle,
            TransitionPhase.Exit => PortalPart.Close,
            _ => PortalPart.None
        };

        public static StripPlace PortalPlace(StripTransition transition) => transition.Phase <= TransitionPhase.Enter ? transition.To : transition.From;

        public static double HeroAlpha(StripTransition transition) => transition.Phase switch
        {
            TransitionPhase.Enter => 1 - transition.PhaseProgress,
            TransitionPhase.Swap => 0,
            TransitionPhase.Arrive => transition.PhaseProgress,
            _ => 1
        };

        public static double HeroScale(StripTransition transition) => transition.Phase switch
        {
            TransitionPhase.Enter => Lerp(1, SmallestHero, transition.PhaseProgress),
            TransitionPhase.Swap => SmallestHero,
            TransitionPhase.Arrive => Lerp(SmallestHero, 1, transition.PhaseProgress),
            _ => 1
        };

        public static double VeilAlpha(StripTransition transition) => transition.Phase switch
        {
            TransitionPhase.Enter => transition.PhaseProgress * transition.PhaseProgress,
            TransitionPhase.Swap => 1,
            TransitionPhase.Arrive => 1 - transition.PhaseProgress,
            _ => 0
        };

        public static bool ShowsLoading(StripTransition transition) => transition.Phase == TransitionPhase.Swap && transition.Loads;

        private static double Lerp(double from, double to, double t) => from + (to - from) * t;
    }
}
