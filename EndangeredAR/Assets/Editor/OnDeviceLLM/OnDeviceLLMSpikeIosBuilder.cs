#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EndangeredAR.Build
{
    public static class OnDeviceLLMSpikeIosBuilder
    {
        public const string OutputPathEnvironmentVariable = "ENDANGERED_AR_IOS_SPIKE_OUTPUT_PATH";

        [MenuItem("Endangered AR/Development/Build On-Device LLM Spike")]
        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for the on-device Spike.");
            }

            var modelPath = OnDeviceLLMBuildInputs.ResolveAndValidateModel();
            OnDeviceLLMBuildInputs.ResolveAndValidateFramework();
            var outputPath = ResolveOutputPath();
            var previousSpikeFlag = Environment.GetEnvironmentVariable(OnDeviceLLMIosPostprocessor.SpikeBuildFlag);
            IDisposable stagingScope = null;

            try
            {
                stagingScope = OnDeviceLLMModelStager.Stage(Application.dataPath, modelPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Environment.SetEnvironmentVariable(OnDeviceLLMIosPostprocessor.SpikeBuildFlag, "1");
                Directory.CreateDirectory(outputPath);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"On-device LLM Spike build failed with {report.summary.totalErrors} error(s).");
                }

                Debug.Log("On-device LLM Spike iOS project built successfully.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    OnDeviceLLMIosPostprocessor.SpikeBuildFlag,
                    previousSpikeFlag);
                stagingScope?.Dispose();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static string ResolveOutputPath()
        {
            var configured = Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new BuildFailedException(
                    $"Set {OutputPathEnvironmentVariable} to an external empty build directory.");
            }

            var outputPath = Path.GetFullPath(configured);
            var projectRoot = EnsureTrailingSeparator(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            if (EnsureTrailingSeparator(outputPath).StartsWith(projectRoot, StringComparison.Ordinal))
            {
                throw new BuildFailedException("On-device Spike build output must remain outside the repository.");
            }

            if (Directory.Exists(outputPath) && Directory.GetFileSystemEntries(outputPath).Length > 0)
            {
                throw new BuildFailedException("On-device Spike build output directory must be empty.");
            }

            return outputPath;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
