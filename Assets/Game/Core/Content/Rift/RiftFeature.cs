namespace Game.Core.Content.Rift
{
    public enum RiftFeature
    {
        Artifacts,
        Dungeons
    }

    public static class RiftFeatures
    {
        public static readonly RiftFeature[] All = { RiftFeature.Artifacts, RiftFeature.Dungeons };

        public static string Key(RiftFeature feature) => feature switch
        {
            RiftFeature.Artifacts => "artifacts",
            _ => "dungeons"
        };

        public static bool TryParse(string key, out RiftFeature feature)
        {
            foreach (var candidate in All)
            {
                if (Key(candidate) == key)
                {
                    feature = candidate;
                    return true;
                }
            }
            feature = RiftFeature.Artifacts;
            return false;
        }
    }
}
