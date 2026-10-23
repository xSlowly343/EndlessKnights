using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class SkillBarView : MonoBehaviour
    {
        public const int MaxIcons = 5;

        private const float IconPx = 24f;
        private const float StepPx = 28f;
        private const float FirstCentreXPx = 196f;
        private const float CentreYPx = 18f;
        private const float FramePx = 2f;
        private const float ReadyPopSeconds = 0.18f;
        private const float ReadyPopScale = 0.2f;

        private static readonly float IconSize = StripSpace.Units(IconPx);
        private static readonly Color FrameIdle = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color FrameActive = new Color(1f, 0.9f, 0.4f);
        private static readonly Color CooldownShade = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color LabelColor = Color.white;
        private static readonly Color KeyColor = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color RedGem = new Color(0.8f, 0.25f, 0.22f);
        private static readonly Color GreenGem = new Color(0.25f, 0.65f, 0.3f);
        private static readonly Color BlueGem = new Color(0.25f, 0.45f, 0.85f);
        private static readonly Color WhiteGem = new Color(0.7f, 0.7f, 0.72f);
        private static readonly Color Dimmed = new Color(0.45f, 0.45f, 0.45f);

        private readonly Icon[] _icons = new Icon[MaxIcons];

        public event Action<int>? SlotClicked;

        public static SkillBarView Create(Transform? parent = null)
        {
            var host = new GameObject("SkillBar");
            if (parent != null)
                host.transform.SetParent(parent, false);
            var view = host.AddComponent<SkillBarView>();
            view.Build();
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

        private void Build()
        {
            for (var i = 0; i < MaxIcons; i++)
                _icons[i] = new Icon(i, StripSpace.AtPx(FirstCentreXPx + i * StepPx, CentreYPx), transform);
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            for (var i = 0; i < MaxIcons; i++)
                _icons[i].Animate(delta);
        }

        private static Color ColorOf(GemColor color) => color switch
        {
            GemColor.Red => RedGem,
            GemColor.Green => GreenGem,
            GemColor.Blue => BlueGem,
            _ => WhiteGem
        };

        private static string KeyLabel(int index) => index switch
        {
            SkillSystem.LeftButtonSlot => "Л",
            SkillSystem.RightButtonSlot => "П",
            _ => string.Empty
        };

        private static string ShortName(string name) => name.Length <= 2 ? name : name.Substring(0, 2);

        private sealed class Icon
        {
            private readonly GameObject _root;
            private readonly SpriteRenderer _frame;
            private readonly SpriteRenderer _back;
            private readonly SpriteRenderer _shade;
            private readonly TextMesh _label;
            private readonly Collider2D _collider;
            private readonly Vector2 _centre;
            private GemDef? _shownGem;
            private bool _wasCoolingDown;
            private float _popAge = ReadyPopSeconds;

            public Icon(int index, Vector2 centre, Transform parent)
            {
                _centre = centre;
                _root = new GameObject($"Skill{index}");
                _root.transform.SetParent(parent, false);
                _root.transform.position = centre;
                var frameSize = IconSize + StripSpace.Units(FramePx * 2f);
                _frame = ShapeSprites.Create("Frame", ShapeSprites.Square, FrameIdle, centre, Vector2.one * frameSize, 30, _root.transform);
                _back = ShapeSprites.Create("Back", ShapeSprites.Square, WhiteGem, centre, Vector2.one * IconSize, 31, _root.transform);
                _collider = _back.gameObject.AddComponent<BoxCollider2D>();
                _shade = ShapeSprites.Create("Cooldown", ShapeSprites.Square, CooldownShade, centre, Vector2.one * IconSize, 32, _root.transform);
                _label = StripText.Create("Name", string.Empty, centre, 0.11f, TextAnchor.MiddleCenter, LabelColor, _root.transform, 33);
                var keyPosition = centre + new Vector2(-IconSize / 2f + StripSpace.Units(2f), IconSize / 2f - StripSpace.Units(1f));
                StripText.Create("Key", KeyLabel(index), keyPosition, 0.07f, TextAnchor.UpperLeft, KeyColor, _root.transform, 34);
                _root.SetActive(false);
            }

            public bool Contains(Vector2 worldPoint) => _root.activeInHierarchy && _collider.OverlapPoint(worldPoint);

            public void Hide()
            {
                if (_root.activeSelf)
                    _root.SetActive(false);
                _shownGem = null;
            }

            public void Show(SkillSlot slot, double resource)
            {
                if (!_root.activeSelf)
                    _root.SetActive(true);
                if (!ReferenceEquals(_shownGem, slot.Skill))
                {
                    _shownGem = slot.Skill;
                    _label.text = ShortName(slot.Skill.NameRu);
                }

                var def = slot.Def;
                var affordable = def.ResourceCost <= 0 || resource >= def.ResourceCost;
                var color = ColorOf(slot.Skill.Color);
                _back.color = def.IsPassive || !affordable ? color * Dimmed : color;
                _frame.color = slot.Active ? FrameActive : FrameIdle;
                ShowCooldown((float)slot.CooldownFraction);

                var coolingDown = !slot.Ready;
                if (_wasCoolingDown && !coolingDown)
                    _popAge = 0f;
                _wasCoolingDown = coolingDown;
            }

            public void Animate(float delta)
            {
                _popAge += delta;
                var pop = _popAge < ReadyPopSeconds ? 1f + ReadyPopScale * Mathf.Sin(_popAge / ReadyPopSeconds * Mathf.PI) : 1f;
                _root.transform.localScale = new Vector3(pop, pop, 1f);
            }

            private void ShowCooldown(float fraction)
            {
                var visible = fraction > 0f;
                if (_shade.gameObject.activeSelf != visible)
                    _shade.gameObject.SetActive(visible);
                if (!visible)
                    return;
                var height = IconSize * Mathf.Clamp01(fraction);
                var transform = _shade.transform;
                transform.localScale = new Vector3(IconSize, height, 1f);
                transform.position = new Vector3(_centre.x, _centre.y + IconSize / 2f - height / 2f, transform.position.z);
            }
        }
    }
}
