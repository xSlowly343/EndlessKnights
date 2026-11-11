using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Presentation.Battle
{
    public static class HeroModel
    {
        public const int AttackCancelFrame = 5;
        public const int AttackHitFrame = 3;
        public const int EffectFramesPerSecond = 30;

        private const string Folder = "Heroes/Seeker/";

        private static readonly Vector2 AttackFeet = new Vector2(16f, 38f);
        private static readonly float[] AttackSeconds = { 0.04f, 0.04f, 0.06f, 0.04f, 0.06f, 0.04f, 0.06f, 0.08f };

        private static HeroClip? _idle;
        private static HeroClip? _attack;
        private static HeroClip? _slash;
        private static HeroClip? _crescent;
        private static HeroClip? _burst;

        public static HeroClip Idle => _idle ??= Load("Seeker_Void_Idle_30x30_24f", 30, 30, new Vector2(15f, 28f), Repeat(0.1f, 24), true);

        public static HeroClip Attack => _attack ??= Load("Seeker_Void_Attack1Body_90x50_8f", 90, 50, AttackFeet, AttackSeconds, false);

        public static HeroClip Slash => _slash ??= Load("Seeker_Void_Attack1Slash_90x50_8f", 90, 50, AttackFeet, AttackSeconds, false);

        public static HeroClip Crescent => _crescent ??= Load("Seeker_Void_Crescent_87x42_8f", 87, 42, new Vector2(72f, 21f), Repeat(1f / EffectFramesPerSecond, 8), false);

        public static HeroClip Burst => _burst ??= Load("Seeker_Void_Burst_80x54_17f", 80, 54, new Vector2(32f, 27f), Repeat(1f / EffectFramesPerSecond, 17), false);

        public static float StrikeSeconds => Attack.StartOf(AttackHitFrame);


        public static SpriteRenderer Create(string name, Vector2 feet, float scale, int sortingOrder, Transform parent)
        {
            return ShapeSprites.Create(name, Idle.Frame(0), Color.white, feet, Vector2.one * scale, sortingOrder, parent);
        }

        private static HeroClip Load(string file, int width, int height, Vector2 feetFromTopLeft, float[] seconds, bool loops)
        {
            var texture = Resources.Load<Texture2D>(Folder + file);
            texture.filterMode = FilterMode.Point;
            var frames = new Sprite[seconds.Length];
            var pivot = new Vector2(feetFromTopLeft.x / width, 1f - feetFromTopLeft.y / height);
            for (var i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * width, 0f, width, height), pivot, StripSpace.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            return new HeroClip(frames, seconds, loops);
        }

        private static float[] Repeat(float seconds, int count)
        {
            var result = new float[count];
            for (var i = 0; i < count; i++)
                result[i] = seconds;
            return result;
        }
    }
}
