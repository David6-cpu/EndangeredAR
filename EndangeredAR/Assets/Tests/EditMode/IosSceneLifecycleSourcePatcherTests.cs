using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public class IosSceneLifecycleSourcePatcherTests
    {
        private const string PatcherTypeName = "EndangeredAR.Build.IosSceneLifecycleSourcePatcher";

        [Test]
        public void PatchMethods_UpgradeLegacyUnitySourcesAndRemainIdempotent()
        {
            var patcherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(PatcherTypeName, false))
                .FirstOrDefault(type => type != null);

            Assert.That(patcherType, Is.Not.Null,
                $"Expected {PatcherTypeName} to provide the legacy Unity iOS lifecycle patch.");

            var legacyHeader = @"- (void)preStartUnity;

// this one is called at at the very end of didFinishLaunchingWithOptions:
// it will start showing unity view and rendering unity content
- (void)startUnity:(UIApplication*)application;";

            var legacyController = @"- (void)startUnity:(UIApplication*)application
{
    NSAssert(_unityAppReady == NO, @""[UnityAppController startUnity:] called after Unity has been initialized"");
}

    // if application is in background, don't initialize Unity
    // this happens if app uses location fence, notifications with content/actions, ...
    // initUnityWithApplication: initializes rendering, possibly loads scene and calls Start(), none meant for background
    if (UIApplication.sharedApplication.applicationState == UIApplicationStateBackground)
        return YES;

    [self initUnityWithApplication: application];
    return YES;
}

- (void)initUnityWithApplication:(UIApplication*)application
{
#if !PLATFORM_VISIONOS
    if (@available(iOS 13, tvOS 13, *))
        _window = [[UIWindow alloc] initWithWindowScene: [self pickStartupWindowScene: application.connectedScenes]];
    else
        _window = [[UIWindow alloc] initWithFrame: [UIScreen mainScreen].bounds];
#else
" + "    _window = [[UIWindow alloc] init]; \n" + @"#endif

    [self performSelector: @selector(startUnity:) withObject: application afterDelay: 0];
    [self startUnity: application];
}

    else
    {
        [self initUnityWithApplication: application];
    }";

            var legacyDisplayManager = @"- (void)createView:(BOOL)useForRendering showRightAway:(BOOL)showRightAway;
{
        UIWindow* window = [[UIWindow alloc] initWithFrame: _screen.bounds];
        window.screen = _screen;
}

- (void)shouldShowWindow:(BOOL)show
{
    _window.hidden = show ? NO : YES;
#if !PLATFORM_VISIONOS
    _window.screen = show ? _screen : nil;
#endif
}";

            var patchedHeader = InvokePatch(patcherType, "PatchUnityAppControllerHeader", legacyHeader);
            var patchedController = InvokePatch(patcherType, "PatchUnityAppControllerImplementation", legacyController);
            var patchedDisplayManager = InvokePatch(patcherType, "PatchDisplayManager", legacyDisplayManager);

            StringAssert.Contains("initUnityWithScene:(UIWindowScene*)scene", patchedHeader);
            StringAssert.DoesNotContain("startUnity:(UIApplication*)application", patchedHeader);
            StringAssert.Contains("initUnityWithScene:(UIWindowScene*)scene", patchedController);
            StringAssert.Contains("@selector(startUnity)", patchedController);
            StringAssert.DoesNotContain("initUnityWithApplication", patchedController);
            StringAssert.Contains("sceneForScreen:(UIScreen*)screen", patchedDisplayManager);
            StringAssert.Contains("window.windowScene", patchedDisplayManager);
            StringAssert.DoesNotContain("window.screen", patchedDisplayManager);

            Assert.That(InvokePatch(patcherType, "PatchUnityAppControllerHeader", patchedHeader), Is.EqualTo(patchedHeader));
            Assert.That(InvokePatch(patcherType, "PatchUnityAppControllerImplementation", patchedController), Is.EqualTo(patchedController));
            Assert.That(InvokePatch(patcherType, "PatchDisplayManager", patchedDisplayManager), Is.EqualTo(patchedDisplayManager));
        }

        private static string InvokePatch(Type patcherType, string methodName, string source)
        {
            var method = patcherType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Expected public patch method {methodName}.");
            return (string)method.Invoke(null, new object[] { source });
        }
    }
}
