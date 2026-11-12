using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Platform.Windows.Clipboard;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class RecapDriver : MonoBehaviour
    {
        private const float CheckSeconds = 1f;
        private const float LaunchWindowSeconds = 60f;

        private GameLoop _loop = null!;
        private PanelModeDriver _panelMode = null!;
        private PanelCanvas _panels = null!;
        private NotificationDriver? _notifications;
        private UiTheme _theme = null!;
        private RecapCard? _shown;
        private float _nextCheck = CheckSeconds;
        private float _startedAt;

        public static RecapDriver Attach(GameObject host, GameLoop loop, PanelModeDriver panelMode, PanelCanvas panels, NotificationDriver? notifications)
        {
            var driver = host.AddComponent<RecapDriver>();
            driver._loop = loop;
            driver._panelMode = panelMode;
            driver._panels = panels;
            driver._notifications = notifications;
            driver._theme = UiTheme.Load();
            driver._startedAt = Time.unscaledTime;
            panels.Stats.RecapRequested += driver.OpenByHand;
            panels.SharePressed += driver.Share;
            return driver;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + CheckSeconds;
            var due = _loop.Recap.Due;
            if (due == RecapKind.None)
                return;
            var atLaunch = Time.unscaledTime - _startedAt < LaunchWindowSeconds;
            if (atLaunch && _panels.IsOpen)
                return;
            if (!atLaunch || !Open(due))
                _notifications?.Notify(RecapView.Ready(due));
            _loop.Recap.MarkShown(due);
        }

        private void OpenByHand(bool weekly)
        {
            if (!Open(weekly ? RecapKind.Week : RecapKind.Day))
                _panels.ShowNotice(RecapView.Nothing);
        }

        private bool Open(RecapKind kind)
        {
            var card = RecapView.Card(_loop.Recap, kind, RecapView.Hero(_loop), NumberDisplay.Settings.ThousandsSeparator);
            if (card == null)
                return false;
            _shown = card;
            _panelMode.OpenReport(RecapView.Title(card), RecapView.Lines(card), Texts.Get("report.gotIt"), RecapView.Share);
            return true;
        }

        private void Share()
        {
            if (_shown == null)
                return;
            var copied = false;
            Texture2D? picture = null;
            try
            {
                picture = RecapCardRenderer.Render(_shown, _theme);
                copied = ClipboardImage.Copy(picture.GetRawTextureData(), picture.width, picture.height);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("Recap: the picture was not copied, " + error.Message);
            }
            finally
            {
                if (picture != null)
                    Destroy(picture);
            }
            _notifications?.Notify(copied ? RecapView.Shared : RecapView.ShareFailed);
        }
    }
}
