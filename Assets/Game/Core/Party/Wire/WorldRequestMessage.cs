namespace Game.Core.Party.Wire
{
    public struct WorldRequestMessage
    {
        public PartyWorld World;
        public bool Cancel;
        public long WindowId;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)World);
            writer.Bool(Cancel);
            writer.Long(WindowId);
        }

        public static WorldRequestMessage Read(ref PartyReader reader) => new WorldRequestMessage
        {
            World = (PartyWorld)reader.Byte(),
            Cancel = reader.Bool(),
            WindowId = reader.Long()
        };
    }
}
