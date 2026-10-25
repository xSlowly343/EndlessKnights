using Game.Core.Content;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public enum StripHintKind
    {
        None,
        Consent,
        Tutorial
    }

    public readonly struct StripHint
    {
        public static readonly StripHint None = new StripHint(StripHintKind.None, string.Empty);

        public readonly StripHintKind Kind;
        public readonly string Text;

        public StripHint(StripHintKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public bool IsShown => Kind != StripHintKind.None;
        public bool CanClose => Kind == StripHintKind.Tutorial;

        public static string ConsentText => Texts.Get("hint.consent");

        public static StripHint For(bool consentGiven, TutorialStep? step)
        {
            if (!consentGiven)
                return new StripHint(StripHintKind.Consent, ConsentText);
            return step == null ? None : new StripHint(StripHintKind.Tutorial, TextOf(step));
        }

        public static string TextOf(TutorialStep step)
        {
            var key = "tutorial." + step.Id;
            return Texts.Has(key) ? Texts.Get(key) : step.Text;
        }
    }
}
