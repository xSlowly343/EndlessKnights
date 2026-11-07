using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class DistillView
    {
        public static string Status(DistillSystem distill)
        {
            if (!distill.Open)
                return Texts.Get("distill.closed");
            if (!distill.Busy)
                return Texts.Get("distill.idle");
            var heat = (int)System.Math.Round(System.Math.Min(1, distill.State.Heat / distill.Rules.HeatMeters) * 100);
            var pressure = (int)System.Math.Round(distill.State.Pressure * 100);
            return Texts.Format(distill.State.Pressure >= distill.Rules.Edge ? "distill.edge" : "distill.going", heat, pressure);
        }

        public static string Colossus(ColossusSeedSystem colossus)
        {
            var state = colossus.State;
            var rules = colossus.Rules;
            if (state.Grown)
                return Texts.Get("colossus.grown");
            if (!state.Planted)
                return Texts.Get(colossus.HasSeed ? "colossus.seed" : "colossus.none");
            return Texts.Format("colossus.growing", Percent(colossus.Share(state.Seconds, rules.GrowSeconds)), Percent(colossus.Share(state.Keys, rules.NeedKeys)),
                    Percent(colossus.Share(state.Clicks, rules.NeedClicks)), Percent(colossus.Share(state.MouseMeters, rules.NeedMouseMeters)))
                + Texts.Format("colossus.quiet", Percent(colossus.Share(state.IdleSeconds, rules.NeedIdleSeconds)));
        }

        private static int Percent(double share) => (int)System.Math.Floor(share * 100);
    }
}
