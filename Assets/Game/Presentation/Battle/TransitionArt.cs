using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Ui;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public enum TransitionSheet
    {
        PortalOpen,
        PortalIdle,
        PortalClose,
        StepIn,
        Arrive,
        AllyOut,
        AllyIn,
        Loading
    }

    public static class TransitionArt
    {
        private const string Folder = "Transitions/";
        private const string ListFile = "transitions";

        private static readonly string[] SheetKeys = { "portalOpen", "portalIdle", "portalClose", "stepIn", "arrive", "allyOut", "allyIn", "loading" };
        private static readonly Dictionary<int, HeroClip?> Clips = new Dictionary<int, HeroClip?>();
        private static readonly Dictionary<StripPlace, Color> Veils = new Dictionary<StripPlace, Color>();
        private static ContentNode? _list;
        private static bool _read;

        public static HeroClip? Clip(StripPlace place, TransitionSheet sheet)
        {
            var key = (int)place * SheetKeys.Length + (int)sheet;
            if (!Clips.TryGetValue(key, out var clip))
                Clips[key] = clip = Load(place, sheet);
            return clip;
        }

        public static HeroClip? Clip(StripPlace place, PortalPart part) => part switch
        {
            PortalPart.Open => Clip(place, TransitionSheet.PortalOpen),
            PortalPart.Idle => Clip(place, TransitionSheet.PortalIdle),
            PortalPart.Close => Clip(place, TransitionSheet.PortalClose),
            _ => null
        };

        public static Color Veil(StripPlace place)
        {
            if (Veils.TryGetValue(place, out var color))
                return color;
            var node = Place(place);
            color = node.HasValue && node.Value.HasField("veil") && ColorUtility.TryParseHtmlString(node.Value.Field("veil").AsString(), out var parsed) ? parsed : Color.black;
            Veils[place] = color;
            return color;
        }

        private static HeroClip? Load(StripPlace place, TransitionSheet sheet)
        {
            var node = Place(place);
            var key = SheetKeys[(int)sheet];
            if (!node.HasValue || !node.Value.HasField(key))
                return null;
            var entry = node.Value.Field(key);
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

        private static ContentNode? Place(StripPlace place)
        {
            var list = List();
            if (!list.HasValue || !list.Value.HasField("places"))
                return null;
            var places = list.Value.Field("places");
            var key = StripSceneRules.KeyOf(place);
            return places.HasField(key) ? places.Field(key) : (ContentNode?)null;
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
