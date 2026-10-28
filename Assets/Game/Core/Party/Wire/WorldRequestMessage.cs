namespace Game.Core.Party.Wire
{
    public struct WorldRequestMessage
    {
        public PartyWorld World;
        public bool Cancel;
        public long RiftId;

        public void Write(ref PartyWriter writer)
        {
            writer.Byte((byte)World);
            writer.Bool(Cancel);
            writer.Long(RiftId);
        }

        public static WorldRequestMessage Read(ref PartyReader reader) => new WorldRequestMessage
        {
            World = (PartyWorld)reader.Byte(),
            Cancel = reader.Bool(),
            RiftId = reader.Long()
        };
    }
}
