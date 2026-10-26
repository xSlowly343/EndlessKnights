using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Weapons
{
    public sealed class WeaponBase
    {
        public WeaponBase(string id, WeaponType type, int fromBiome, string nameRu, string nameEn, IReadOnlyList<Modifier> innate)
        {
            Id = id;
            Type = type;
            FromBiome = fromBiome;
            NameRu = nameRu;
            NameEn = nameEn;
            Innate = innate;
        }

        public string Id { get; }
        public WeaponType Type { get; }
        public int FromBiome { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Innate { get; }
    }
}
