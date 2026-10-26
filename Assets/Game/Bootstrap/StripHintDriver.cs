using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Battle;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class StripHintDriver : MonoBehaviour
    {
        private GameLoop _loop = null!;
        private StripHintView _view = null!;
        private SettingsDriver _settings = null!;
        private PanelModeDriver _panelMode = null!;
        private TutorialStep? _shownStep;
        private bool _shownConsent = true;
        private string _shownLanguage = string.Empty;

        public static StripHintDriver Attach(GameObject host, GameLoop loop, StripHintView view, SettingsDriver settings, PanelModeDriver panelMode)
        {
            var driver = host.AddComponent<StripHintDriver>();
            driver._loop = loop;
            driver._view = view;
            driver._settings = settings;
            driver._panelMode = panelMode;
            view.CloseClicked += driver.Dismiss;
            view.HintClicked += driver.OnHintClicked;
            driver.Refresh();
            return driver;
        }

        private void LateUpdate()
        {
            var step = _loop.Tutorial.Current;
            var consent = _settings.Settings.InputConsentGiven;
            if (step != _shownStep || consent != _shownConsent || Texts.Language != _shownLanguage)
                Refresh();
        }

        private void Refresh()
        {
            _shownStep = _loop.Tutorial.Current;
            _shownConsent = _settings.Settings.InputConsentGiven;
            _shownLanguage = Texts.Language;
            _view.Show(StripHint.For(_shownConsent, _shownStep));
        }

        private void Dismiss()
        {
            _loop.Tutorial.Dismiss();
            Refresh();
        }

        private void OnHintClicked()
        {
            if (!_settings.Settings.InputConsentGiven)
                _panelMode.OpenConsent();
        }
    }
}
