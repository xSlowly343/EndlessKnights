using System.Globalization;

namespace Game.Core.Text
{
    public static class Texts
    {
        public static StringTable Table { get; private set; } = StringTable.Empty;
        public static string Language { get; private set; } = StringTable.DefaultLanguage;

        public static void Use(StringTable table)
        {
            Table = table;
            if (!table.Supports(Language))
                Language = StringTable.DefaultLanguage;
        }

        public static bool UseLanguage(string language)
        {
            if (!Table.Supports(language))
                return false;
            Language = language;
            return true;
        }

        public static string Get(string key)
        {
            if (Table.TryGet(Language, key, out var text) || Table.TryGet(StringTable.DefaultLanguage, key, out text))
                return text;
            return key;
        }

        public static bool Has(string key) => Table.TryGet(StringTable.DefaultLanguage, key, out _);

        public static string Format(string key, object arg0) => string.Format(CultureInfo.InvariantCulture, Get(key), arg0);

        public static string Format(string key, object arg0, object arg1) => string.Format(CultureInfo.InvariantCulture, Get(key), arg0, arg1);

        public static string Pick(string ru, string en) => Language == StringTable.DefaultLanguage ? ru : en;
    }
}
