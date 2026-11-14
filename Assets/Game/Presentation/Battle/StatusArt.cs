using System.Collections.Generic;
using Game.Core.Content;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public enum StatusAnchor
    {
        Feet,
        Top,
        Head,
        Eyes,
        Chest
    }

    public sealed class StatusClip
    {
        public StatusClip(HeroClip clip, StatusAnchor anchor, bool masked)
        {
            Clip = clip;
            Anchor = anchor;
            Masked = masked;
        }

        public HeroClip Clip { get; }
        public StatusAnchor Anchor { get; }
        public bool Masked { get; }
    }

    public static class StatusArt
    {
        public const float SmallScale = 3f;
        public const float LargeScale = 2f;

        private const string Folder = "Statuses/";
        private const string ListFile = "statuses";
        private const string SmallKey = "small";
        private const string LargeKey = "large";

        private static readonly Dictionary<string, StatusClip?> Small = new Dictionary<string, StatusClip?>();
        private static readonly Dictionary<string, StatusClip?> Large = new Dictionary<string, StatusClip?>();
        private static ContentNode? _list;
        private static bool _read;
        private static Sprite?[]? _icons;
        private static string[] _iconOrder = System.Array.Empty<string>();

        public static StatusClip? Clip(string key, bool large)
        {
            var clips = large ? Large : Small;
            if (!clips.TryGetValue(key, out var clip))
                clips[key] = clip = Load(key, large ? LargeKey : SmallKey);
            return clip;
        }

        public static float BodyPx(bool large)
        {
            var list = List();
            var size = large ? LargeKey : SmallKey;
            return list.HasValue && list.Value.HasField("sizes") && list.Value.Field("sizes").HasField(size)
                ? list.Value.Field("sizes").Field(size).Field("body").AsInt(1)
                : large ? 64f : 32f;
        }

        public static Sprite? Icon(string name)
        {
            ReadIcons();
            var index = System.Array.IndexOf(_iconOrder, name);
            return index < 0 ? null : _icons![index];
        }

        private static void ReadIcons()
        {
            if (_icons != null)
                return;
            _icons = System.Array.Empty<Sprite?>();
            var list = List();
            if (!list.HasValue || !list.Value.HasField("icons"))
                return;
            var entry = list.Value.Field("icons");
            var texture = Resources.Load<Texture2D>(Folder + entry.Field("file").AsString());
            if (texture == null)
                return;
            texture.filterMode = FilterMode.Point;
            var order = entry.Field("order").Items();
            var width = entry.Field("width").AsInt(1);
            var height = entry.Field("height").AsInt(1);
            _iconOrder = new string[order.Count];
            _icons = new Sprite?[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                _iconOrder[i] = order[i].AsString();
                _icons[i] = Sprite.Create(texture, new Rect(i * width, 0f, width, height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            }
        }

        private static StatusClip? Load(string key, string size)
        {
            var list = List();
            if (!list.HasValue || !list.Value.HasField("sizes") || !list.Value.Field("sizes").HasField(size))
                return null;
            var sheets = list.Value.Field("sizes").Field(size).Field("sheets");
            if (!sheets.HasField(key))
                return null;
            var entry = sheets.Field(key);
            var texture = Resources.Load<Texture2D>(Folder + entry.Field("file").AsString());
            if (texture == null)
                return null;
            texture.filterMode = FilterMode.Point;
            var frames = entry.Field("frames").AsInt(1);
            var pivot = entry.Field("pivot").Items();
            var loops = entry.HasField("loops") && entry.Field("loops").AsBool();
            var clip = HeroModel.Clip(texture, entry.Field("width").AsInt(1), entry.Field("height").AsInt(1), frames,
                new Vector2((float)pivot[0].AsNumber(), (float)pivot[1].AsNumber()), HeroModel.Repeat(1f / entry.Field("fps").AsInt(1), frames), loops);
            return new StatusClip(clip, AnchorOf(entry.Field("anchor").AsString()), entry.HasField("masked") && entry.Field("masked").AsBool());
        }

        private static StatusAnchor AnchorOf(string name) => name switch
        {
            "top" => StatusAnchor.Top,
            "head" => StatusAnchor.Head,
            "eyes" => StatusAnchor.Eyes,
            "chest" => StatusAnchor.Chest,
            _ => StatusAnchor.Feet
        };

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
