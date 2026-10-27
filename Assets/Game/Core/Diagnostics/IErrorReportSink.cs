namespace Game.Core.Diagnostics
{
    public interface IErrorReportSink
    {
        bool TrySend(string reportJson);
    }
}
