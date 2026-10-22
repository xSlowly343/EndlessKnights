using System.Collections.Generic;

namespace Game.Core.Save
{
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveFile? save, string? path, IReadOnlyList<string> problems)
        {
            Save = save;
            Path = path;
            Problems = problems;
        }

        public SaveFile? Save { get; }
        public string? Path { get; }
        public IReadOnlyList<string> Problems { get; }
        public bool Found => Save != null;
    }
}
