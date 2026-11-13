using System.Diagnostics;
using UnityEngine;

namespace Game.Bootstrap
{
    public static class GameRestart
    {
        public static void Now()
        {
            if (!Application.isEditor)
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                    Process.Start(path);
            }
            Application.Quit();
        }
    }
}
