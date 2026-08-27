#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

namespace EndangeredAR.Build
{
    public static class OnDeviceLLMIosPostprocessor
    {
        public const string SpikeBuildFlag = "ENDANGERED_AR_ON_DEVICE_SPIKE";
        public const string MinimumIosVersion = "16.4";
        private const string EnabledValue = "1";
        private const string FrameworkDirectoryName = "llama.xcframework";

        [PostProcessBuild(200)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS ||
                !string.Equals(
                    Environment.GetEnvironmentVariable(SpikeBuildFlag),
                    EnabledValue,
                    StringComparison.Ordinal))
            {
                return;
            }

            var sourceFrameworkPath = OnDeviceLLMBuildInputs.ResolveAndValidateFramework();
            var destinationRoot = Path.Combine(pathToBuiltProject, "Frameworks");
            var destinationFrameworkPath = Path.Combine(destinationRoot, FrameworkDirectoryName);
            if (Directory.Exists(destinationFrameworkPath))
            {
                throw new InvalidOperationException("Generated iOS project already contains llama.xcframework.");
            }

            Directory.CreateDirectory(destinationRoot);
            CopyDirectory(sourceFrameworkPath, destinationFrameworkPath);
            AddFrameworkToProject(pathToBuiltProject);
        }

        public static string ValidateFramework(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("llama.cpp XCFramework input is not configured.");
            }

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath) ||
                !string.Equals(Path.GetFileName(fullPath), FrameworkDirectoryName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("llama.cpp XCFramework input is missing or has the wrong name.");
            }

            if (!File.Exists(Path.Combine(fullPath, "Info.plist")))
            {
                throw new InvalidOperationException("llama.cpp XCFramework metadata is missing.");
            }

            var sliceDirectories = Directory.GetDirectories(fullPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (sliceDirectories.Length != 1 ||
                !string.Equals(sliceDirectories[0], "ios-arm64", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("llama.cpp XCFramework must contain only the iOS arm64 device slice.");
            }

            var frameworkPath = Path.Combine(fullPath, "ios-arm64", "llama.framework");
            if (!File.Exists(Path.Combine(frameworkPath, "llama")) ||
                !File.Exists(Path.Combine(frameworkPath, "Headers", "llama.h")))
            {
                throw new InvalidOperationException("llama.cpp iOS arm64 framework binary or headers are missing.");
            }

            return fullPath;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static void AddFrameworkToProject(string buildPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var projectFrameworkPath = "Frameworks/" + FrameworkDirectoryName;
            var frameworkGuid = project.AddFile(
                projectFrameworkPath,
                projectFrameworkPath,
                PBXSourceTree.Source);
            var unityFrameworkTarget = project.GetUnityFrameworkTargetGuid();
            var mainTarget = project.GetUnityMainTargetGuid();
            project.AddFileToBuild(unityFrameworkTarget, frameworkGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(project, mainTarget, frameworkGuid);
            project.SetBuildProperty(unityFrameworkTarget, "IPHONEOS_DEPLOYMENT_TARGET", MinimumIosVersion);
            project.SetBuildProperty(mainTarget, "IPHONEOS_DEPLOYMENT_TARGET", MinimumIosVersion);
            project.AddBuildProperty(
                unityFrameworkTarget,
                "FRAMEWORK_SEARCH_PATHS",
                "$(PROJECT_DIR)/Frameworks");
            project.WriteToFile(projectPath);
        }
    }
}
#endif
