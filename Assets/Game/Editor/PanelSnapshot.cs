using System;
using System.IO;
using System.Linq;
using Game.Bootstrap;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Presentation.Strip;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class PanelSnapshot
    {
        private const int StripHeightPx = 140;
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
            var loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Swordsman }, new StatSheet());
            loop.State.Tree.PointsEarned = 5;
            loop.State.Gold = BigNumber.FromDouble(12345);
            loop.AllocateNode("swordsman_ring_1");

            var panels = PanelView.Create();
            panels.Show(true);
            panels.Tree.Bind(loop, data.Tree);
            var width = (int)PanelView.AreaWidthPx;
            var height = (int)PanelView.AreaHeightPx + StripHeightPx;
            StripCamera.Fit(camera, width, height, StripHeightPx, 0, 0);

            var target = panels.Tree.WorldPointOf(data.Tree["swordsman_ring_1"]);
            panels.TryClick(target);
            panels.Release(target);
            Save(camera, panels, width, height, Path.Combine(folder, "panel-tree.png"));

            panels.ShowReport("Пока тебя не было", new[] { "Тебя не было: 2 ч 15 мин", "Золото: +1.50K", "Побеждено врагов: 340", "Этап: 12 → 15" }, true);
            Save(camera, panels, (int)PanelView.ReportAreaWidthPx, (int)PanelView.ReportAreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-report.png"));
            Debug.Log("Panel snapshots saved to " + folder);
        }

        private static void Save(Camera camera, PanelView panels, int width, int height, string path)
        {
            StripCamera.Fit(camera, width, height, StripHeightPx, 0, 0);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            panels.Tree.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
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
