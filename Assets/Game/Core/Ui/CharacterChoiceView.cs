using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Tree;
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

    public sealed class PathChoiceCard
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string About { get; internal set; } = string.Empty;
        public List<RadarPoint> Radar { get; } = new List<RadarPoint>();
        public List<string> Pros { get; } = new List<string>();
        public List<string> Cons { get; } = new List<string>();
    }

    public sealed class CharacterChoiceCard
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Role { get; internal set; } = string.Empty;
        public string About { get; internal set; } = string.Empty;
        public List<RadarPoint> Radar { get; } = new List<RadarPoint>();
        public List<string> Partners { get; } = new List<string>();
        public List<PathChoiceCard> Paths { get; } = new List<PathChoiceCard>();
    }

    public static class CharacterChoiceView
    {
        public const int StrongValue = 4;

        public static CharacterChoiceCard Build(GameData data, CharacterDef character)
        {
            var card = new CharacterChoiceCard
            {
                Id = character.Id,
                Name = Texts.Get("class." + character.Id),
                Role = Texts.Get("party.role." + character.Role),
                About = Texts.Pick(character.AboutRu, character.AboutEn)
            };
            Fill(card.Radar, data.Characters.AxisList, character.Radar);
            foreach (var partner in character.Partners)
                card.Partners.Add(Texts.Get("class." + partner));
            foreach (var path in data.Ascendancy.SubclassesOf(character.Id))
                card.Paths.Add(BuildPath(data, character, path));
            return card;
        }

        public static List<CharacterChoiceCard> All(GameData data)
        {
            var cards = new List<CharacterChoiceCard>();
            foreach (var character in data.Characters.All)
                cards.Add(Build(data, character));
            return cards;
        }

        public static List<CharacterChoiceCard> StrongIn(GameData data, int axis)
        {
            var cards = new List<CharacterChoiceCard>();
            foreach (var character in data.Characters.All)
            {
                if (Best(character, axis) >= StrongValue)
                    cards.Add(Build(data, character));
            }
            return cards;
        }

        private static int Best(CharacterDef character, int axis)
        {
            var best = character.Radar[axis];
            foreach (var path in character.Paths)
                best = System.Math.Max(best, path.Radar[axis]);
            return best;
        }

        private static PathChoiceCard BuildPath(GameData data, CharacterDef character, SubclassDef path)
        {
            var card = new PathChoiceCard { Id = path.Id, Name = SubclassView.Name(path), About = SubclassView.Blurb(path) };
            if (!character.TryGetPath(path.Id, out var details))
                return card;
            Fill(card.Radar, data.Characters.AxisList, details.Radar);
            var russian = Texts.Language == StringTable.DefaultLanguage;
            card.Pros.AddRange(russian ? details.ProsRu : details.ProsEn);
            card.Cons.AddRange(russian ? details.ConsRu : details.ConsEn);
            return card;
        }

        private static void Fill(List<RadarPoint> into, IReadOnlyList<CharacterAxis> axes, IReadOnlyList<int> values)
        {
            for (var i = 0; i < axes.Count; i++)
                into.Add(new RadarPoint(Texts.Pick(axes[i].NameRu, axes[i].NameEn), values[i]));
        }
    }
}
