using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class CompanionVisitView
    {
        public static string Status(CompanionSystem companions, string companionName, long nowUnixMs)
        {
            var state = companions.State;
            if (!companions.Visiting)
                return Texts.Get("visit.none");
            if (companions.VisitBack(nowUnixMs))
                return Texts.Format("visit.ready", companionName, state.VisitHost);
            var minutes = (long)System.Math.Ceiling((state.VisitEndsUnixMs - nowUnixMs) / 60000.0);
            return Texts.Format("visit.away", companionName, state.VisitHost, Texts.Format("unit.hoursMinutes", minutes / 60, minutes % 60));
        }

        public static string Guest(string companionName, string ownerName) => Texts.Format("visit.guest", companionName, ownerName);
    }
}
