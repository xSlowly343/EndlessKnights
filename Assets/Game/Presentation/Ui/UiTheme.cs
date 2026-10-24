using TMPro;
using UnityEngine;

namespace Game.Presentation.Ui
{
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "Ui/UiTheme";

        [SerializeField] private TMP_FontAsset? textFont;
        [SerializeField] private TMP_FontAsset? boldFont;
        [SerializeField] private TMP_FontAsset? numberFont;
        [SerializeField] private Color background = new Color32(0x1B, 0x22, 0x36, 0xFF);
        [SerializeField] private Color card = new Color32(0x26, 0x30, 0x4A, 0xFF);
        [SerializeField] private Color tabBar = new Color32(0x14, 0x1A, 0x2B, 0xFF);
        [SerializeField] private Color border = new Color32(0x3A, 0x48, 0x70, 0xFF);
        [SerializeField] private Color plate = new Color32(0xB0, 0x2A, 0x2A, 0xFF);
        [SerializeField] private Color plateDark = new Color32(0x7A, 0x1E, 0x24, 0xFF);
        [SerializeField] private Color gold = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        [SerializeField] private Color accent = new Color32(0x3F, 0x6F, 0xD1, 0xFF);
        [SerializeField] private Color alert = new Color32(0xE5, 0x48, 0x4D, 0xFF);
        [SerializeField] private Color success = new Color32(0x3C, 0x9A, 0x4E, 0xFF);
        [SerializeField] private Color text = new Color32(0xEE, 0xF2, 0xFF, 0xFF);
        [SerializeField] private Color textDim = new Color32(0x9A, 0xA6, 0xC8, 0xFF);

        public TMP_FontAsset? TextFont => textFont;
        public TMP_FontAsset? BoldFont => boldFont;
        public TMP_FontAsset? NumberFont => numberFont;
        public Color Background => background;
        public Color Card => card;
        public Color TabBar => tabBar;
        public Color Border => border;
        public Color Plate => plate;
        public Color PlateDark => plateDark;
        public Color Gold => gold;
        public Color Accent => accent;
        public Color Alert => alert;
        public Color Success => success;
        public Color Text => text;
        public Color TextDim => textDim;
        public Color TextOnGold => background;

        public static UiTheme Load()
        {
            var theme = Resources.Load<UiTheme>(ResourcePath);
            return theme != null ? theme : CreateInstance<UiTheme>();
        }

        public void UseFonts(TMP_FontAsset text, TMP_FontAsset bold, TMP_FontAsset numbers)
        {
            textFont = text;
            boldFont = bold;
            numberFont = numbers;
        }
    }
}
