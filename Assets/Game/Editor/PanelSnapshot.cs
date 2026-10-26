using System;
using System.IO;
using System.Linq;
using Game.Bootstrap;
using Game.Core.Content.Items;
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
            loop.State.HeroLevel = 42;
            loop.State.Stage = 37;
            loop.State.RecordStage = 120;
            var starter = loop.Weapons.Give(data.Weapons.StarterFor(HeroClass.Viking));
            loop.Weapons.Equip(WeaponHand.Right, starter);
            loop.Book.GrantStartingGems();
            var rng = new Rng(7);
            var rarity = ItemRarity.Fine;
            foreach (var weapon in data.Weapons.Bases.Where(weapon => weapon.Type != starter.Type).Take(9))
                loop.Weapons.Give(weapon, rarity++, rng);
            loop.State.OwnedGems.AddRange(data.Gems.All.Where(gem => gem.Skill?.Weapon == null).Take(12));

            var panels = PanelCanvas.Create();
            panels.Bind(loop, data.Tree);
            var marks = new PanelMarks(data.Panel);
            marks.Refresh(loop.State, data.Ascendancy);
            panels.UseMarks(marks);
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Tree);
            var frame = ShowPanel(panels, navigator);

            var target = panels.Tree.WorldPointOf(data.Tree["ronin_ring_1"]);
            panels.Point(target, false);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-tree.png"));
            SaveTreeZoom(camera, panels, frame, 2, Path.Combine(folder, "panel-tree-close.png"));
            SaveTreeZoom(camera, panels, frame, -2, Path.Combine(folder, "panel-tree-mid.png"));
            SaveTreeZoom(camera, panels, frame, -20, Path.Combine(folder, "panel-tree-far.png"));
            panels.Tree.FocusOwnPath();

            navigator.ToggleWide();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-tree-wide.png"));
            navigator.ToggleWide();

            foreach (var activity in data.Ascendancy.Activities.Take(4))
                loop.CompleteActivity(activity.Id);
            navigator.SelectSubTab(1);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-subclass-choose.png"));
            var path = data.Ascendancy.SubclassesOf(loop.State.Tree.StartClass)[0];
            loop.ChooseSubclass(path.Id);
            foreach (var node in path.Start.Neighbours.Take(2))
                loop.AllocateSubclassNode(node.Id);
            frame = ShowPanel(panels, navigator);
            panels.Point(panels.Subclass.WorldPointOf(path.Start.Neighbours[0]), false);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-subclass.png"));
            panels.PointAway();
            navigator.SelectSubTab(0);

            navigator.SelectTab(PanelSection.Hero);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-hero.png"));

            navigator.SelectTab(PanelSection.Items);
            ShowPanel(panels, navigator);
            panels.Items.Select(loop.Weapons.Bag[3]);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-items.png"));
            var rarities = RaritySample(panels.CanvasRect);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-rarities.png"));
            UnityEngine.Object.DestroyImmediate(rarities.gameObject);
            navigator.SelectTab(PanelSection.Party);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-party.png"));

            navigator.SelectTab(PanelSection.Book);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-book.png"));
            var grabbed = panels.Book.WorldPointOfBagCell(0);
            panels.TryClick(grabbed);
            panels.Drag(Vector2.Lerp(grabbed, panels.Book.WorldPointOfRow(2), 0.7f));
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-book-drag.png"));
            panels.CancelPress();
            navigator.ToggleHeight();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-book-half.png"));
            navigator.ToggleHeight();
            navigator.SelectSubTab(1);
            frame = ShowPanel(panels, navigator);
            var place = panels.Master.WorldPointOfPlace(3);
            panels.TryClick(place);
            panels.Release(place);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-master.png"));
            navigator.SelectSubTab(0);
            navigator.SelectTab(PanelSection.Hero);
            frame = ShowPanel(panels, navigator);

            navigator.SelectSubTab(2);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-hero-input.png"));
            navigator.SelectSubTab(0);

            navigator.ToggleHeight();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-hero-half.png"));
            navigator.ToggleHeight();

            navigator.PressMore();
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-more.png"));

            panels.Settings.Use(new Game.Core.Settings.GameSettings());
            navigator.SelectTab(PanelSection.Settings);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-settings.png"));
            navigator.SelectSubTab(1);
            frame = ShowPanel(panels, navigator);
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-settings-sound.png"));

            navigator.SelectTab(PanelSection.Hero);
            frame = ShowPanel(panels, navigator);
            panels.ShowNotice(PanelTexts.Locked(PanelSection.Party, data.Panel.StageFor(PanelSection.Party)));
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-notice.png"));

            panels.ShowReport("Пока тебя не было", new[] { "Тебя не было: 2 ч 15 мин", "Золото: +1.50K", "Побеждено врагов: 340", "Этап: 12 → 15" });
            panels.Place(0f, true);
            var report = PanelCanvas.ReportFrame;
            Save(camera, panels, report.WidthPx, report.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-report.png"));

            panels.ShowConsent();
            panels.Place(0f, true);
            panels.FinishTween();
            Canvas.ForceUpdateCanvases();
            var consent = PanelCanvas.ConsentFrame;
            Save(camera, panels, consent.WidthPx, consent.AreaHeightPx + StripHeightPx, Path.Combine(folder, "panel-consent.png"));

            panels.Hide();
            var battle = BattleView.Create();
            battle.ShowEnemy(true, false);
            battle.ShowState(37, 2, 4, 0.5, 0.62, 18, 0.6, true, 40, 100, BigNumber.FromDouble(12400));
            battle.Hit(BigNumber.FromDouble(3900), true);
            battle.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            var skillBar = SkillBarView.Create(battle.transform);
            skillBar.Show(loop.State.Skills, loop.State.Resource);
            var hint = StripHintView.Create(battle.transform);
            hint.Show(StripHint.For(true, data.Tutorial.Steps[0]));
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
            Canvas.ForceUpdateCanvases();
            return navigator.Frame;
        }

        private static RectTransform RaritySample(RectTransform canvas)
        {
            const float cellWidthPx = 58f;
            const float cellHeightPx = 54f;
            const float gapPx = 6f;
            var theme = UiTheme.Load();
            var back = UiElements.Fill("RaritySample", canvas, theme.Card, 12f);
            var sample = UiElements.Anchor(back.rectTransform, Vector2.zero, new Vector2(16f, 220f), new Vector2(7f * cellWidthPx + 8f * gapPx, 2f * cellHeightPx + 3f * gapPx + 8f));
            var rarities = (ItemRarity[])Enum.GetValues(typeof(ItemRarity));
            for (var i = 0; i < rarities.Length; i++)
            {
                var position = new Vector2(gapPx + i % 7 * (cellWidthPx + gapPx), -(gapPx + 4f + i / 7 * (cellHeightPx + gapPx)));
                var host = UiElements.Anchor(UiElements.Rect(rarities[i].ToString(), sample), new Vector2(0f, 1f), position, new Vector2(cellWidthPx, cellHeightPx));
                RarityFrame.Create(host, 8f, 2f, new Color32(0x22, 0x30, 0x4A, 0xFF)).Show(rarities[i], true);
                var letter = UiElements.Label("Letter", host, theme.BoldFont, 16f, RarityFrame.IconOf(rarities[i]), TMPro.TextAlignmentOptions.Center);
                UiElements.Stretch(letter.rectTransform);
                letter.text = (i + 1).ToString();
            }
            return sample;
        }

        private static void SaveTreeZoom(Camera camera, PanelCanvas panels, PanelFrame frame, int steps, string path)
        {
            for (var i = 0; i < System.Math.Abs(steps); i++)
            {
                if (steps > 0)
                    panels.Tree.ZoomIn();
                else
                    panels.Tree.ZoomOut();
            }
            Save(camera, panels, frame.WidthPx, frame.AreaHeightPx + StripHeightPx, path);
        }

        private static void Save(Camera camera, PanelCanvas panels, int width, int height, string path)
        {
            StripCamera.Fit(camera, width, height, StripHeightPx, 0, 0);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
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
