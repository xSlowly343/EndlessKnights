using Game.Core.Content;
using Game.Core.Ui;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class MilestoneArt
    {
        private const string Folder = "Milestones/";
        private const string ListFile = "milestones";

        private static readonly string[] Keys =
        {
            "levelUp", "prestige", "artifactOpen", "artifactAwaken", "artifactEnchant", "riftStep", "featureOpen", "giftOffer",
            "giftChosen", "gate", "achievement", "collection", "campTier", "quest", "trial"
        };

        private static HeroClip?[][]? _clips;

        public static HeroClip? Clip(MilestoneKind kind, int variant)
        {
            if (_clips == null)
                ReadAll();
            var variants = _clips![(int)kind];
            return variants[Mathf.Clamp(variant, 0, variants.Length - 1)];
        }

        private static bool HasVariants(MilestoneKind kind) =>
            kind == MilestoneKind.ArtifactUnlocked || kind == MilestoneKind.ArtifactAwakened || kind == MilestoneKind.ArtifactEnchanted;

        private static void ReadAll()
        {
            _clips = new HeroClip?[Keys.Length][];
            var asset = Resources.Load<TextAsset>(Folder + ListFile);
            var sheets = asset == null ? (ContentNode?)null : ContentNode.Root(asset.text, ListFile + ".json").Field("sheets");
            for (var kind = 0; kind < Keys.Length; kind++)
            {
                var count = HasVariants((MilestoneKind)kind) ? MilestoneShow.ArtifactGroups : 1;
                _clips[kind] = new HeroClip?[count];
                for (var variant = 0; variant < count; variant++)
                {
                    var key = count > 1 ? Keys[kind] + variant : Keys[kind];
                    _clips[kind][variant] = sheets.HasValue && sheets.Value.HasField(key) ? Read(sheets.Value.Field(key)) : null;
                }
            }
        }

        private static HeroClip? Read(ContentNode entry)
        {
            var texture = Resources.Load<Texture2D>(Folder + entry.Field("file").AsString());
            if (texture == null)
                return null;
            texture.filterMode = FilterMode.Point;
            var frames = entry.Field("frames").AsInt(1);
            var pivot = entry.Field("pivot").Items();
            return HeroModel.Clip(texture, entry.Field("width").AsInt(1), entry.Field("height").AsInt(1), frames,
                new Vector2((float)pivot[0].AsNumber(), (float)pivot[1].AsNumber()), HeroModel.Repeat(1f / entry.Field("fps").AsInt(1), frames), false);
        }
    }
}
