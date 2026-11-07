using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct ClanCampMaterialRow
    {
        public ClanCampMaterialRow(string item, long inBag, int points)
        {
            Item = item;
            InBag = inBag;
            Points = points;
        }

        public string Item { get; }
        public long InBag { get; }
        public int Points { get; }
        public bool CanGive => InBag > 0;
    }

    public static class ClanCampView
    {
        public static string LevelName(ClanCampRules rules, int level) =>
            level <= 0 ? Texts.Get("clanCamp.empty") : Texts.Pick(rules.Levels[level - 1].NameRu, rules.Levels[level - 1].NameEn);

        public static string Progress(ClanCampRules rules, long points)
        {
            var level = rules.LevelAt(points);
            return level >= rules.MaxLevel
                ? Texts.Format("clanCamp.done", points)
                : Texts.Format("clanCamp.progress", points, rules.Levels[level].Points, Texts.Pick(rules.Levels[level].NameRu, rules.Levels[level].NameEn));
        }

        public static List<ClanCampMaterialRow> Materials(ClanCampRules rules, CampSystem camp)
        {
            var rows = new List<ClanCampMaterialRow>(rules.Materials.Count);
            foreach (var item in rules.Materials)
                rows.Add(new ClanCampMaterialRow(item, camp.Count(item), rules.PointsOf(item)));
            return rows;
        }

        public static string Feast(ClanCampRules rules, int level, int dishes, int target, bool laid) =>
            level < rules.Feast.FromLevel ? Texts.Format("clanCamp.feast.locked", LevelName(rules, rules.Feast.FromLevel))
            : laid ? Texts.Get("clanCamp.feast.laid")
            : Texts.Format("clanCamp.feast.progress", dishes, target);

        public static string Daily(long givenToday, int cap) => Texts.Format("clanCamp.daily", givenToday, cap);
    }
}
