using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct RadarPoint
    {
        public readonly string Axis;
        public readonly int Value;

        public RadarPoint(string axis, int value)
        {
            Axis = axis;
            Value = value;
        }
    }

    public sealed class CharacterChoiceCard
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Role { get; internal set; } = string.Empty;
        public string About { get; internal set; } = string.Empty;
        public List<RadarPoint> Radar { get; } = new List<RadarPoint>();
        public List<string> Partners { get; } = new List<string>();
        public List<string> Paths { get; } = new List<string>();
    }

    public static class CharacterChoiceView
    {
        public static CharacterChoiceCard Build(GameData data, CharacterDef character)
        {
            var card = new CharacterChoiceCard
            {
                Id = character.Id,
                Name = Texts.Get("class." + character.Id),
                Role = Texts.Get("party.role." + character.Role),
                About = Texts.Pick(character.AboutRu, character.AboutEn)
            };
            var axes = data.Characters.AxisList;
            for (var i = 0; i < axes.Count; i++)
                card.Radar.Add(new RadarPoint(Texts.Pick(axes[i].NameRu, axes[i].NameEn), character.Radar[i]));
            foreach (var partner in character.Partners)
                card.Partners.Add(Texts.Get("class." + partner));
            foreach (var path in data.Ascendancy.SubclassesOf(character.Id))
                card.Paths.Add(SubclassView.Name(path));
            return card;
        }

        public static List<CharacterChoiceCard> All(GameData data)
        {
            var cards = new List<CharacterChoiceCard>();
            foreach (var character in data.Characters.All)
                cards.Add(Build(data, character));
            return cards;
        }
    }
}
