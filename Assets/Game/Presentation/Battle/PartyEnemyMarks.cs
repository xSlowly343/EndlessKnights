using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Ui;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Battle
{
    public sealed class PartyEnemyMarks
    {
        private const float BadgePx = 18f;
        private const float BadgeCorePx = 10f;
        private const float BadgeRightPx = 468f;
        private const float BadgeStepPx = 22f;
        private const float BadgeRowPx = 40f;
        private const float CardCentreXPx = 230f;
        private const float CardCentreYPx = 62f;
        private const float CardWidthPx = 220f;
        private const float CardHeightPx = 50f;
        private const float CardInsetPx = 8f;
        private const float CardBarPx = 8f;
        private const float CardSeconds = 4f;
        private const float CardFadeSeconds = 0.6f;

        private static readonly Color CardFill = new Color32(0x1B, 0x22, 0x36, 0xF0);
        private static readonly Color CardBorder = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color CardTrack = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color NoOwner = new Color32(0x8C, 0x97, 0xB6, 0xFF);

        private readonly Image[] _badges = new Image[ShownEffect.MaxShown];
        private readonly Image[] _badgeCores = new Image[ShownEffect.MaxShown];
        private readonly TextMeshProUGUI[] _badgeTexts = new TextMeshProUGUI[ShownEffect.MaxShown];
        private readonly int[] _shownSeconds = new int[ShownEffect.MaxShown];
        private readonly CanvasGroup _card;
        private readonly TextMeshProUGUI _cardTitle;
        private readonly Image[] _cardParts = new Image[PartyRules.MaxSlots];
        private readonly TextMeshProUGUI[] _cardLabels = new TextMeshProUGUI[PartyRules.MaxSlots];
        private float _cardAge = CardSeconds;

        private PartyEnemyMarks(RectTransform canvas, UiTheme theme)
        {
            var centre = new Vector2(0.5f, 0.5f);
            for (var i = 0; i < _badges.Length; i++)
            {
                _badges[i] = UiElements.Dot("Enemy effect " + i, canvas, NoOwner);
                StripCanvas.Place(_badges[i].rectTransform, BadgeRightPx - i * BadgeStepPx, BadgeRowPx, Vector2.one * BadgePx, centre);
                _badgeCores[i] = UiElements.Dot("Gem", _badges[i].rectTransform, GemColors.White);
                UiElements.Stretch(_badgeCores[i].rectTransform, (BadgePx - BadgeCorePx) / 2f);
                _badgeTexts[i] = UiElements.Label("Seconds", _badges[i].rectTransform, theme.BoldFont, 9f, Color.white, TextAlignmentOptions.Center);
                UiElements.Stretch(_badgeTexts[i].rectTransform);
                _shownSeconds[i] = -1;
                _badges[i].gameObject.SetActive(false);
            }
            var border = UiElements.Fill("Boss shares", canvas, CardBorder, 8f);
            StripCanvas.Place(border.rectTransform, CardCentreXPx, CardCentreYPx, new Vector2(CardWidthPx, CardHeightPx), centre);
            UiElements.Stretch(UiElements.Fill("Inner", border.rectTransform, CardFill, 7f).rectTransform, 1f);
            _card = border.gameObject.AddComponent<CanvasGroup>();
            _cardTitle = UiElements.Label("Title", border.rectTransform, theme.BoldFont, 11f, theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(_cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(CardInsetPx, -4f), new Vector2(CardWidthPx - 2f * CardInsetPx, 15f));
            _cardTitle.overflowMode = TextOverflowModes.Ellipsis;
            var track = UiElements.Fill("Bar", border.rectTransform, CardTrack, CardBarPx / 2f).rectTransform;
            UiElements.Anchor(track, new Vector2(0f, 1f), new Vector2(CardInsetPx, -21f), new Vector2(CardWidthPx - 2f * CardInsetPx, CardBarPx));
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                _cardParts[slot] = UiElements.Fill("Share " + slot, track, NoOwner);
                _cardLabels[slot] = UiElements.Label("Label " + slot, border.rectTransform, theme.TextFont, 10f, theme.Text, TextAlignmentOptions.MidlineLeft);
                var labelWidth = (CardWidthPx - 2f * CardInsetPx) / PartyRules.MaxSlots;
                UiElements.Anchor(_cardLabels[slot].rectTransform, new Vector2(0f, 1f), new Vector2(CardInsetPx + slot * labelWidth, -32f), new Vector2(labelWidth, 14f));
                _cardLabels[slot].overflowMode = TextOverflowModes.Ellipsis;
                _cardLabels[slot].enableAutoSizing = true;
                _cardLabels[slot].fontSizeMin = 7f;
                _cardLabels[slot].fontSizeMax = 10f;
            }
            border.gameObject.SetActive(false);
        }

        public static PartyEnemyMarks Create(RectTransform canvas, UiTheme theme) => new PartyEnemyMarks(canvas, theme);

        public int BadgesShown { get; private set; }
        public bool CardShown => _card.gameObject.activeSelf;
        public string CardTitle => _cardTitle.text;

        public void ShowBadges(EnemyBadge[] badges, int count)
        {
            BadgesShown = Mathf.Clamp(count, 0, _badges.Length);
            for (var i = 0; i < _badges.Length; i++)
            {
                var shown = i < BadgesShown;
                if (_badges[i].gameObject.activeSelf != shown)
                    _badges[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var badge = badges[i];
                _badges[i].color = badge.ColorIndex == PartyStripMarks.NoColor ? NoOwner : PartyColors.Of(badge.ColorIndex);
                _badgeCores[i].color = GemColors.Of(badge.GemColor);
                if (_shownSeconds[i] == badge.SecondsLeft)
                    continue;
                _shownSeconds[i] = badge.SecondsLeft;
                _badgeTexts[i].text = BattleTexts.Whole(badge.SecondsLeft);
            }
        }

        public void ShowBossShares(string title, BossShare[] shares, int count)
        {
            _cardTitle.text = title;
            var trackWidth = CardWidthPx - 2f * CardInsetPx;
            var x = 0f;
            for (var slot = 0; slot < _cardParts.Length; slot++)
            {
                var shown = slot < count;
                _cardParts[slot].gameObject.SetActive(shown);
                _cardLabels[slot].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var color = PartyColors.Of(shares[slot].ColorIndex);
                var width = trackWidth * Mathf.Clamp01((float)shares[slot].Share);
                var part = _cardParts[slot].rectTransform;
                part.anchorMin = new Vector2(0f, 0.5f);
                part.anchorMax = new Vector2(0f, 0.5f);
                part.pivot = new Vector2(0f, 0.5f);
                part.anchoredPosition = new Vector2(x, 0f);
                part.sizeDelta = new Vector2(width, CardBarPx);
                _cardParts[slot].color = color;
                x += width;
                _cardLabels[slot].text = shares[slot].Label;
                _cardLabels[slot].color = Color.Lerp(color, Color.white, 0.45f);
            }
            _cardAge = 0f;
            _card.alpha = 1f;
            _card.gameObject.SetActive(true);
        }

        public void Animate(float delta)
        {
            if (!_card.gameObject.activeSelf)
                return;
            _cardAge += delta;
            if (_cardAge >= CardSeconds)
            {
                _card.gameObject.SetActive(false);
                return;
            }
            _card.alpha = Mathf.Clamp01((CardSeconds - _cardAge) / CardFadeSeconds);
        }
    }
}
