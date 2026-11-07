using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class CampModuleLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Building { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Made { get; internal set; }
        public bool CanCraft { get; internal set; }
        public int Socket { get; internal set; } = -1;
        public string Status { get; internal set; } = string.Empty;
    }

    public static class CampModuleView
    {
        public static List<CampModuleLine> Lines(CampModuleSystem modules, string building)
        {
            var lines = new List<CampModuleLine>();
            foreach (var def in modules.Rules.Modules)
            {
                if (def.Building != building)
                    continue;
                var check = modules.CheckCraft(def.Id);
                var socket = modules.SocketOf(def.Id);
                lines.Add(new CampModuleLine
                {
                    Id = def.Id,
                    Building = def.Building,
                    Name = Texts.Pick(def.NameRu, def.NameEn),
                    Made = modules.Made(def.Id),
                    CanCraft = check == CampModuleResult.Done,
                    Socket = socket,
                    Status = Status(check, socket)
                });
            }
            return lines;
        }

        public static string Sockets(CampModuleSystem modules, string building)
        {
            var used = 0;
            var total = modules.Sockets(building);
            for (var i = 0; i < total; i++)
            {
                if (modules.InSocket(building, i).Length > 0)
                    used++;
            }
            return Texts.Format("campModule.sockets", used, total);
        }

        private static string Status(CampModuleResult check, int socket) => check switch
        {
            CampModuleResult.AlreadyMade => Texts.Get(socket >= 0 ? "campModule.installed" : "campModule.made"),
            CampModuleResult.ForgeClosed => Texts.Get("campModule.forgeClosed"),
            CampModuleResult.NoBlueprint => Texts.Get("campModule.noBlueprint"),
            CampModuleResult.NotEnough => Texts.Get("campModule.notEnough"),
            _ => Texts.Get("campModule.ready")
        };
    }
}
