using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CompanionFork
    {
        public CompanionFork(string id, string nameRu, string nameEn, IReadOnlyList<CompanionLine> lines)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Lines = lines;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<CompanionLine> Lines { get; }
    }

    public sealed class CompanionPath
    {
        public CompanionPath(string id, string nameRu, string nameEn, IReadOnlyList<IReadOnlyList<CompanionLine>> levels, IReadOnlyList<IReadOnlyList<CompanionFork>> forks)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Levels = levels;
            Forks = forks;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<IReadOnlyList<CompanionLine>> Levels { get; }
        public IReadOnlyList<IReadOnlyList<CompanionFork>> Forks { get; }

        public CompanionFork? Fork(int index, string option)
        {
            if (index < 1 || index > Forks.Count)
                return null;
            var options = Forks[index - 1];
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Id == option)
                    return options[i];
            }
            return null;
        }
    }

    public sealed class CompanionPathRules
    {
        public IReadOnlyList<CompanionPath> Paths { get; private set; } = Array.Empty<CompanionPath>();
        public long ChangeEssence { get; private set; }
        public int FirstLevelCircle { get; private set; } = 4;

        public CompanionPath? Path(string id)
        {
            for (var i = 0; i < Paths.Count; i++)
            {
                if (Paths[i].Id == id)
                    return Paths[i];
            }
            return null;
        }

        internal static CompanionPathRules Load(ContentNode node)
        {
            var paths = new List<CompanionPath>();
            foreach (var item in node.Field("list").Items())
            {
                var levels = new List<IReadOnlyList<CompanionLine>>();
                foreach (var level in item.Field("levels").Items())
                    levels.Add(CompanionLines.Read(level));
                var forks = new List<IReadOnlyList<CompanionFork>>();
                foreach (var fork in item.Field("forks").Items())
                {
                    var options = new List<CompanionFork>();
                    foreach (var option in fork.Items())
                    {
                        var optionName = option.Field("name");
                        options.Add(new CompanionFork(option.Field("id").AsString(), optionName.Field("ru").AsString(), optionName.Field("en").AsString(), CompanionLines.Read(option.Field("lines"))));
                    }
                    forks.Add(options);
                }
                var name = item.Field("name");
                paths.Add(new CompanionPath(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), levels, forks));
            }
            return new CompanionPathRules
            {
                Paths = paths,
                ChangeEssence = node.Field("changeEssence").AsLong(0, 1_000_000),
                FirstLevelCircle = node.Field("firstLevelCircle").AsInt(1, 100)
            };
        }
    }
}
