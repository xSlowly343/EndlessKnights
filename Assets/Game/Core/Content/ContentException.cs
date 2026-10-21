using System;

namespace Game.Core.Content
{
    public sealed class ContentException : Exception
    {
        public ContentException(string fileName, string fieldPath, string problem)
            : base(Describe(fileName, fieldPath, problem))
        {
            FileName = fileName;
            FieldPath = fieldPath;
            Problem = problem;
        }

        public string FileName { get; }
        public string FieldPath { get; }
        public string Problem { get; }

        private static string Describe(string fileName, string fieldPath, string problem) =>
            string.IsNullOrEmpty(fieldPath) ? $"{fileName}: {problem}" : $"{fileName}: {fieldPath}: {problem}";
    }
}
