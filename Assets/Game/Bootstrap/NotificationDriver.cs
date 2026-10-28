using Game.Core.Sim;
using Game.Core.Ui;
using Game.Platform.Windows.Window;
using Game.Presentation.Ui;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class NotificationDriver : MonoBehaviour
    {
        private const float MarksRefreshSeconds = 0.25f;

        private readonly NotificationQueue _queue = new NotificationQueue();
        private TaskbarDockDriver _dock = null!;
        private PanelModeDriver _panelMode = null!;
        private PanelCanvas _panels = null!;
        private NotificationPills _pills = null!;
        private GameLoop _loop = null!;
        private PanelMarks _marks = null!;
        private bool _expanded;
        private float _sinceMarks;

        public static NotificationDriver Attach(GameObject host, TaskbarDockDriver dock, PanelModeDriver panelMode, PanelCanvas panels, BattleDriver battle)
        {
            var driver = host.AddComponent<NotificationDriver>();
            driver._dock = dock;
            driver._panelMode = panelMode;
            driver._panels = panels;
            driver._loop = battle.Loop;
            driver._pills = NotificationPills.Create();
            driver._marks = new PanelMarks(battle.Loop.Data.Panel);
            driver._marks.Refresh(battle.Loop.State, battle.Loop.Data.Ascendancy);
            driver._marks.Unlocked += section => driver.Notify(PanelTexts.Unlocked(section));
            panels.UseMarks(driver._marks);
            panelMode.Navigator.IsUnlocked = driver._marks.IsOpen;
            panelMode.Navigator.Refused += section => driver.Notify(PanelTexts.Locked(section, driver._marks.UnlockStage(section)));
            battle.LeveledUp += driver.OnLevelUp;
            dock.Placed += driver.OnPlaced;
            return driver;
        }

        public PanelMarks Marks => _marks;

        public void Notify(string text)
        {
            if (_panelMode.IsOpen)
            {
                _panels.ShowNotice(text);
                return;
            }
            _queue.Push(text, Now);
            if (!_expanded)
            {
                _expanded = true;
                _pills.Place(_dock.Layout.PanelsAboveStrip);
            }
            _pills.Show(_queue.Items, Now);
        }

        private void OnLevelUp(long level)
        {
            if (!_panelMode.IsOpen)
                Notify(PanelTexts.LevelUp(level));
        }

        private void Update()
        {
            RefreshMarks();
            if (_panelMode.IsOpen)
            {
                DropPills();
                return;
            }
            if (_queue.IsEmpty)
                return;
            if (_queue.Expire(Now) && _queue.IsEmpty)
            {
                DropPills();
                return;
            }
            _pills.Show(_queue.Items, Now);
        }

        private void RefreshMarks()
        {
            _sinceMarks += Time.unscaledDeltaTime;
            if (_sinceMarks < MarksRefreshSeconds)
                return;
            _sinceMarks = 0f;
            if (_marks.Refresh(_loop.State, _loop.Data.Ascendancy))
                _panels.RefreshMarks();
        }

        private void DropPills()
        {
            _queue.Clear();
            _pills.Hide();
            _expanded = false;
        }

        private void OnPlaced(PanelLayout layout, int stripHeightPx)
        {
            if (_expanded && !_panelMode.IsOpen)
                _pills.Place(layout.PanelsAboveStrip);
        }

        private static double Now => Time.unscaledTimeAsDouble;
    }
}
