using System;
using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class PartyPanel
    {
        private const float PaddingPx = 12f;
        private const float TopPx = 4f;
        private const float GapPx = 6f;
        private const float RadiusPx = 8f;
        private const float EnemyCardPx = 78f;
        private const float HintCardPx = 44f;
        private const float BarPx = 10f;
        private const float RowPx = 52f;
        private const float AvatarPx = 36f;
        private const float ChipHeightPx = 18f;
        private const float ChipWidthPx = 96f;
        private const float FreePx = 40f;
        private const float ButtonPx = 32f;

        private static readonly Color[] AllyColors =
        {
            new Color32(0x3F, 0x6F, 0xD1, 0xFF),
            new Color32(0x4F, 0xC2, 0x77, 0xFF),
            new Color32(0xB0, 0x7C, 0xFF, 0xFF),
            new Color32(0xFF, 0x9A, 0x3C, 0xFF),
        };

        private static readonly Color FightingChip = new Color32(0x1F, 0x4A, 0x33, 0xFF);
        private static readonly Color FightingText = new Color32(0xBF, 0xF2, 0xD2, 0xFF);
        private static readonly Color RestingChip = new Color32(0x4A, 0x3A, 0x16, 0xFF);
        private static readonly Color RestingText = new Color32(0xFF, 0xE2, 0xA0, 0xFF);
        private static readonly Color OfflineChip = new Color32(0x3A, 0x1E, 0x24, 0xFF);
        private static readonly Color OfflineText = new Color32(0xFF, 0xB4, 0xB4, 0xFF);
        private static readonly Color BarBack = new Color32(0x12, 0x18, 0x28, 0xFF);
        private static readonly Color LeaveText = new Color32(0xFF, 0xB4, 0xB4, 0xFF);

        private readonly UiTheme _theme;
        private readonly RectTransform _root;
        private readonly List<MemberRow> _rows = new List<MemberRow>();
        private readonly RectTransform _enemyCard;
        private readonly TextMeshProUGUI _enemyTitle;
        private readonly RectTransform _barFill;
        private readonly RectTransform _hintCard;
        private readonly UiButton _free;
        private readonly UiButton _help;
        private readonly UiButton _links;
        private readonly UiButton _leave;
        private PartyClient? _client;
        private string _code = string.Empty;

        private PartyPanel(RectTransform content, UiTheme theme)
        {
            _theme = theme;
            _root = UiElements.Stretch(UiElements.Rect("Party", content));
            _enemyCard = BuildEnemyCard(out _enemyTitle, out _barFill);
            _hintCard = BuildHintCard();
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                _rows.Add(MemberRow.Create(slot, _root, theme));
            _free = BuildFreeSlot();
            var buttons = UiElements.Rect("Buttons", _root);
            var buttonWidth = (Width() - 2f * GapPx) / 3f;
            _help = Button(buttons, PartyRoster.Help, 0f, buttonWidth, theme.Accent, Color.white, theme.Accent);
            _links = Button(buttons, PartyRoster.Links, buttonWidth + GapPx, buttonWidth, theme.Card, theme.Text, theme.Card);
            _leave = Button(buttons, PartyRoster.Leave, 2f * (buttonWidth + GapPx), buttonWidth, theme.Background, LeaveText, theme.Plate);
            _root.gameObject.SetActive(false);
        }

        public static PartyPanel Create(RectTransform content, UiTheme theme) => new PartyPanel(content, theme);

        public event Action<string>? NoticeRequested;
        public event Action<PartyAction>? ActionRequested;

        public bool IsShown => _root.gameObject.activeSelf;
        public int ShownMembers { get; private set; }
        public bool ShowsEnemy => _enemyCard.gameObject.activeSelf;

        public void UseClient(PartyClient? client) => _client = client;

        public void UseCode(string code) => _code = code;

        public void Show(bool shown, GameLoop? loop)
        {
            _root.gameObject.SetActive(shown);
            if (shown && loop != null)
                Refresh(loop);
        }

        public void Refresh(GameLoop loop)
        {
            if (!IsShown)
                return;
            var inParty = _client != null && _client.InParty;
            var rows = inParty ? PartyRoster.From(loop.State, _client!) : PartyRoster.Solo(loop.State);
            var y = TopPx;
            _enemyCard.gameObject.SetActive(inParty);
            _hintCard.gameObject.SetActive(!inParty);
            if (inParty)
            {
                Place(_enemyCard, y, EnemyCardPx);
                y += EnemyCardPx + GapPx;
                ShowEnemy();
            }
            else
            {
                Place(_hintCard, y, HintCardPx);
                y += HintCardPx + GapPx;
            }
            ShownMembers = 0;
            var hasFree = false;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = i < rows.Count ? rows[i] : PartyRow.Free;
                hasFree |= row.IsFree;
                _rows[i].Show(row, _theme);
                if (row.IsFree)
                    continue;
                ShownMembers++;
                Place(_rows[i].Rect, y, RowPx);
                y += RowPx + GapPx;
            }
            _free.Show(hasFree);
            if (hasFree)
            {
                Place(_free.Rect, y, FreePx);
                y += FreePx + GapPx;
            }
            Place((RectTransform)_help.Rect.parent, y, ButtonPx);
            var hasCode = _code.Length > 0;
            _help.Text = PartyActions.Label(PartyActions.Left(hasCode), _code);
            _links.Text = PartyActions.Label(PartyActions.Middle(hasCode), _code);
            _leave.Text = PartyActions.Label(PartyActions.Right(hasCode), _code);
        }

        public bool TryClick(Vector2 worldPoint)
        {
            if (!IsShown)
                return false;
            var hasCode = _code.Length > 0;
            var action = _help.Contains(worldPoint) || _free.Contains(worldPoint) ? PartyActions.Left(hasCode)
                : _links.Contains(worldPoint) ? PartyActions.Middle(hasCode)
                : _leave.Contains(worldPoint) ? PartyActions.Right(hasCode)
                : PartyAction.None;
            if (action == PartyAction.None)
                return false;
            if (ActionRequested != null)
                ActionRequested(action);
            else
                NoticeRequested?.Invoke(PartyRoster.NotYet);
            return true;
        }

        public Vector2 WorldPointOfHelp() => _help.Rect.TransformPoint(_help.Rect.rect.center);

        private void ShowEnemy()
        {
            _enemyTitle.text = PartyRoster.Enemy;
            var full = _client!.BarFull;
            var left = full > 0 ? Mathf.Clamp01((float)(_client.PredictedBarLeft / full)) : 0f;
            _barFill.anchorMax = new Vector2(left, 1f);
        }

        private RectTransform BuildEnemyCard(out TextMeshProUGUI title, out RectTransform fill)
        {
            var card = UiElements.Fill("Enemy", _root, _theme.Card, RadiusPx).rectTransform;
            title = UiElements.Label("Title", card, _theme.BoldFont, 13f, _theme.Text, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -8f), new Vector2(Width() - 2f * PaddingPx, 18f));
            title.overflowMode = TextOverflowModes.Ellipsis;
            var bar = UiElements.Fill("Bar", card, BarBack, BarPx / 2f).rectTransform;
            UiElements.Anchor(bar, new Vector2(0f, 1f), new Vector2(PaddingPx, -32f), new Vector2(Width() - 2f * PaddingPx, BarPx));
            fill = UiElements.Fill("Fill", bar, _theme.Alert, BarPx / 2f).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var hint = UiElements.Label("Hint", card, _theme.TextFont, 11f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(PaddingPx, -50f), new Vector2(Width() - 2f * PaddingPx, 16f));
            hint.text = PartyRoster.BarHint;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            return card;
        }

        private RectTransform BuildHintCard()
        {
            var card = UiElements.Fill("Solo", _root, _theme.Card, RadiusPx).rectTransform;
            var text = UiElements.Label("Text", card, _theme.TextFont, 12f, _theme.TextDim, TextAlignmentOptions.MidlineLeft);
            UiElements.Stretch(text.rectTransform, PaddingPx);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = PartyRoster.SoloHint;
            return card;
        }

        private UiButton BuildFreeSlot()
        {
            var border = UiElements.Fill("Free", _root, _theme.Border, RadiusPx);
            UiElements.Stretch(UiElements.Fill("Inner", border.rectTransform, _theme.Background, RadiusPx - PanelCanvas.BorderPx).rectTransform, PanelCanvas.BorderPx);
            var label = UiElements.Label("Text", border.rectTransform, _theme.TextFont, 12f, _theme.TextDim, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform);
            label.text = "+  " + PartyRoster.FreeSlot;
            return new UiButton(border, label);
        }

        private UiButton Button(RectTransform row, string text, float x, float width, Color fill, Color textColor, Color border)
        {
            var back = UiElements.Fill(text, row, border, RadiusPx);
            UiElements.Anchor(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, ButtonPx));
            UiElements.Stretch(UiElements.Raise(UiElements.Fill("Inner", back.rectTransform, fill, RadiusPx - PanelCanvas.BorderPx)).rectTransform, PanelCanvas.BorderPx);
            var label = UiElements.Label("Text", back.rectTransform, _theme.BoldFont, 12f, textColor, TextAlignmentOptions.Center);
            UiElements.Stretch(label.rectTransform, 4f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 12f;
            label.text = text;
            return new UiButton(back, label);
        }

        private static void Place(RectTransform rect, float y, float height) =>
            UiElements.Anchor(rect, new Vector2(0f, 1f), new Vector2(PaddingPx, -y), new Vector2(Width(), height));

        private static float Width() => PanelGeometry.WidthPx - 2f * PanelCanvas.BorderPx - 2f * PaddingPx;

        private sealed class MemberRow
        {
            private readonly Image _avatar;
            private readonly TextMeshProUGUI _name;
            private readonly TextMeshProUGUI _details;
            private readonly Image _chip;
            private readonly TextMeshProUGUI _status;
            private readonly Color _color;

            private MemberRow(Image back, Image avatar, TextMeshProUGUI name, TextMeshProUGUI details, Image chip, TextMeshProUGUI status, Color color)
            {
                Back = back;
                _avatar = avatar;
                _name = name;
                _details = details;
                _chip = chip;
                _status = status;
                _color = color;
            }

            public Image Back { get; }
            public RectTransform Rect => Back.rectTransform;

            public static MemberRow Create(int slot, RectTransform parent, UiTheme theme)
            {
                var back = UiElements.Fill("Member " + slot, parent, theme.Card, RadiusPx);
                var rect = back.rectTransform;
                var middleLeft = new Vector2(0f, 0.5f);
                var color = AllyColors[slot % AllyColors.Length];
                var avatar = UiElements.Dot("Avatar", rect, color);
                UiElements.Anchor(avatar.rectTransform, middleLeft, new Vector2(PaddingPx, 0f), Vector2.one * AvatarPx);
                var textLeft = 2f * PaddingPx + AvatarPx;
                var textWidth = Width() - textLeft - ChipWidthPx - 2f * PaddingPx;
                var name = UiElements.Label("Name", rect, theme.BoldFont, 14f, theme.Text, TextAlignmentOptions.BottomLeft);
                UiElements.Anchor(name.rectTransform, middleLeft, new Vector2(textLeft, 9f), new Vector2(textWidth, 18f));
                var details = UiElements.Label("Details", rect, theme.TextFont, 11f, theme.TextDim, TextAlignmentOptions.TopLeft);
                UiElements.Anchor(details.rectTransform, middleLeft, new Vector2(textLeft, -9f), new Vector2(textWidth, 16f));
                details.overflowMode = TextOverflowModes.Ellipsis;
                var chip = UiElements.Fill("Status", rect, FightingChip, ChipHeightPx / 2f);
                UiElements.Anchor(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(-PaddingPx, 0f), new Vector2(ChipWidthPx, ChipHeightPx));
                var status = UiElements.Label("Text", chip.rectTransform, theme.TextFont, 11f, FightingText, TextAlignmentOptions.Center);
                UiElements.Stretch(status.rectTransform);
                return new MemberRow(back, avatar, name, details, chip, status, color);
            }

            public void Show(PartyRow row, UiTheme theme)
            {
                Back.gameObject.SetActive(!row.IsFree);
                if (row.IsFree)
                    return;
                _avatar.color = row.IsSelf ? AllyColors[0] : _color;
                _name.text = row.Name;
                _name.color = row.IsSelf ? theme.Text : Color.Lerp(_color, Color.white, 0.45f);
                _details.text = row.Details;
                _status.text = PartyRoster.Status(row.Presence);
                _chip.color = row.Presence == PartyPresence.Fighting ? FightingChip : row.Presence == PartyPresence.Offline ? OfflineChip : RestingChip;
                _status.color = row.Presence == PartyPresence.Fighting ? FightingText : row.Presence == PartyPresence.Offline ? OfflineText : RestingText;
            }
        }
    }
}
