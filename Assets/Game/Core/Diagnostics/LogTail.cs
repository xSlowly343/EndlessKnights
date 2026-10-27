using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    public static class LogTail
    {
        public static IReadOnlyList<string> Last(string? text, int max)
        {
            if (string.IsNullOrEmpty(text) || max <= 0)
                return new string[0];
            var lines = text!.Split('\n');
            var end = lines.Length;
            while (end > 0 && lines[end - 1].Trim().Length == 0)
                end--;
            var start = System.Math.Max(0, end - max);
            var result = new string[end - start];
            for (var i = start; i < end; i++)
                result[i - start] = lines[i].TrimEnd('\r');
            return result;
        }
    }
}
