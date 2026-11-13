using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class StripSceneText
    {
        public static string Place(StripPlace place) => Texts.Get(Key(place));

        public static string Title(GameLoop loop)
        {
            var scene = loop.Scene;
            if (scene.Place == StripPlace.Dungeon && loop.State.Dungeons.Active is { } key)
                return Texts.Format("strip.place.dungeonTier", key.Tier);
            if (!scene.Peaceful)
                return Place(scene.Place);
            return Texts.Format("strip.place.activity", Place(scene.Place), ActivityName(loop.Peaceful));
        }

        private static string ActivityName(PeacefulSystem peaceful)
        {
            var activity = peaceful.Rules.Activities[peaceful.Rules.IndexOf(peaceful.Activity)];
            return Texts.Pick(activity.NameRu, activity.NameEn);
        }

        private static string Key(StripPlace place) => place switch
        {
            StripPlace.Rift => "strip.place.rift",
            StripPlace.Dungeon => "strip.place.dungeon",
            StripPlace.EventFight => "strip.place.eventFight",
            StripPlace.Hideout => "strip.place.hideout",
            StripPlace.ClanCamp => "strip.place.clanCamp",
            StripPlace.Town => "strip.place.town",
            _ => "strip.place.home"
        };
    }
}
