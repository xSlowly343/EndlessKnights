namespace Game.Core.Sim
{
    public sealed class AlchemistState
    {
        public const int FlaskSize = 3;

        public int ReagentHits { get; set; }
        public int Reagent { get; set; }
        public int[] Flask { get; } = new int[FlaskSize];
        public int InFlask { get; set; }
        public long Mixes { get; set; }
        public int LastRecipe { get; set; } = -1;
        public double MixDamageTaken { get; set; }
        public int MixTicks { get; set; }
        public double MixCrit { get; set; }
        public int MixCritTicks { get; set; }
        public double MixGold { get; set; }
        public int MixGoldTicks { get; set; }
        public int CallTicks { get; set; }
        public int LayersLeft { get; set; } = -1;
        public double LayerProgress { get; set; }
        public int CarryLayers { get; set; }
        public long LayersDissolved { get; set; }
        public int ElixirHits { get; set; }
        public int ElixirColor { get; set; }
        public long ElixirsSent { get; set; }
        public int MixResendTicks { get; set; }
        public int SolventResendTicks { get; set; }
        public int SentFlask { get; set; } = -1;
        public bool SpawnPending { get; set; }

        public void OnSpawn() => SpawnPending = true;
    }
}
