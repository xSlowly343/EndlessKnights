using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    public static class WindowsBuild
    {
        private const string OutputDirectory = "Builds/Windows";
        private const string ExecutableName = "EndlessKnights.exe";

        [MenuItem("Game/Build Windows")]
        public static void Build()
        {
            ProjectConfigurator.Apply();

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = Path.Combine(OutputDirectory, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows build finished with {summary.result}, errors: {summary.totalErrors}");
        }
    }
}
