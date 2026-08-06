#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class EndangeredARIosBuilder
{
    private const string OutputPathArgument = "-iosOutputPath";

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

        ConfigurePortraitPlayerSettings();

        var outputPath = ResolveOutputPath();
        Directory.CreateDirectory(outputPath);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.Development
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"iOS build failed with result {report.summary.result} and {report.summary.totalErrors} error(s).");
        }

        Debug.Log($"EndangeredAR iOS Xcode project built at: {outputPath}");
    }

    private static void ConfigurePortraitPlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
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

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "iOS"));
    }
}
#endif
