#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class EndangeredARIosPostprocessor
{
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
        var appTransportSecurity = root.CreateDict("NSAppTransportSecurity");
        appTransportSecurity.SetBoolean("NSAllowsArbitraryLoads", true);
        root.SetString("NSCameraUsageDescription", "用于扫描濒危动物 AR 识别卡");
        root.SetString("NSLocalNetworkUsageDescription", "用于连接同一 Wi-Fi 下的本地 AI 后端服务");

        plist.WriteToFile(plistPath);
    }
}
#endif
