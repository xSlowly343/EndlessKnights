using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Items;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class LootArt
    {
        private const string Folder = "Loot/";
        private const string ListFile = "loot";
        private const float DefaultBeamSeconds = 1.6f;

        private static readonly Dictionary<string, HeroClip?> Sheets = new Dictionary<string, HeroClip?>();
        private static HeroClip?[]? _beams;
        private static float[] _beamSeconds = System.Array.Empty<float>();
        private static ContentNode? _list;
        private static bool _read;

        public static HeroClip? Beam(ItemRarity rarity)
        {
            ReadBeams();
            var index = (int)rarity;
            return index < _beams!.Length ? _beams[index] : null;
        }

        public static float BeamSeconds(ItemRarity rarity)
        {
            ReadBeams();
            var index = (int)rarity;
            return index < _beamSeconds.Length ? _beamSeconds[index] : DefaultBeamSeconds;
        }

        public static HeroClip? Sheet(string key)
        {
            if (Sheets.TryGetValue(key, out var clip))
                return clip;
            var list = List();
            clip = list.HasValue && list.Value.HasField("sheets") && list.Value.Field("sheets").HasField(key) ? Read(list.Value.Field("sheets").Field(key)) : null;
            Sheets[key] = clip;
            return clip;
        }

        private static void ReadBeams()
        {
            if (_beams != null)
                return;
            _beams = System.Array.Empty<HeroClip?>();
            var list = List();
            if (!list.HasValue || !list.Value.HasField("beams"))
                return;
            var entries = list.Value.Field("beams").Items();
            _beams = new HeroClip?[entries.Count];
            _beamSeconds = new float[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                _beams[i] = Read(entries[i]);
                _beamSeconds[i] = entries[i].HasField("seconds") ? (float)entries[i].Field("seconds").AsNumber() : DefaultBeamSeconds;
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
            var loops = entry.HasField("loops") && entry.Field("loops").AsBool();
            return HeroModel.Clip(texture, entry.Field("width").AsInt(1), entry.Field("height").AsInt(1), frames,
                new Vector2((float)pivot[0].AsNumber(), (float)pivot[1].AsNumber()), HeroModel.Repeat(1f / entry.Field("fps").AsInt(1), frames), loops);
        }

        private static ContentNode? List()
        {
            if (_read)
                return _list;
            _read = true;
            var asset = Resources.Load<TextAsset>(Folder + ListFile);
            _list = asset == null ? (ContentNode?)null : ContentNode.Root(asset.text, ListFile + ".json");
            return _list;
        }
    }
}
