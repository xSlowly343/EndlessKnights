namespace Game.Core.Sim
{
    public sealed class DreamcatcherState
    {
        public const int AlbumCapacity = 512;
        public const int BiomeCapacity = 64;

        public int[] Album { get; } = new int[AlbumCapacity];
        public bool[] Prophesied { get; } = new bool[BiomeCapacity];

        public int Nets { get; set; }
        public double NetPx { get; set; }
        public int CaughtTicks { get; set; }
        public long Catches { get; set; }
        public long Trophies { get; set; }
        public int EnemyIndex { get; set; } = -1;
        public int EnemyBiome { get; set; } = -1;
        public long Specimens { get; set; }
        public int ProphecyTicks { get; set; }
        public long Prophecies { get; set; }
        public int SeenPrestiges { get; set; }
        public int Bait { get; set; }
        public int SpiritsPending { get; set; }
        public bool SpiritAlive { get; set; }
        public long Spirits { get; set; }
        public long SpiritKills { get; set; }
        public int HornTicks { get; set; }
        public int DozeTicks { get; set; }
        public int LucidTicks { get; set; }
        public bool SpawnPending { get; set; }

        public bool Caught => CaughtTicks > 0;

        public void ClearAlbum()
        {
            System.Array.Clear(Album, 0, Album.Length);
            System.Array.Clear(Prophesied, 0, Prophesied.Length);
            Specimens = 0;
        }

        public void HalveAlbum()
        {
            Specimens = 0;
            for (var i = 0; i < Album.Length; i++)
            {
                Album[i] /= 2;
                Specimens += Album[i];
            }
            System.Array.Clear(Prophesied, 0, Prophesied.Length);
        }

        public void OnSpawn() => SpawnPending = true;
    }
}
