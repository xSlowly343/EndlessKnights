using Game.Core.Ui;
using TMPro;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public sealed class MilestoneView
    {
        private const float ArtScale = 3f;
        private const int LargeSortingOrder = 20;
        private const float LabelRiseSeconds = 0.25f;
        private const float LabelFadeSeconds = 0.2f;

        private readonly SheetPlayer _small;
        private readonly SheetPlayer _medium;
        private readonly SheetPlayer _large;
        private readonly Vector2 _hero;
        private readonly Vector2 _corner;
        private readonly TextMeshProUGUI _level;
        private readonly Color _levelColor;
        private float _levelAge;
        private float _levelSeconds;

        private MilestoneView(Vector2 hero, Vector2 corner, int sortingOrder, Transform scene, TextMeshProUGUI level)
        {
            _hero = hero;
            _corner = corner;
            _level = level;
            _levelColor = level.color;
            _small = new SheetPlayer("Milestone small", ArtScale, sortingOrder, scene);
            _medium = new SheetPlayer("Milestone medium", ArtScale, sortingOrder, scene);
            _large = new SheetPlayer("Milestone large", ArtScale, LargeSortingOrder, scene);
            _level.gameObject.SetActive(false);
        }

        public static MilestoneView Create(Vector2 hero, Vector2 corner, int sortingOrder, Transform scene, TextMeshProUGUI level) =>
            new MilestoneView(hero, corner, sortingOrder, scene, level);

        public bool CoversStrip => _large.Playing;

        public void Show(MilestoneCue cue)
        {
            var clip = MilestoneArt.Clip(cue.Kind, cue.Variant);
            switch (cue.Size)
            {
                case MilestoneSize.Small:
                    _small.Play(clip, _corner, Color.white);
                    break;
                case MilestoneSize.Large:
                    _large.Play(clip, _hero, Color.white);
                    break;
                default:
                    _medium.Play(clip, _hero, Color.white);
                    break;
            }
            if (cue.Kind == MilestoneKind.LevelUp)
                ShowLevel(cue);
        }

        public void Animate(float delta)
        {
            _small.Animate(delta);
            _medium.Animate(delta);
            _large.Animate(delta);
            if (_levelSeconds <= 0f)
                return;
            _levelAge += delta;
            if (_levelAge >= _levelSeconds)
            {
                _levelSeconds = 0f;
                _level.gameObject.SetActive(false);
                return;
            }
            var alpha = Mathf.Clamp01(_levelAge / LabelRiseSeconds) * Mathf.Clamp01((_levelSeconds - _levelAge) / LabelFadeSeconds);
            _level.color = new Color(_levelColor.r, _levelColor.g, _levelColor.b, alpha);
        }

        private void ShowLevel(MilestoneCue cue)
        {
            _level.SetText("{0}", cue.Value);
            _levelAge = 0f;
            _levelSeconds = (float)cue.Seconds;
            _level.color = new Color(_levelColor.r, _levelColor.g, _levelColor.b, 0f);
            _level.gameObject.SetActive(true);
        }
    }
}
