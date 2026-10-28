namespace Game.Core.Diagnostics
{
    public sealed class NoErrorReportSink : IErrorReportSink
    {
        public static readonly NoErrorReportSink Instance = new NoErrorReportSink();

        public bool TrySend(string reportJson) => false;
    }
}
