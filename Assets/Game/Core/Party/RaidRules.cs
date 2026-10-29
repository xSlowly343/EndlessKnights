using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public enum RaidInput
    {
        Skill,
        Mouse,
        Keys,
        Idle
    }

    public readonly struct RaidLayerRule
    {
        public RaidLayerRule(string id, RaidInput input, double perMember)
        {
            Id = id;
            Input = input;
            PerMember = perMember;
        }

        public string Id { get; }
        public RaidInput Input { get; }
        public double PerMember { get; }
    }

    public sealed class RaidRules
    {
        public const string FileName = "raid.json";
        public const int SupportedVersion = 1;
        public const int InputKinds = 4;

        private readonly int[] _layerOfInput;

        private RaidRules(string boss, double pushSeconds, double idleSeconds, double maxSharesPerSecond, IReadOnlyList<RaidLayerRule> layers, int[] layerOfInput)
        {
            Boss = boss;
            PushSeconds = pushSeconds;
            IdleSeconds = idleSeconds;
            MaxSharesPerSecond = maxSharesPerSecond;
            Layers = layers;
            _layerOfInput = layerOfInput;
        }

        public string Boss { get; }
        public double PushSeconds { get; }
        public double IdleSeconds { get; }
        public double MaxSharesPerSecond { get; }
        public IReadOnlyList<RaidLayerRule> Layers { get; }

        public int LayerOf(RaidInput input) => _layerOfInput[(int)input];

        public static RaidRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var layers = new List<RaidLayerRule>();
            var layerOfInput = new[] { -1, -1, -1, -1 };
            var layersNode = root.Field("layers");
            foreach (var item in layersNode.Items())
            {
                var id = item.Field("id").AsString();
                var inputNode = item.Field("input");
                var input = inputNode.AsString() switch
                {
                    "skill" => RaidInput.Skill,
                    "mouse" => RaidInput.Mouse,
                    "keys" => RaidInput.Keys,
                    "idle" => RaidInput.Idle,
                    var other => throw inputNode.Error($"unknown input \"{other}\", expected skill, mouse, keys or idle")
                };
                if (layerOfInput[(int)input] >= 0)
                    throw inputNode.Error($"\"{inputNode.AsString()}\" already takes down another layer");
                if (id.Length == 0)
                    throw item.Field("id").Error("must not be empty");
                layerOfInput[(int)input] = layers.Count;
                layers.Add(new RaidLayerRule(id, input, item.Field("perMember").AsNumber(1, 1e9)));
            }
            if (layers.Count != InputKinds)
                throw layersNode.Error($"must list {InputKinds} layers, one for each input");
            var rewards = root.Field("rewards");
            rewards.Field("sealsPerMember").AsNumber(0, 100000);
            rewards.Field("killBonus").AsNumber(0, 10);
            rewards.Field("minSeals").AsInt(0, 100000);
            root.Field("spillover").AsNumber(0, 1);
            root.Field("minMembers").AsInt(1, 100);
            return new RaidRules(root.Field("boss").AsString(), root.Field("pushSeconds").AsNumber(5, 300), root.Field("idleSeconds").AsNumber(1, 60),
                root.Field("maxSharesPerSecond").AsNumber(0.01, 1000), layers, layerOfInput);
        }
    }
}
