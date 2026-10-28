using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public enum PartyRiftAction
    {
        None,
        Enter,
        Home,
        Cancel
    }

    public static class PartyRiftButton
    {
        public static PartyRiftAction Of(PartyClient? client, long nowUnixMs)
        {
            if (client == null || !client.InParty)
                return PartyRiftAction.None;
            if (client.WorldSwitching)
                return PartyRiftAction.Cancel;
            if (client.World == PartyWorld.Rift)
                return PartyRiftAction.Home;
            return client.RiftOpen(nowUnixMs) ? PartyRiftAction.Enter : PartyRiftAction.None;
        }

        public static string Label(PartyRiftAction action, PartyClient? client) => action switch
        {
            PartyRiftAction.Enter => Texts.Get("party.rift.enter"),
            PartyRiftAction.Home => Texts.Get("party.rift.home"),
            PartyRiftAction.Cancel => Texts.Format(client!.PendingWorld == PartyWorld.Rift ? "party.rift.leaving" : "party.rift.returning", Seconds(client)),
            _ => string.Empty
        };

        public static bool Perform(PartyRiftAction action, PartyClient? client, long nowUnixMs) => client != null && action switch
        {
            PartyRiftAction.Enter => client.ProposeWorld(PartyWorld.Rift, nowUnixMs),
            PartyRiftAction.Home => client.ProposeWorld(PartyWorld.Home, nowUnixMs),
            PartyRiftAction.Cancel => client.CancelWorldSwitch(),
            _ => false
        };

        private static int Seconds(PartyClient client) => (int)System.Math.Ceiling(client.WorldSwitchSecondsLeft - 1e-9);
    }
}
