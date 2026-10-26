using Game.Platform.Windows.Input;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class InputShutdownDriver : MonoBehaviour
    {
        private RawInputService _rawInput = null!;

        public static void Attach(GameObject host, RawInputService rawInput)
        {
            var driver = host.AddComponent<InputShutdownDriver>();
            driver._rawInput = rawInput;
        }

        private void OnApplicationQuit() => _rawInput.Dispose();
    }
}
