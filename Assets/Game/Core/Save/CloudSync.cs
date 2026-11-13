using Game.Core.Sim;

namespace Game.Core.Save
{
    public enum CloudAction
    {
        Upload,
        OfferDownload,
        Same
    }

    public readonly struct CloudMark
    {
        public CloudMark(double playSeconds, long record, long savedAtUnixMs)
        {
            PlaySeconds = playSeconds;
            Record = record;
            SavedAtUnixMs = savedAtUnixMs;
        }

        public double PlaySeconds { get; }
        public long Record { get; }
        public long SavedAtUnixMs { get; }

        public static CloudMark Of(GameState state, long savedAtUnixMs) => new CloudMark(state.TotalInput.PlaySeconds, state.RecordStage, savedAtUnixMs);
    }

    public static class CloudSync
    {
        public const double SameSeconds = 60;

        public static CloudAction Decide(CloudMark local, CloudMark? server)
        {
            if (server == null)
                return CloudAction.Upload;
            var cloud = server.Value;
            if (System.Math.Abs(cloud.PlaySeconds - local.PlaySeconds) <= SameSeconds && cloud.Record == local.Record)
                return CloudAction.Same;
            if (cloud.PlaySeconds > local.PlaySeconds + SameSeconds || (cloud.PlaySeconds >= local.PlaySeconds && cloud.Record > local.Record))
                return CloudAction.OfferDownload;
            return CloudAction.Upload;
        }
    }
}
