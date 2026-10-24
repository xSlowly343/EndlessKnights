using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Ui
{
    public sealed class UiButton
    {
        public UiButton(Image back, TextMeshProUGUI? label)
        {
            Back = back;
            Label = label;
        }

        public Image Back { get; }
        public TextMeshProUGUI? Label { get; }
        public RectTransform Rect => Back.rectTransform;

        public string Text
        {
            set
            {
                if (Label != null)
                    Label.text = value;
            }
        }

        public bool Contains(Vector2 worldPoint) => UiElements.Contains(Rect, worldPoint);

        public void Show(bool shown) => Back.gameObject.SetActive(shown);
    }
}
