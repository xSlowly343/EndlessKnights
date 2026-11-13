namespace Game.Core.Sim
{
    public sealed class TownForecast
    {
        public string Fish { get; internal set; } = string.Empty;
        public string Plant { get; internal set; } = string.Empty;
        public double FishWeight { get; internal set; } = 1;
        public double PlantGrow { get; internal set; }
        public double PlantMutation { get; internal set; }

        public double WeightOf(string fish) => Fish.Length > 0 && fish == Fish ? FishWeight : 1;

        public double GrowOf(string plant) => Plant.Length > 0 && plant == Plant ? PlantGrow : 0;

        public double MutationOf(string plant, string neighbor) => Plant.Length > 0 && (plant == Plant || neighbor == Plant) ? PlantMutation : 0;

        internal void Clear()
        {
            Fish = string.Empty;
            Plant = string.Empty;
            FishWeight = 1;
            PlantGrow = 0;
            PlantMutation = 0;
        }
    }
}
