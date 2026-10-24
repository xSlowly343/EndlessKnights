using System;
using System.IO;
using System.Linq;
using Game.Bootstrap;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Presentation.Battle;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class PanelSnapshot
    {
        private const int StripHeightPx = GameStartup.StripHeight;
        private static readonly Color DesktopColor = new Color(0.18f, 0.24f, 0.32f, 1f);

        public static void Capture()
        {
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Logs"));
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DesktopColor;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var store = GameDataFiles.TryLoadAll() ?? throw new InvalidOperationException("Game data did not load.");
            var data = store.Current;
            var loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Viking }, new StatSheet());
            loop.State.Tree.PointsEarned = 5;
            loop.State.Gold = BigNumber.FromDouble(12345);
            loop.AllocateNode("ronin_ring_1");

            var panels = PanelCanvas.Create();
            panels.Bind(loop, data.Tree);
            var marks = new PanelMarks(data.Panel);
            marks.Refresh(loop.State, data.Ascendancy);
            panels.UseMarks(marks);
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Tree);
            var frame = ShowPanel(panels, navigator);

            var target = panels.Tree.WorldPointOf(data.Tree["ronin_ring_1"]);
            panels.TryClick(target);
            panels.Release(target);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-tree.png"));

            navigator.ToggleWide();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-tree-wide.png"));

            navigator.SelectTab(PanelSection.Hero);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-hero.png"));

            navigator.PressMore();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-more.png"));

            navigator.SelectTab(PanelSection.Hero);
            frame = ShowPanel(panels, navigator);
            panels.ShowNotice(PanelTexts.Locked(PanelSection.Party, data.Panel.StageFor(PanelSection.Party)));
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-notice.png"));

            panels.ShowReport("Пока тебя не было", new[] { "Тебя не было: 2 ч 15 мин", "Золото: +1.50K", "Побеждено врагов: 340", "Этап: 12 → 15" });
            panels.Place(0f, true);
            var report = PanelCanvas.ReportFrame;
            Save(camera, panels, report.WidthPx, report.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-report.png"));

            panels.Hide();
            var battle = BattleView.Create();
            battle.ShowEnemy(true, false);
            battle.ShowState(37, 4, 10, 0.62, 18, 0.6, true, 0.7, 0.4, BigNumber.FromDouble(12400));
            battle.Hit(BigNumber.FromDouble(3900), true);
            battle.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            Save(camera, panels, GameStartup.StripWidth, StripHeightPx, Path.Combine(folder, "strip.png"));

            var queue = new NotificationQueue();
            queue.Push(PanelTexts.Unlocked(PanelSection.Items), 8);
            queue.Push(PanelTexts.LevelUp(7), 9);
            queue.Push(PanelTexts.Locked(PanelSection.Party, data.Panel.StageFor(PanelSection.Party)), 9.6);
            var pills = NotificationPills.Create();
            pills.Place(true);
            pills.Show(queue.Items, 10);
            var pillsArea = Mathf.RoundToInt(NotificationPills.AreaHeightPx) + PanelGeometry.GapToStripPx;
            Save(camera, panels, GameStartup.StripWidth, StripHeightPx + pillsArea, Path.Combine(folder, "strip-notifications.png"));
            Debug.Log("Panel snapshots saved to " + folder);
        }

        private static PanelFrame ShowPanel(PanelCanvas panels, PanelNavigator navigator)
        {
            panels.Show(navigator);
            panels.Place(0f, true);
            return navigator.Frame;
        }

        private static void Save(Camera camera, PanelCanvas panels, int width, int height, string path)
        {
            StripCamera.Fit(camera, width, height, StripHeightPx, 0, 0);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            panels.Tree.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            Canvas.ForceUpdateCanvases();
            foreach (var view in new[] { camera }.Concat(panels.GetComponentsInChildren<Camera>()))
            {
                view.targetTexture = target;
                view.Render();
                view.targetTexture = null;
            }
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
    }
}
