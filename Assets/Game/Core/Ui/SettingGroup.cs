using System.Collections.Generic;

namespace Game.Core.Ui
{
    public sealed class SettingGroup
    {
        public SettingGroup(string key, IReadOnlyList<SettingId> rows)
        {
            Key = key;
            Rows = rows;
        }

        public string Key { get; }
        public IReadOnlyList<SettingId> Rows { get; }
    }
}
