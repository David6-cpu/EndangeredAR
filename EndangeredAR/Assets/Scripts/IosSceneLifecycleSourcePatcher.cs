#if UNITY_EDITOR
using System;

namespace EndangeredAR.Build
{
    public static class IosSceneLifecycleSourcePatcher
    {
        public static string PatchUnityAppControllerHeader(string source)
        {
            EnsureSource(source, nameof(source));
            if (source.Contains("- (void)initUnityWithScene:(UIWindowScene*)scene;") &&
                source.Contains("- (void)startUnity;") &&
                !source.Contains("- (void)startUnity:(UIApplication*)application;"))
            {
                return source;
            }

            source = ReplaceRequired(
                source,
                "- (void)preStartUnity;\n\n// this one is called at at the very end of didFinishLaunchingWithOptions:",
                "- (void)preStartUnity;\n\n- (void)initUnityApplicationNoGraphics;\n- (void)initUnityWithScene:(UIWindowScene*)scene;\n\n// this one is called at at the very end of didFinishLaunchingWithOptions:",
                "UnityAppController lifecycle declarations");

            return ReplaceRequired(
                source,
                "- (void)startUnity:(UIApplication*)application;",
                "- (void)startUnity;",
                "UnityAppController startUnity declaration");
        }

        public static string PatchUnityAppControllerImplementation(string source)
        {
            EnsureSource(source, nameof(source));
            if (source.Contains("- (void)initUnityWithScene:(UIWindowScene*)scene") &&
                source.Contains("@selector(startUnity)") &&
                !source.Contains("initUnityWithApplication"))
            {
                return source;
            }

            source = ReplaceRequired(
                source,
                "- (void)startUnity:(UIApplication*)application\n{\n    NSAssert(_unityAppReady == NO, @\"[UnityAppController startUnity:] called after Unity has been initialized\");",
                "- (void)startUnity\n{\n    NSAssert(_unityAppReady == NO, @\"[UnityAppController startUnity] called after Unity has been initialized\");",
                "UnityAppController startUnity implementation");

            source = ReplaceRequired(
                source,
                "    // if application is in background, don't initialize Unity\n" +
                "    // this happens if app uses location fence, notifications with content/actions, ...\n" +
                "    // initUnityWithApplication: initializes rendering, possibly loads scene and calls Start(), none meant for background\n" +
                "    if (UIApplication.sharedApplication.applicationState == UIApplicationStateBackground)\n" +
                "        return YES;\n\n" +
                "    [self initUnityWithApplication: application];\n" +
                "    return YES;\n}",
                "    return YES;\n}",
                "UnityAppController application launch tail");

            source = ReplaceRequired(
                source,
                "- (void)initUnityWithApplication:(UIApplication*)application",
                "- (void)initUnityWithScene:(UIWindowScene*)scene",
                "UnityAppController scene initializer signature");

            source = ReplaceRequired(
                source,
                "#if !PLATFORM_VISIONOS\n" +
                "    if (@available(iOS 13, tvOS 13, *))\n" +
                "        _window = [[UIWindow alloc] initWithWindowScene: [self pickStartupWindowScene: application.connectedScenes]];\n" +
                "    else\n" +
                "        _window = [[UIWindow alloc] initWithFrame: [UIScreen mainScreen].bounds];\n" +
                "#else\n" +
                "    _window = [[UIWindow alloc] init]; \n" +
                "#endif",
                "    if (scene == nil)\n" +
                "        _window = [[UIWindow alloc] init];\n" +
                "    else\n" +
                "        _window = [[UIWindow alloc] initWithWindowScene: scene];",
                "UnityAppController scene window creation");

            source = ReplaceRequired(
                source,
                "[self performSelector: @selector(startUnity:) withObject: application afterDelay: 0];",
                "[self performSelector: @selector(startUnity) withObject: nil afterDelay: 0];",
                "UnityAppController delayed startup");

            source = ReplaceRequired(
                source,
                "[self startUnity: application];",
                "[self startUnity];",
                "UnityAppController immediate startup");

            return ReplaceRequired(
                source,
                "    else\n" +
                "    {\n" +
                "        [self initUnityWithApplication: application];\n" +
                "    }",
                "    else\n" +
                "    {\n" +
                "        UIWindowScene* scene = [self pickStartupWindowScene: application.connectedScenes];\n" +
                "        [self initUnityWithScene: scene];\n" +
                "    }",
                "UnityAppController foreground initialization");
        }

        public static string PatchDisplayManager(string source)
        {
            EnsureSource(source, nameof(source));
            if (source.Contains("- (UIWindowScene*)sceneForScreen:(UIScreen*)screen") &&
                source.Contains("window.windowScene = [self sceneForScreen: _screen];") &&
                !source.Contains("window.screen = _screen;"))
            {
                return source;
            }

            const string createViewDeclaration = "- (void)createView:(BOOL)useForRendering showRightAway:(BOOL)showRightAway;";
            var sceneLookup =
                "#if !PLATFORM_VISIONOS\n" +
                "- (UIWindowScene*)sceneForScreen:(UIScreen*)screen\n" +
                "{\n" +
                "    for (UIScene* scene in UIApplication.sharedApplication.connectedScenes)\n" +
                "    {\n" +
                "        if ([scene isKindOfClass: [UIWindowScene class]])\n" +
                "        {\n" +
                "            UIWindowScene* windowScene = (UIWindowScene*)scene;\n" +
                "            if (windowScene.screen == screen)\n" +
                "                return windowScene;\n" +
                "        }\n" +
                "    }\n" +
                "    return nil;\n" +
                "}\n" +
                "#endif\n\n";

            source = ReplaceRequired(
                source,
                createViewDeclaration,
                sceneLookup + createViewDeclaration,
                "DisplayManager scene lookup");

            source = ReplaceRequired(
                source,
                "window.screen = _screen;",
                "window.windowScene = [self sceneForScreen: _screen];",
                "DisplayManager window scene assignment");

            return ReplaceRequired(
                source,
                "- (void)shouldShowWindow:(BOOL)show\n" +
                "{\n" +
                "    _window.hidden = show ? NO : YES;\n" +
                "#if !PLATFORM_VISIONOS\n" +
                "    _window.screen = show ? _screen : nil;\n" +
                "#endif\n" +
                "}",
                "- (void)shouldShowWindow:(BOOL)show\n" +
                "{\n" +
                "    if (_window.hidden != show)\n" +
                "        return;\n\n" +
                "    _window.hidden = !show;\n" +
                "#if !PLATFORM_VISIONOS\n" +
                "    _window.windowScene = show ? [self sceneForScreen: _screen] : nil;\n" +
                "#endif\n" +
                "}",
                "DisplayManager show and hide lifecycle");
        }

        private static string ReplaceRequired(string source, string oldValue, string newValue, string description)
        {
            if (!source.Contains(oldValue))
            {
                throw new InvalidOperationException($"Could not patch {description}; the generated Unity source format changed.");
            }

            return source.Replace(oldValue, newValue);
        }

        private static void EnsureSource(string source, string parameterName)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("Generated Unity source must not be empty.", parameterName);
            }
        }
    }
}
#endif
