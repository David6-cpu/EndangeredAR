#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using EndangeredAR.Build;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class EndangeredARIosPostprocessor
{
    public const string DevelopmentRemoteNetworkFlag = "ENDANGERED_AR_DEVELOPMENT_REMOTE_NETWORK";

    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        var root = plist.root;
        ConfigureDevelopmentNetworkPermissions(
            root,
            string.Equals(
                Environment.GetEnvironmentVariable(DevelopmentRemoteNetworkFlag),
                "1",
                StringComparison.Ordinal));
        root.SetString("NSCameraUsageDescription", "用于扫描濒危动物 AR 识别卡");

        if (RequiresSceneLifecycleBackport(Application.unityVersion))
        {
            ConfigureSceneManifest(root);
            PatchSceneLifecycle(pathToBuiltProject);
            Debug.Log($"Applied iOS UIScene lifecycle backport for Unity {Application.unityVersion}.");
        }

        plist.WriteToFile(plistPath);
    }

    public static void ConfigureDevelopmentNetworkPermissions(
        PlistElementDict root,
        bool developmentRemoteEnabled)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        root.values.Remove("NSLocalNetworkUsageDescription");
        root.values.Remove("NSAppTransportSecurity");
        if (!developmentRemoteEnabled)
        {
            return;
        }

        var appTransportSecurity = root.CreateDict("NSAppTransportSecurity");
        appTransportSecurity.SetBoolean("NSAllowsArbitraryLoads", true);
        root.SetString(
            "NSLocalNetworkUsageDescription",
            "仅用于 Development Build 显式连接开发机 AI 基准服务");
    }

    private static bool RequiresSceneLifecycleBackport(string unityVersion)
    {
        var versionParts = unityVersion.Split('.');
        if (versionParts.Length < 3 || versionParts[0] != "2022" || versionParts[1] != "3")
        {
            return false;
        }

        var patchDigits = new string(versionParts[2].TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(patchDigits, out var patchVersion) && patchVersion < 72;
    }

    private static void ConfigureSceneManifest(PlistElementDict root)
    {
        var sceneManifest = root.CreateDict("UIApplicationSceneManifest");
        sceneManifest.SetBoolean("UIApplicationSupportsMultipleScenes", false);
        var configurations = sceneManifest.CreateDict("UISceneConfigurations");
        var applicationRole = configurations.CreateArray("UIWindowSceneSessionRoleApplication");
        var configuration = applicationRole.AddDict();
        configuration.SetString("UISceneConfigurationName", "Default Configuration");
        configuration.SetString("UISceneDelegateClassName", "UnityScene");
    }

    private static void PatchSceneLifecycle(string buildPath)
    {
        PatchSource(
            Path.Combine(buildPath, "Classes", "UnityAppController.h"),
            IosSceneLifecycleSourcePatcher.PatchUnityAppControllerHeader);
        PatchSource(
            Path.Combine(buildPath, "Classes", "UnityAppController.mm"),
            IosSceneLifecycleSourcePatcher.PatchUnityAppControllerImplementation);
        PatchSource(
            Path.Combine(buildPath, "Classes", "Unity", "DisplayManager.mm"),
            IosSceneLifecycleSourcePatcher.PatchDisplayManager);

        var uiDirectory = Path.Combine(buildPath, "Classes", "UI");
        Directory.CreateDirectory(uiDirectory);
        CopyTemplate("UnityScene.h.txt", Path.Combine(uiDirectory, "UnityScene.h"));
        CopyTemplate("UnityScene.mm.txt", Path.Combine(uiDirectory, "UnityScene.mm"));

        var projectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        AddProjectFile(project, "Classes/UI/UnityScene.h", false);
        AddProjectFile(project, "Classes/UI/UnityScene.mm", true);
        project.WriteToFile(projectPath);
    }

    private static void PatchSource(string path, Func<string, string> patch)
    {
        var source = File.ReadAllText(path);
        File.WriteAllText(path, patch(source));
    }

    private static void CopyTemplate(string templateName, string destinationPath)
    {
        var sourcePath = Path.Combine(Application.dataPath, "Editor", templateName);
        File.Copy(sourcePath, destinationPath, true);
    }

    private static void AddProjectFile(PBXProject project, string projectPath, bool compile)
    {
        var fileGuid = project.FindFileGuidByProjectPath(projectPath);
        if (string.IsNullOrEmpty(fileGuid))
        {
            fileGuid = project.AddFile(projectPath, projectPath, PBXSourceTree.Source);
        }

        if (compile)
        {
            project.AddFileToBuild(project.GetUnityFrameworkTargetGuid(), fileGuid);
        }
    }
}
#endif
