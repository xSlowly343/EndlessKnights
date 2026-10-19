using UnityEngine;

namespace Game.Presentation.Strip
{
    public static class TransparentCamera
    {
        public static void Apply(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
        }
    }
}
