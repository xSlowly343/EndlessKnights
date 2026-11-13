using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class TransitionSound
    {
        public const string RiftLoading = "rift_loading";

        private static readonly StripPlace[] Places =
        {
            StripPlace.Home, StripPlace.Rift, StripPlace.Dungeon, StripPlace.EventFight, StripPlace.Hideout, StripPlace.ClanCamp, StripPlace.Town
        };

        private static readonly TransitionPhase[] Phases = { TransitionPhase.Open, TransitionPhase.Enter, TransitionPhase.Arrive, TransitionPhase.Exit };

        private static readonly string[,] Keys = Build();

        public static string Cue(StripTransition transition)
        {
            if (!transition.Active)
                return string.Empty;
            if (transition.Phase == TransitionPhase.Swap)
                return transition.Loads && transition.To == StripPlace.Rift ? RiftLoading : string.Empty;
            var place = transition.Phase <= TransitionPhase.Enter ? transition.To : transition.From;
            if (transition.Phase == TransitionPhase.Arrive)
                place = transition.To;
            return Key(place, transition.Phase);
        }

        public static string Key(StripPlace place, TransitionPhase phase)
        {
            var p = System.Array.IndexOf(Places, place);
            var f = System.Array.IndexOf(Phases, phase);
            return p < 0 || f < 0 ? string.Empty : Keys[p, f];
        }

        public static float Volume(Settings.GameSettings settings) => (float)(settings.MasterVolume * settings.InterfaceVolume);

        private static string[,] Build()
        {
            var keys = new string[Places.Length, Phases.Length];
            for (var p = 0; p < Places.Length; p++)
            {
                for (var f = 0; f < Phases.Length; f++)
                    keys[p, f] = "portal_" + StripSceneRules.KeyOf(Places[p]) + "_" + Phases[f].ToString().ToLowerInvariant();
            }
            return keys;
        }
    }
}
