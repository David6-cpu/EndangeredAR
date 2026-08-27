#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EndangeredAR.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class EndangeredARIosBuilder
{
    public const string MinimumIosVersion = OnDeviceLLMIosPostprocessor.MinimumIosVersion;
    private const string OutputPathArgument = "-iosOutputPath";
    private const string DevelopmentBuildArgument = "-iosDevelopmentBuild";
    private const string DevelopmentRemoteArgument = "-iosDevelopmentRemote";

    [MenuItem("Endangered AR/Build iOS Xcode Project")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes are configured in Build Settings.");
        }

        var modelPath = OnDeviceLLMBuildInputs.ResolveAndValidateModel();
        OnDeviceLLMBuildInputs.ResolveAndValidateFramework();
        var outputPath = ResolveOutputPath();
        var developmentBuild = HasArgument(DevelopmentBuildArgument);
        var developmentRemote = developmentBuild && HasArgument(DevelopmentRemoteArgument);
        var previousOnDeviceFlag = Environment.GetEnvironmentVariable(
            OnDeviceLLMIosPostprocessor.OnDeviceBuildFlag);
        var previousNetworkFlag = Environment.GetEnvironmentVariable(
            EndangeredARIosPostprocessor.DevelopmentRemoteNetworkFlag);
        IDisposable stagingScope = null;

        try
        {
            ConfigurePortraitPlayerSettings();
            stagingScope = OnDeviceLLMModelStager.Stage(Application.dataPath, modelPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Environment.SetEnvironmentVariable(OnDeviceLLMIosPostprocessor.OnDeviceBuildFlag, "1");
            Environment.SetEnvironmentVariable(
                EndangeredARIosPostprocessor.DevelopmentRemoteNetworkFlag,
                developmentRemote ? "1" : null);
            Directory.CreateDirectory(outputPath);

            var options = developmentBuild
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = options
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"iOS build failed with result {report.summary.result} and {report.summary.totalErrors} error(s).");
            }

            Debug.Log("EndangeredAR on-device iOS Xcode project built successfully.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                OnDeviceLLMIosPostprocessor.OnDeviceBuildFlag,
                previousOnDeviceFlag);
            Environment.SetEnvironmentVariable(
                EndangeredARIosPostprocessor.DevelopmentRemoteNetworkFlag,
                previousNetworkFlag);
            stagingScope?.Dispose();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void ConfigurePortraitPlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = MinimumIosVersion;
    }

    private static string ResolveOutputPath()
    {
        var arguments = Environment.GetCommandLineArgs();
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], OutputPathArgument, StringComparison.Ordinal))
            {
                return Path.GetFullPath(arguments[index + 1]);
            }
        }

        throw new BuildFailedException(
            $"Provide an external iOS build directory with {OutputPathArgument}.");
    }

    private static bool HasArgument(string expected)
    {
        return Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, expected, StringComparison.Ordinal));
    }
}
#endif
