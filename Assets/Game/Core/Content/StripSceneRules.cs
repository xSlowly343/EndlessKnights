using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Content
{
    public sealed class PortalRule
    {
        public static readonly PortalRule None = new PortalRule(0, 0, 0, 0, 0, false, 0, 0);

        public PortalRule(double open, double enter, double swap, double arrive, double exit, bool group, double minLoad, double maxLoad)
        {
            Open = open;
            Enter = enter;
            Swap = swap;
            Arrive = arrive;
            Exit = exit;
            Group = group;
            MinLoad = minLoad;
            MaxLoad = maxLoad;
        }

        public double Open { get; }
        public double Enter { get; }
        public double Swap { get; }
        public double Arrive { get; }
        public double Exit { get; }
        public bool Group { get; }
        public double MinLoad { get; }
        public double MaxLoad { get; }
        public bool Loads => MaxLoad > 0;
    }

    public sealed class StripSceneRules
    {
        public const double MaxSwitchSeconds = 5;
        public const double MaxPhaseSeconds = 5;
        public const double MaxLoadSeconds = 30;

        public static readonly StripSceneRules Instant = new StripSceneRules(0, new Dictionary<StripPlace, PortalRule>());

        private static readonly (string Key, StripPlace Place)[] Keys =
        {
            ("home", StripPlace.Home), ("rift", StripPlace.Rift), ("dungeon", StripPlace.Dungeon), ("eventFight", StripPlace.EventFight),
            ("hideout", StripPlace.Hideout), ("clanCamp", StripPlace.ClanCamp), ("town", StripPlace.Town)
        };

        private readonly IReadOnlyDictionary<StripPlace, PortalRule> _portals;

        public StripSceneRules(double switchSeconds, IReadOnlyDictionary<StripPlace, PortalRule> portals)
        {
            SwitchSeconds = switchSeconds;
            _portals = portals;
        }

        public StripSceneRules(double switchSeconds) : this(switchSeconds, new Dictionary<StripPlace, PortalRule>())
        {
        }

        public double SwitchSeconds { get; }

        public PortalRule Portal(StripPlace place) => _portals.TryGetValue(place, out var rule) ? rule : PortalRule.None;

        public static string KeyOf(StripPlace place)
        {
            foreach (var (key, candidate) in Keys)
            {
                if (candidate == place)
                    return key;
            }
            return Keys[0].Key;
        }

        public static StripSceneRules Load(ContentNode node)
        {
            var portals = new Dictionary<StripPlace, PortalRule>();
            if (node.HasField("portals"))
            {
                var list = node.Field("portals");
                foreach (var (key, place) in Keys)
                {
                    if (!list.HasField(key))
                        throw list.Error($"the portal to '{key}' is missing");
                    portals[place] = ReadPortal(list.Field(key));
                }
            }
            return new StripSceneRules(node.Field("switchSeconds").AsNumber(0, MaxSwitchSeconds), portals);
        }

        private static PortalRule ReadPortal(ContentNode node)
        {
            double Phase(string name) => node.Field(name).AsNumber(0, MaxPhaseSeconds);
            var minLoad = node.HasField("minLoad") ? node.Field("minLoad").AsNumber(0, MaxLoadSeconds) : 0;
            var maxLoad = node.HasField("maxLoad") ? node.Field("maxLoad").AsNumber(minLoad, MaxLoadSeconds) : 0;
            return new PortalRule(Phase("open"), Phase("enter"), Phase("swap"), Phase("arrive"), Phase("exit"),
                node.HasField("group") && node.Field("group").AsBool(), minLoad, maxLoad);
        }
    }
}
