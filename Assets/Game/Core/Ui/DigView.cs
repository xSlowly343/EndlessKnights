using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class DigView
    {
        public static string Status(DigSystem digs)
        {
            if (!digs.Open)
                return Texts.Get("dig.closed");
            var site = digs.Rules.Site(digs.CurrentSite);
            if (site == null)
                return Texts.Get("dig.empty");
            var name = Texts.Pick(site.NameRu, site.NameEn);
            if (digs.State.Phase == DigPhase.Exposed)
                return Texts.Format("dig.exposed", name);
            var brushed = digs.State.Phase == DigPhase.Brushing ? digs.State.Brushed / digs.BrushNeeded : 0;
            var quality = digs.State.Phase == DigPhase.Brushing ? digs.State.Quality : 1;
            return Texts.Format("dig.brushing", name, (int)System.Math.Round(System.Math.Min(1, brushed) * 100), (int)System.Math.Round(quality * 100));
        }

        public static List<string> Skeletons(DigSystem digs)
        {
            var lines = new List<string>();
            foreach (var site in digs.Rules.Sites)
            {
                var name = Texts.Pick(site.NameRu, site.NameEn);
                lines.Add(digs.State.Skeletons.Contains(site.Biome)
                    ? Texts.Format("dig.skeleton", name)
                    : Texts.Format("dig.bones", name, digs.Bones(site.Biome), digs.Rules.SkeletonBones));
            }
            return lines;
        }
    }
}
