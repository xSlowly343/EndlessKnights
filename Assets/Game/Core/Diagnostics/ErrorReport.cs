using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    public sealed class ErrorReport
    {
        public const int CurrentSchema = 1;
        public const string ErrorKind = "error";
        public const string CrashKind = "crash";

        public int Schema { get; set; } = CurrentSchema;
        public string Kind { get; set; } = ErrorKind;
        public string GameVersion { get; set; } = string.Empty;
        public string Os { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public IReadOnlyList<string> Lines { get; set; } = new string[0];
    }
}
