using Game.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    public static class ProjectConfigurator
    {
        public const string CompanyName = "EndlessKnights";
        public const string ProductName = "Endless Knights";
        private const string AppIconPath = "Assets/Game/Art/Icons/AppIcon.png";

        [MenuItem("Game/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = GameStartup.StripWidth;
            PlayerSettings.defaultScreenHeight = GameStartup.StripHeight;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            ApplyAppIcon();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
                throw new BuildFailedException($"App icon not found at {AppIconPath}");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }
    }
}
