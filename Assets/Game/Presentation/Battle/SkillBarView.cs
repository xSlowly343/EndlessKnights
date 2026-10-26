using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Ui;
using Game.Presentation.Strip;
using Game.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Battle
{
    public sealed class SkillBarView : MonoBehaviour
    {
        public const int MaxIcons = 5;

        private const float IconPx = 22f;
        private const float StepPx = 28f;
        private const float FirstCentreXPx = 59f;
        private const float CentreYPx = 166f;
        private const float FramePx = 1.5f;
        private const float RechargeAlpha = 0.45f;
        private const float RadiusPx = 6f;
        private const float ReadyPopSeconds = 0.18f;
        private const float ReadyPopScale = 0.2f;
        private const int SortingOrder = 30;

        private static readonly Color FrameIdle = new Color32(0x3A, 0x48, 0x70, 0xFF);
        private static readonly Color FrameActive = new Color32(0xF0, 0xB4, 0x3C, 0xFF);
        private static readonly Color BackColor = new Color32(0x14, 0x1A, 0x2B, 0xFF);
        private static readonly Color LabelColor = Color.white;
        private static readonly Color LabelCooling = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color KeyColor = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color Dimmed = new Color(0.45f, 0.45f, 0.45f);

        private readonly Icon[] _icons = new Icon[MaxIcons];

        public event Action<int>? SlotClicked;

        public static SkillBarView Create(Transform? parent = null)
        {
            var canvas = StripCanvas.Create("SkillBar", SortingOrder, parent);
            var view = canvas.gameObject.AddComponent<SkillBarView>();
            view.Build(canvas);
            return view;
        }

        public bool TryClick(Vector2 worldPoint)
        {
            for (var i = 0; i < MaxIcons; i++)
            {
                if (!_icons[i].Contains(worldPoint))
                    continue;
                SlotClicked?.Invoke(i);
                return true;
            }
            return false;
        }

        public Vector2 WorldPointOf(int index) => _icons[index].WorldCentre;

        public string LabelOf(int index) => _icons[index].Label;

        public bool IsShown(int index) => _icons[index].IsShown;

        public void Show(IReadOnlyList<SkillSlot> slots, double resource)
        {
            for (var i = 0; i < MaxIcons; i++)
            {
                if (i < slots.Count)
                    _icons[i].Show(slots[i], resource);
                else
                    _icons[i].Hide();
            }
        }

        private void Build(RectTransform canvas)
        {
            var theme = UiTheme.Load();
            for (var i = 0; i < MaxIcons; i++)
                _icons[i] = new Icon(i, FirstCentreXPx + i * StepPx, canvas, theme);
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            for (var i = 0; i < MaxIcons; i++)
                _icons[i].Animate(delta);
        }

        private static string KeyLabel(int index) => index switch
        {
            SkillSystem.LeftButtonSlot => "Л",
            SkillSystem.RightButtonSlot => "П",
            _ => string.Empty
        };

        private static string ShortName(string name) => name.Length <= 2 ? name : name.Substring(0, 2);

        private sealed class Icon
        {
            private readonly RectTransform _root;
            private readonly Image _frame;
            private readonly Image _back;
            private readonly Image _recharge;
            private readonly TextMeshProUGUI _label;
            private readonly TextMeshProUGUI _seconds;
            private int _shownSeconds = -1;
            private GemDef? _shownGem;
            private bool _wasCoolingDown;
            private float _popAge = ReadyPopSeconds;

            public Icon(int index, float centreXPx, RectTransform canvas, UiTheme theme)
            {
                var framePx = IconPx + 2f * FramePx;
                _frame = UiElements.Fill($"Skill{index}", canvas, FrameIdle, RadiusPx + FramePx);
                _root = StripCanvas.Place(_frame.rectTransform, centreXPx, CentreYPx, Vector2.one * framePx, new Vector2(0.5f, 0.5f));
                var hit = _root.gameObject.AddComponent<BoxCollider2D>();
                hit.size = Vector2.one * framePx;
                _root.gameObject.AddComponent<StripClickable>();
                _back = UiElements.Fill("Back", _root, BackColor, RadiusPx);
                UiElements.Stretch(_back.rectTransform, FramePx);
                _recharge = UiElements.Fill("Recharge", _back.rectTransform, GemColors.White, RadiusPx);
                UiElements.BottomEdge(_recharge.rectTransform, 0f);
                _recharge.gameObject.SetActive(false);
                _label = UiElements.Label("Name", _back.rectTransform, theme.BoldFont, 10f, LabelColor, TextAlignmentOptions.Center);
                UiElements.Stretch(_label.rectTransform);
                _seconds = UiElements.Label("Seconds", _back.rectTransform, theme.NumberFont, 12f, LabelColor, TextAlignmentOptions.Center);
                UiElements.Stretch(_seconds.rectTransform);
                _seconds.gameObject.SetActive(false);
                var key = UiElements.Label("Key", _back.rectTransform, theme.BoldFont, 7f, KeyColor, TextAlignmentOptions.TopLeft);
                UiElements.Stretch(key.rectTransform, 1.5f);
                key.text = KeyLabel(index);
                _root.gameObject.SetActive(false);
            }

            public bool IsShown => _root.gameObject.activeSelf;
            public string Label => _label.text;
            public Vector2 WorldCentre => _root.TransformPoint(_root.rect.center);

            public bool Contains(Vector2 worldPoint) => UiElements.Contains(_root, worldPoint);

            public void Hide()
            {
                if (_root.gameObject.activeSelf)
                    _root.gameObject.SetActive(false);
                _shownGem = null;
            }

            public void Show(SkillSlot slot, double resource)
            {
                if (!_root.gameObject.activeSelf)
                    _root.gameObject.SetActive(true);
                if (!ReferenceEquals(_shownGem, slot.Skill))
                {
                    _shownGem = slot.Skill;
                    _label.text = ShortName(slot.Skill.NameRu);
                }

                var def = slot.Def;
                var affordable = def.ResourceCost <= 0 || resource >= def.ResourceCost;
                var color = GemColors.Of(slot.Skill.Color);
                var coolingDown = !slot.Ready;
                var usable = !def.IsPassive && affordable && !coolingDown;
                _frame.color = slot.Active ? FrameActive : usable ? color : def.IsPassive || !affordable ? color * Dimmed : FrameIdle;
                _label.color = coolingDown ? LabelCooling : LabelColor;
                ShowCooldown((float)slot.CooldownFraction, slot.CooldownTicks, color);

                if (_wasCoolingDown && !coolingDown)
                    _popAge = 0f;
                _wasCoolingDown = coolingDown;
            }

            public void Animate(float delta)
            {
                _popAge += delta;
                var pop = _popAge < ReadyPopSeconds ? 1f + ReadyPopScale * Mathf.Sin(_popAge / ReadyPopSeconds * Mathf.PI) : 1f;
                _root.localScale = new Vector3(pop, pop, 1f);
            }

            private void ShowCooldown(float fraction, int ticksLeft, Color color)
            {
                var visible = fraction > 0f;
                if (_recharge.gameObject.activeSelf != visible)
                    _recharge.gameObject.SetActive(visible);
                if (_seconds.gameObject.activeSelf != visible)
                    _seconds.gameObject.SetActive(visible);
                if (!visible)
                    return;
                color.a = RechargeAlpha;
                _recharge.color = color;
                _recharge.rectTransform.sizeDelta = new Vector2(0f, IconPx * (1f - Mathf.Clamp01(fraction)));
                var seconds = Mathf.CeilToInt(ticksLeft / (float)GameLoop.TicksPerSecond);
                if (seconds != _shownSeconds)
                    _seconds.text = BattleTexts.Whole(_shownSeconds = seconds);
            }
        }
    }
}
