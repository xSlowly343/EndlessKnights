using Game.Core.Party;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Battle
{
    public sealed class AllySilhouettes
    {
        private const float BodyScale = 2f;
        private const float BodyHeightPx = 48f;
        private const float TintStrength = 0.35f;
        private const float GroundPx = 150f;
        private const float MarkPx = 12f;
        private const float MarkGapPx = 4f;
        private const float BubbleWidthPx = 88f;
        private const float BubbleHeightPx = 16f;
        private const float BubbleLowestPx = 74f;
        private const float BubbleStepPx = 17f;
        private const float EdgePx = 4f;
        private const float SwingPeriodSeconds = 0.75f;
        private const float FadedAlpha = 0.45f;
        private const int BodySortingOrder = 9;
        private const float StrikeDepth = 0.5f;
        private const float DefaultStrikeOpacity = 0.5f;

        private static readonly float[] CentresPx = { 49f, 29f, 10f };
        private static readonly Color BubbleFill = new Color32(0x1B, 0x22, 0x36, 0xEB);
        private static readonly Color BubbleText = new Color32(0xFF, 0xD2, 0x7A, 0xFF);
        private static readonly Color FightingMark = new Color32(0x4F, 0xC2, 0x77, 0xFF);
        private static readonly Color RestingMark = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color PeacefulMark = new Color32(0x8F, 0xB4, 0xFF, 0xFF);
        private static readonly Color OfflineMark = new Color32(0xE0, 0x57, 0x4A, 0xFF);

        private readonly SpriteRenderer[] _bodies = new SpriteRenderer[StripAllies.MaxShown];
        private readonly HeroStrike[] _strikes = new HeroStrike[StripAllies.MaxShown];
        private readonly float[] _swingAge = new float[StripAllies.MaxShown];
        private readonly Image[] _marks = new Image[StripAllies.MaxShown];
        private readonly TextMeshProUGUI[] _markTexts = new TextMeshProUGUI[StripAllies.MaxShown];
        private readonly Image[] _bubbles = new Image[StripAllies.MaxShown];
        private readonly TextMeshProUGUI[] _bubbleTexts = new TextMeshProUGUI[StripAllies.MaxShown];
        private readonly StripAlly[] _shown = new StripAlly[StripAllies.MaxShown];
        private readonly float[] _phase = { 0f, 0.31f, 0.57f };
        private int _count;

        private AllySilhouettes(Transform scene, RectTransform canvas, UiTheme theme, int flightSortingOrder, int burstSortingOrder)
        {
            for (var i = 0; i < StripAllies.MaxShown; i++)
            {
                _bodies[i] = HeroModel.Create("Ally " + i, BodyFeet(i), BodyScale, BodySortingOrder, scene);
                _bodies[i].gameObject.SetActive(false);
                _strikes[i] = new HeroStrike("Ally strike " + i, BodyFeet(i), BodyScale, flightSortingOrder, burstSortingOrder, StrikeDepth, scene);
                _strikes[i].UseOpacity(DefaultStrikeOpacity);
                _swingAge[i] = float.MaxValue;
                _marks[i] = UiElements.Dot("Ally mark " + i, canvas, FightingMark);
                StripCanvas.Place(_marks[i].rectTransform, CentresPx[i], GroundPx - BodyHeightPx - MarkGapPx - MarkPx / 2f, Vector2.one * MarkPx, new Vector2(0.5f, 0.5f));
                _markTexts[i] = UiElements.Label("Text", _marks[i].rectTransform, theme.BoldFont, 9f, Color.white, TextAlignmentOptions.Center);
                UiElements.Stretch(_markTexts[i].rectTransform);
                _marks[i].gameObject.SetActive(false);
                _bubbles[i] = UiElements.Fill("Ally reaction " + i, canvas, BubbleFill, BubbleHeightPx / 2f);
                var bubbleX = Mathf.Max(EdgePx + BubbleWidthPx / 2f, CentresPx[i]);
                StripCanvas.Place(_bubbles[i].rectTransform, bubbleX, BubbleLowestPx - i * BubbleStepPx, new Vector2(BubbleWidthPx, BubbleHeightPx), new Vector2(0.5f, 0.5f));
                _bubbleTexts[i] = UiElements.Label("Text", _bubbles[i].rectTransform, theme.BoldFont, 10f, BubbleText, TextAlignmentOptions.Center);
                UiElements.Stretch(_bubbleTexts[i].rectTransform, 3f);
                _bubbleTexts[i].enableAutoSizing = true;
                _bubbleTexts[i].fontSizeMin = 7f;
                _bubbleTexts[i].fontSizeMax = 10f;
                _bubbles[i].gameObject.SetActive(false);
            }
        }

        public static AllySilhouettes Create(Transform scene, RectTransform canvas, UiTheme theme, int flightSortingOrder, int burstSortingOrder) =>
            new AllySilhouettes(scene, canvas, theme, flightSortingOrder, burstSortingOrder);

        public int Shown => _count;

        public HeroStrike Strike(int index) => _strikes[index];

        public void UseStrikeOpacity(float opacity)
        {
            for (var i = 0; i < _strikes.Length; i++)
                _strikes[i].UseOpacity(opacity);
        }

        public string ReactionShown(int index) => _bubbles[index].gameObject.activeSelf ? _bubbleTexts[index].text : string.Empty;

        public void Show(StripAlly[] allies, int count)
        {
            _count = Mathf.Clamp(count, 0, StripAllies.MaxShown);
            for (var i = 0; i < StripAllies.MaxShown; i++)
            {
                var shown = i < _count;
                SetActive(_bodies[i].gameObject, shown);
                SetActive(_marks[i].gameObject, shown);
                if (!shown)
                {
                    SetActive(_bubbles[i].gameObject, false);
                    continue;
                }
                var ally = allies[i];
                _shown[i] = ally;
                var color = Color.Lerp(Color.white, PartyColors.Of(ally.ColorIndex), TintStrength);
                color.a = ally.Faded ? FadedAlpha : 1f;
                _bodies[i].color = color;
                _strikes[i].UseTint(Color.Lerp(Color.white, PartyColors.Of(ally.ColorIndex), TintStrength));
                _marks[i].color = MarkColor(ally.Presence);
                var mark = StripAllies.StatusMark(ally.Presence);
                if (_markTexts[i].text != mark)
                    _markTexts[i].text = mark;
                var reacting = ally.Reaction.Length > 0;
                SetActive(_bubbles[i].gameObject, reacting);
                if (reacting && _bubbleTexts[i].text != ally.Reaction)
                    _bubbleTexts[i].text = ally.Reaction;
            }
        }

        public void Animate(float time, float delta, Vector2 target)
        {
            for (var i = 0; i < StripAllies.MaxShown; i++)
            {
                var swinging = i < _count && _shown[i].Swings;
                var into = swinging ? Mathf.Repeat(time + _phase[i], SwingPeriodSeconds) : float.MaxValue;
                var attacking = into < HeroModel.Attack.Duration;
                if (i < _count)
                {
                    var sprite = attacking ? HeroModel.Attack.SpriteAt(into) : HeroModel.Idle.SpriteAt(time + _phase[i]);
                    if (_bodies[i].sprite != sprite)
                        _bodies[i].sprite = sprite;
                }
                var strikes = attacking && into >= HeroModel.StrikeSeconds && (_swingAge[i] < HeroModel.StrikeSeconds || _swingAge[i] > into);
                if (strikes)
                    _strikes[i].Launch(target);
                _swingAge[i] = into;
                _strikes[i].ShowSlash(into);
                _strikes[i].Animate(delta);
            }
        }

        private static Vector2 BodyFeet(int index) => StripSpace.AtPx(CentresPx[index], GroundPx);

        private static Color MarkColor(PartyPresence presence) => presence switch
        {
            PartyPresence.Resting => RestingMark,
            PartyPresence.Peaceful => PeacefulMark,
            PartyPresence.Offline => OfflineMark,
            _ => FightingMark
        };

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
