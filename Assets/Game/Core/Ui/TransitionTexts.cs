using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class TransitionTexts
    {
        public static string Caption(StripTransition transition)
        {
            if (!transition.Active)
                return string.Empty;
            if (transition.Phase == TransitionPhase.Swap && transition.Loads)
                return Texts.Get(transition.To == StripPlace.Rift ? "strip.portal.riftLoading" : "strip.portal.loading");
            return Texts.Format(transition.Group ? "strip.portal.partyGoes" : "strip.portal.goes", StripSceneText.Place(transition.To));
        }

        public static string FeatureOpened(Feature feature) => Texts.Get("rift.opened." + FeatureKey(feature));

        private static string FeatureKey(Feature feature)
        {
            foreach (var (name, candidate) in UnlockRules.FeatureNames)
            {
                if (candidate == feature)
                    return name;
            }
            return "artifacts";
        }
    }
}
