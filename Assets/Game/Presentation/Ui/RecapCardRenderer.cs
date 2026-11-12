using Game.Core.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public static class RecapCardRenderer
    {
        private const float MarginPx = 56f;
        private const float GapPx = 24f;
        private const float TitleSizePx = 68f;
        private const float PeriodSizePx = 34f;
        private const float LabelSizePx = 26f;
        private const float ValueSizePx = 46f;
        private const float FooterSizePx = 26f;
        private const float HeaderPx = 110f;
        private const float FooterPx = 60f;
        private const float LinePx = 6f;
        private const float TileRadiusPx = 18f;
        private const float TileInsetPx = 26f;
        private const int Columns = 3;
        private const int DepthBits = 24;

        private static readonly Vector3 FarAway = new Vector3(40000f, 40000f, 0f);

        public static Texture2D Render(RecapCard card, UiTheme theme)
        {
            var root = new GameObject("RecapCard");
            root.transform.position = FarAway;
            var target = new RenderTexture(RecapCard.WidthPx, RecapCard.HeightPx, DepthBits, RenderTextureFormat.ARGB32);
            try
            {
                var canvas = Build(root.transform, card, theme);
                Canvas.ForceUpdateCanvases();
                foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>())
                    label.ForceMeshUpdate();
                var camera = MakeCamera(root.transform, theme.Background, target);
                camera.Render();
                return Read(target);
            }
            finally
            {
                RenderTexture.active = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(root);
            }
        }

        private static Camera MakeCamera(Transform root, Color background, RenderTexture target)
        {
            var host = new GameObject("Camera");
            host.transform.SetParent(root, false);
            host.transform.localPosition = new Vector3(RecapCard.WidthPx / 2f, RecapCard.HeightPx / 2f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = RecapCard.HeightPx / 2f;
            camera.aspect = RecapCard.WidthPx / (float)RecapCard.HeightPx;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.targetTexture = target;
            return camera;
        }

        private static Texture2D Read(RenderTexture target)
        {
            RenderTexture.active = target;
            var picture = new Texture2D(RecapCard.WidthPx, RecapCard.HeightPx, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0f, 0f, RecapCard.WidthPx, RecapCard.HeightPx), 0, 0);
            picture.Apply();
            return picture;
        }

        private static Canvas Build(Transform root, RecapCard card, UiTheme theme)
        {
            var rect = UiElements.Rect("Canvas", root);
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            rect.pivot = Vector2.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(RecapCard.WidthPx, RecapCard.HeightPx);
            rect.localPosition = Vector3.zero;

            UiElements.Stretch(UiElements.Fill("Back", rect, theme.Background).rectTransform);
            UiElements.TopEdge(UiElements.Fill("Line", rect, theme.Gold).rectTransform, LinePx);

            var width = RecapCard.WidthPx - 2f * MarginPx;
            var title = UiElements.Label("Title", rect, theme.BoldFont, TitleSizePx, theme.Gold, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(MarginPx, -MarginPx / 2f), new Vector2(width * 0.55f, HeaderPx));
            title.text = card.Title;
            var period = UiElements.Label("Period", rect, theme.TextFont, PeriodSizePx, theme.TextDim, TextAlignmentOptions.MidlineRight);
            UiElements.Anchor(period.rectTransform, new Vector2(1f, 1f), new Vector2(-MarginPx, -MarginPx / 2f), new Vector2(width * 0.45f, HeaderPx));
            period.text = card.Period;

            var rows = Mathf.CeilToInt(card.Tiles.Count / (float)Columns);
            var top = MarginPx / 2f + HeaderPx + GapPx / 2f;
            var room = RecapCard.HeightPx - top - FooterPx - MarginPx / 2f;
            var tileWidth = (width - GapPx * (Columns - 1)) / Columns;
            var tileHeight = (room - GapPx * (rows - 1)) / Mathf.Max(1, rows);
            for (var i = 0; i < card.Tiles.Count; i++)
            {
                var column = i % Columns;
                var row = i / Columns;
                var tile = UiElements.Fill("Tile " + i, rect, theme.Card, TileRadiusPx);
                UiElements.Anchor(tile.rectTransform, new Vector2(0f, 1f),
                    new Vector2(MarginPx + column * (tileWidth + GapPx), -(top + row * (tileHeight + GapPx))), new Vector2(tileWidth, tileHeight));
                var label = UiElements.Label("Label", tile.rectTransform, theme.TextFont, LabelSizePx, theme.TextDim, TextAlignmentOptions.TopLeft);
                UiElements.Stretch(label.rectTransform, TileInsetPx);
                label.text = card.Tiles[i].Label;
                var value = UiElements.Label("Value", tile.rectTransform, theme.NumberFont != null ? theme.NumberFont : theme.BoldFont, ValueSizePx, theme.Text, TextAlignmentOptions.BottomLeft);
                UiElements.Stretch(value.rectTransform, TileInsetPx);
                value.enableAutoSizing = true;
                value.fontSizeMax = ValueSizePx;
                value.fontSizeMin = LabelSizePx;
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.text = card.Tiles[i].Value;
            }

            var footer = UiElements.Label("Footer", rect, theme.TextFont, FooterSizePx, theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(footer.rectTransform, Vector2.zero, new Vector2(MarginPx, MarginPx / 2f), new Vector2(width, FooterPx));
            footer.overflowMode = TextOverflowModes.Ellipsis;
            footer.text = card.Footer;
            return canvas;
        }
    }
}
