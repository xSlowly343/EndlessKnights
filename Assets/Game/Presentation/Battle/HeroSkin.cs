namespace Game.Presentation.Battle
{
    public sealed class HeroSkin
    {
        public HeroSkin(HeroClip idle, HeroClip attack)
        {
            Idle = idle;
            Attack = attack;
        }

        public HeroClip Idle { get; }
        public HeroClip Attack { get; }
    }
}
