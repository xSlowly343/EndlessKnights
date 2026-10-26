using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponType
    {
        public WeaponType(string id, HeroClass heroClass, string color, string nameRu, string nameEn, IReadOnlyList<Modifier> innate, IReadOnlyList<string> starterSkills)
        {
            StarterSkills = starterSkills;
            Id = id;
            HeroClass = heroClass;
            Color = color;
            NameRu = nameRu;
            NameEn = nameEn;
            Innate = innate;
        }

        public string Id { get; }
        public HeroClass HeroClass { get; }
        public string Color { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Innate { get; }
        public IReadOnlyList<string> StarterSkills { get; }
    }
}
