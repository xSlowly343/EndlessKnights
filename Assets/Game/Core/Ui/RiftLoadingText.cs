using System.Collections.Generic;
using Game.Core.Content.Rift;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class RiftLoadingText
    {
        public static IReadOnlyList<string> Lines(GameLoop loop)
        {
            var lines = new List<string>();
            var modifiers = loop.Rift.WindowModifiers;
            for (var i = 0; i < modifiers.Count; i++)
                lines.Add(Line(modifiers[i]));
            if (loop.State.Rift.WindowSet.Length == 0 || !loop.Data.Items.TryGetSet(loop.State.Rift.WindowSet, out var set))
                return lines;
            var name = Texts.Pick(set.NameRu, set.NameEn);
            lines.Add(Texts.Format("rift.loading.set", name));
            if (set.RiftOnly)
                lines.Add(Texts.Get("rift.key." + set.Id));
            var keyed = loop.Rift.KeyedModifier;
            if (keyed != null && loop.Rift.KeyHeld)
                lines.Add(Texts.Format("rift.loading.key", name, Texts.Get("rift.mod." + keyed.Id)));
            return lines;
        }

        public static string Line(RiftModifier modifier)
        {
            var name = Texts.Get("rift.mod." + modifier.Id);
            if (modifier.UnlessRoles.Count == 0)
                return name;
            var roles = new string[modifier.UnlessRoles.Count];
            for (var i = 0; i < roles.Length; i++)
                roles[i] = Texts.Get("party.role." + modifier.UnlessRoles[i]);
            return Texts.Format("rift.loading.lifted", name, string.Join(", ", roles));
        }

        public static string Gift(string id) => Texts.Get("rift.gift." + id);
    }
}
