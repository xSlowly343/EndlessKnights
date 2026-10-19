using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    public static class ProjectConfigurator
    {
        public const string CompanyName = "EndlessKnights";
        public const string ProductName = "Endless Knights";

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
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            AssetDatabase.SaveAssets();
        }
    }
}
