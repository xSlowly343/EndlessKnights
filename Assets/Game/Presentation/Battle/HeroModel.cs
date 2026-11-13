using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class HeroModel
    {
        public const int AttackCancelFrame = 5;
        public const int AttackHitFrame = 3;
        public const int EffectFramesPerSecond = 30;
        public const int DefaultLook = 0;

        private const string Folder = "Heroes/Seeker/";
        private const string StrikesFile = "strikes";
        private const string IdleFile = "Seeker_Void_Idle_30x30_24f";
        private const string AttackFile = "Seeker_Void_Attack1Body_90x50_8f";
        private const string SlashFile = "Seeker_Void_Attack1Slash_90x50_8f";
        private const string GroundPath = "ground";
        private const string GroundBurst = "ground";
        private const string TiersField = "tiers";
        private const string SkillsField = "skills";
        private const string ImpactField = "impactSeconds";

        private static readonly Vector2 IdleFeet = new Vector2(15f, 28f);
        private static readonly Vector2 AttackFeet = new Vector2(16f, 38f);
        private static readonly float[] AttackSeconds = { 0.04f, 0.04f, 0.06f, 0.04f, 0.06f, 0.04f, 0.06f, 0.08f };
        private static readonly Dictionary<int, HeroSkin> Skins = new Dictionary<int, HeroSkin>();
        private static readonly Dictionary<int, StrikeArt> Strikes = new Dictionary<int, StrikeArt>();

        private static CosmeticSystem? _cosmetics;
        private static ContentNode _strikeSheets;
        private static bool _strikeSheetsRead;

        public static HeroClip Idle => Skin(default).Idle;

        public static HeroClip Attack => Skin(default).Attack;

        public static HeroClip Slash => Strike(DefaultLook).Slash;

        public static HeroClip Crescent => Strike(DefaultLook).Flight;

        public static HeroClip Burst => Strike(DefaultLook).Burst;

        public static float StrikeSeconds => Attack.StartOf(AttackHitFrame);

        public static void UseCosmetics(CosmeticSystem? cosmetics) => _cosmetics = cosmetics;

        public static SpriteRenderer Create(string name, Vector2 feet, float scale, int sortingOrder, Transform parent)
        {
            return ShapeSprites.Create(name, Idle.Frame(0), Color.white, feet, Vector2.one * scale, sortingOrder, parent);
        }

        public static HeroSkin Skin(HeroLook look)
        {
            var key = look.Cloak | (look.Eyes << 8);
            if (Skins.TryGetValue(key, out var skin))
                return skin;
            var cloak = PaintOf(look.Cloak);
            var eyes = ColorOf(CosmeticCatalog.EyesSlot, look.Eyes);
            var phases = cloak.HasValue && cloak.Value.Animated ? cloak.Value.Frames : 1;
            var idle = new HeroClip[phases];
            var attack = new HeroClip[phases];
            for (var i = 0; i < phases; i++)
            {
                var phase = (float)i / phases;
                idle[i] = Clip(Texture(IdleFile, 30, cloak, eyes, phase), 30, 30, 24, IdleFeet, Repeat(0.1f, 24), true);
                attack[i] = Clip(Texture(AttackFile, 90, cloak, eyes, phase), 90, 50, 8, AttackFeet, AttackSeconds, false);
            }
            skin = new HeroSkin(idle, attack, cloak.HasValue ? cloak.Value.Seconds : 0f);
            Skins.Add(key, skin);
            return skin;
        }

        public static StrikeArt Strike(int look)
        {
            if (Strikes.TryGetValue(look, out var art))
                return art;
            art = LoadStrike(look) ?? (look == DefaultLook ? null : Strike(DefaultLook));
            if (art == null)
                throw new System.InvalidOperationException("strikes.json has no default strike");
            Strikes.Add(look, art);
            return art;
        }

        private static StrikeArt? LoadStrike(int look)
        {
            foreach (var node in StrikeSheets().Field("strikes").Items())
            {
                if (node.Field("look").AsInt(0) != look)
                    continue;
                Color? slash = look != DefaultLook && ColorUtility.TryParseHtmlString(node.Field("slash").AsString(), out var parsed) ? parsed : (Color?)null;
                return new StrikeArt(
                    look,
                    Clip(Texture(SlashFile, 90, null, slash, 0f), 90, 50, 8, AttackFeet, AttackSeconds, false),
                    Sheet(node.Field("flight")),
                    Sheet(node.Field("burst")),
                    node.Field("path").AsString() == GroundPath,
                    (float)node.Field("arcPx").AsNumber(),
                    node.Field("burstAt").AsString() == GroundBurst,
                    Tiers(node));
            }
            return null;
        }

        public static HeroClip? SkillSheet(string id)
        {
            var node = SkillNode(id);
            return node.HasValue ? Sheet(node.Value) : null;
        }

        public static float SkillImpactSeconds(string id)
        {
            var node = SkillNode(id);
            return node.HasValue && node.Value.HasField(ImpactField) ? (float)node.Value.Field(ImpactField).AsNumber() : 0f;
        }

        private static ContentNode? SkillNode(string id)
        {
            var sheets = StrikeSheets();
            if (!sheets.HasField(SkillsField) || !sheets.Field(SkillsField).HasField(id))
                return null;
            return sheets.Field(SkillsField).Field(id);
        }

        private static HeroClip[]? Tiers(ContentNode strike)
        {
            if (!strike.HasField(TiersField))
                return null;
            var nodes = strike.Field(TiersField).Items();
            var tiers = new HeroClip[nodes.Count];
            for (var i = 0; i < tiers.Length; i++)
                tiers[i] = Sheet(nodes[i]);
            return tiers;
        }

        private static ContentNode StrikeSheets()
        {
            if (!_strikeSheetsRead)
            {
                _strikeSheets = ContentNode.Root(Resources.Load<TextAsset>(Folder + StrikesFile).text, StrikesFile + ".json");
                _strikeSheetsRead = true;
            }
            return _strikeSheets;
        }

        private static HeroClip Sheet(ContentNode node)
        {
            var frames = node.Field("frames").AsInt(1);
            var pivot = node.Field("pivot").Items();
            var texture = Resources.Load<Texture2D>(Folder + node.Field("file").AsString());
            texture.filterMode = FilterMode.Point;
            return Clip(texture, node.Field("width").AsInt(1), node.Field("height").AsInt(1), frames,
                new Vector2((float)pivot[0].AsNumber(), (float)pivot[1].AsNumber()), Repeat(1f / node.Field("fps").AsInt(1), frames), false);
        }

        private static Color? ColorOf(string slot, int look)
        {
            if (look == DefaultLook || _cosmetics == null)
                return null;
            var item = _cosmetics.Catalog.ByLook(slot, look);
            return item != null && ColorUtility.TryParseHtmlString(item.Color, out var color) ? color : (Color?)null;
        }

        private static CloakPaint? PaintOf(int look)
        {
            if (look == DefaultLook || _cosmetics == null)
                return null;
            var item = _cosmetics.Catalog.ByLook(CosmeticCatalog.CloakSlot, look);
            if (item == null || !ColorUtility.TryParseHtmlString(item.Color, out var first))
                return null;
            var second = ColorUtility.TryParseHtmlString(item.Color2, out var parsed) ? parsed : first;
            var third = ColorUtility.TryParseHtmlString(item.Color3, out var extra) ? extra : second;
            return new CloakPaint(first, second, third, CloakPaint.PatternOf(item.Pattern), item.Frames, (float)item.Seconds);
        }

        private static Texture2D Texture(string file, int cellWidth, CloakPaint? cloak, Color? glow, float phase)
        {
            var source = Resources.Load<Texture2D>(Folder + file);
            source.filterMode = FilterMode.Point;
            if ((!cloak.HasValue && !glow.HasValue) || !source.isReadable)
                return source;
            var pixels = source.GetPixels32();
            if (cloak.HasValue)
                HeroPalette.Recolor(pixels, source.width, cellWidth, cloak.Value, glow, phase);
            else
                HeroPalette.Recolor(pixels, null, glow);
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal static HeroClip Clip(Texture2D texture, int width, int height, int count, Vector2 feetFromTopLeft, float[] seconds, bool loops)
        {
            var frames = new Sprite[count];
            var pivot = new Vector2(feetFromTopLeft.x / width, 1f - feetFromTopLeft.y / height);
            for (var i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * width, 0f, width, height), pivot, StripSpace.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            return new HeroClip(frames, seconds, loops);
        }

        internal static float[] Repeat(float seconds, int count)
        {
            var result = new float[count];
            for (var i = 0; i < count; i++)
                result[i] = seconds;
            return result;
        }
    }
}
