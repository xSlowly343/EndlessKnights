using Game.Core.Math;
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
        private const float GroundPx = 150f;
        private const float MarkPx = 12f;
        private const float MarkGapPx = 4f;
        private const float PastimePx = 18f;
        private const float PlaceBadgePx = 10f;
        private const float BubbleWidthPx = 88f;
        private const float BubbleHeightPx = 16f;
        private const float BubbleLowestPx = 74f;
        private const float BubbleStepPx = 17f;
        private const float EdgePx = 4f;
        private const float SwingGapSeconds = 0.34f;
        private const float RestingSwingAge = 100f;
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
        private readonly HeroSkin[] _skins = new HeroSkin[StripAllies.MaxShown];
        private readonly float[] _swingAge = new float[StripAllies.MaxShown];
        private readonly AllySlashTurns _turns = new AllySlashTurns(StripAllies.MaxShown);
        private readonly bool[] _ready = new bool[StripAllies.MaxShown];
        private readonly BigNumber[] _queuedDamage = new BigNumber[StripAllies.MaxShown];
        private readonly BigNumber[] _flyingDamage = new BigNumber[StripAllies.MaxShown];
        private readonly Image[] _marks = new Image[StripAllies.MaxShown];
        private readonly TextMeshProUGUI[] _markTexts = new TextMeshProUGUI[StripAllies.MaxShown];
        private readonly Image[] _pastimes = new Image[StripAllies.MaxShown];
        private readonly Image[] _placeBadges = new Image[StripAllies.MaxShown];
        private readonly byte[] _pastimeShown = new byte[StripAllies.MaxShown];
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
                _skins[i] = HeroModel.Skin(default);
                _strikes[i] = new HeroStrike("Ally strike " + i, BodyFeet(i), BodyScale, flightSortingOrder, burstSortingOrder, StrikeDepth, scene);
                _strikes[i].UseOpacity(DefaultStrikeOpacity);
                Rest(i);
                _marks[i] = UiElements.Dot("Ally mark " + i, canvas, FightingMark);
                StripCanvas.Place(_marks[i].rectTransform, CentresPx[i], GroundPx - BodyHeightPx - MarkGapPx - MarkPx / 2f, Vector2.one * MarkPx, new Vector2(0.5f, 0.5f));
                _markTexts[i] = UiElements.Label("Text", _marks[i].rectTransform, theme.BoldFont, 9f, Color.white, TextAlignmentOptions.Center);
                UiElements.Stretch(_markTexts[i].rectTransform);
                _marks[i].gameObject.SetActive(false);
                _pastimes[i] = UiElements.Icon("Ally pastime " + i, canvas, null, Color.white);
                StripCanvas.Place(_pastimes[i].rectTransform, CentresPx[i], PastimeCentrePx, Vector2.one * PastimePx, new Vector2(0.5f, 0.5f));
                _pastimes[i].gameObject.SetActive(false);
                _placeBadges[i] = UiElements.Icon("Ally place " + i, _pastimes[i].rectTransform, null, PeacefulMark);
                UiElements.Anchor(_placeBadges[i].rectTransform, Vector2.one, Vector2.one * (PlaceBadgePx / 3f), Vector2.one * PlaceBadgePx);
                _placeBadges[i].gameObject.SetActive(false);
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

        public event System.Action<BigNumber, Color>? StrikeLanded;

        public int Shown => _count;

        public void Hit(int index, int hits, BigNumber damage)
        {
            if (index < 0 || index >= _count)
                return;
            if (hits > 0)
                _turns.Hit(index, hits);
            if (_turns.Pending(index) > 0)
                _queuedDamage[index] += damage;
            else if (_strikes[index].Flying > 0)
                _flyingDamage[index] += damage;
            else
                Land(index, damage);
        }

        public HeroStrike Strike(int index) => _strikes[index];

        public void UseStrikeOpacity(float opacity)
        {
            for (var i = 0; i < _strikes.Length; i++)
                _strikes[i].UseOpacity(opacity);
        }

        public void UseStrikeDim(float dim)
        {
            for (var i = 0; i < _strikes.Length; i++)
                _strikes[i].UseDim(dim);
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
                    SetActive(_pastimes[i].gameObject, false);
                    Rest(i);
                    continue;
                }
                var ally = allies[i];
                var changed = ally.Look != _shown[i].Look || ally.Slot != _shown[i].Slot;
                _shown[i] = ally;
                if (changed)
                {
                    _skins[i] = HeroModel.Skin(ally.Look);
                    _strikes[i].Use(HeroModel.Strike(ally.Look.Strike));
                }
                var alpha = ally.Faded ? FadedAlpha : 1f;
                if (_bodies[i].color.a != alpha)
                    _bodies[i].color = new Color(1f, 1f, 1f, alpha);
                _marks[i].color = MarkColor(ally.Presence);
                var mark = ShowPastime(i, ally) ? string.Empty : StripAllies.StatusMark(ally.Presence);
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
                if (_swingAge[i] < RestingSwingAge)
                    _swingAge[i] += delta;
                _ready[i] = _swingAge[i] >= SwingGapSeconds;
            }
            if (_turns.TryTake(time, _ready, out var turn, out var tier))
                Swing(turn, tier);
            for (var i = 0; i < StripAllies.MaxShown; i++)
            {
                if (i < _count)
                {
                    var sprite = _swingAge[i] < HeroModel.Attack.Duration ? _skins[i].AttackAt(time).SpriteAt(_swingAge[i]) : _skins[i].IdleAt(time).SpriteAt(time + _phase[i]);
                    if (_bodies[i].sprite != sprite)
                        _bodies[i].sprite = sprite;
                }
                _strikes[i].ShowSlash(_swingAge[i]);
                var flying = _strikes[i].Flying;
                _strikes[i].Animate(delta, target);
                var landed = flying - _strikes[i].Flying;
                if (landed <= 0)
                    continue;
                var amount = landed >= flying ? _flyingDamage[i] : _flyingDamage[i] * (landed / (double)flying);
                _flyingDamage[i] -= amount;
                Land(i, amount);
            }
        }

        private void Swing(int index, int tier)
        {
            _flyingDamage[index] += _queuedDamage[index];
            _queuedDamage[index] = BigNumber.Zero;
            _swingAge[index] = HeroModel.StrikeSeconds;
            _strikes[index].Launch(tier);
        }

        private void Land(int index, BigNumber amount)
        {
            if (amount.Sign > 0)
                StrikeLanded?.Invoke(amount, PartyColors.Of(_shown[index].ColorIndex));
        }

        private void Rest(int index)
        {
            _swingAge[index] = RestingSwingAge;
            _turns.Clear(index);
            _queuedDamage[index] = BigNumber.Zero;
            _flyingDamage[index] = BigNumber.Zero;
        }

        private bool ShowPastime(int index, StripAlly ally)
        {
            var packed = AllyPastimeView.Shows(ally) ? ally.Pastime.Packed : (byte)0;
            if (packed != _pastimeShown[index])
            {
                _pastimeShown[index] = packed;
                var sprite = packed == 0 ? null : UiIcons.Get(AllyPastimeView.IconGroup, AllyPastimeView.Icon(ally.Pastime));
                UiElements.Use(_pastimes[index], sprite);
                var badge = sprite == null ? null : UiIcons.Get(AllyPastimeView.IconGroup, AllyPastimeView.PlaceIcon(ally.Pastime));
                UiElements.Use(_placeBadges[index], badge);
                SetActive(_placeBadges[index].gameObject, badge != null);
            }
            var shown = _pastimeShown[index] != 0 && _pastimes[index].sprite != null;
            SetActive(_pastimes[index].gameObject, shown);
            return shown;
        }

        public string PastimeShown(int index) => _pastimes[index].gameObject.activeSelf && _pastimes[index].sprite != null ? _pastimes[index].sprite!.name : string.Empty;

        private static float PastimeCentrePx => GroundPx - BodyHeightPx - MarkGapPx - MarkPx - MarkGapPx - PastimePx / 2f;

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
