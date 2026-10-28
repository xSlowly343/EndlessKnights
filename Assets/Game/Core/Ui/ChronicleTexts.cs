using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ChronicleTexts
    {
        public static string Name(TrialDef trial) => Texts.Pick(trial.NameRu, trial.NameEn);

        public static string Goal(TrialDef trial) => Texts.Format("chronicle.goal", trial.GoalStage);

        public static string NoSkills => Texts.Get("chronicle.noSkills");

        public static string Completed(int done, int total) => Texts.Format("chronicle.completed", done, total);

        public static string Result(TrialResult result) => result switch
        {
            TrialResult.UnknownTrial => Texts.Get("chronicle.result.unknownTrial"),
            TrialResult.AlreadyRunning => Texts.Get("chronicle.result.alreadyRunning"),
            TrialResult.NeedsPrestige => Texts.Get("chronicle.result.needsPrestige"),
            TrialResult.RunNotFresh => Texts.Get("chronicle.result.runNotFresh"),
            TrialResult.InsideRift => Texts.Get("chronicle.result.insideRift"),
            _ => string.Empty
        };
    }
}
