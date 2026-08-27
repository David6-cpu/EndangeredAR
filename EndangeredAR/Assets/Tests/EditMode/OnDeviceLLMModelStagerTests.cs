using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMModelStagerTests
    {
        private const string TypeName = "EndangeredAR.Build.OnDeviceLLMModelStager";

        [Test]
        public void Stage_CopiesToApprovedStreamingAssetsLocationAndDisposeCleansEverything()
        {
            var type = FindType(TypeName);
            var stage = RequireMethod(type, "Stage");
            var root = CreateTemporaryDirectory();
            var assetsPath = Path.Combine(root, "Assets");
            var sourcePath = Path.Combine(root, "fixture.gguf");
            Directory.CreateDirectory(assetsPath);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });

            try
            {
                var scope = stage.Invoke(null, new object[] { assetsPath, sourcePath }) as IDisposable;
                Assert.That(scope, Is.Not.Null);

                var stagedPath = Path.Combine(
                    assetsPath,
                    "StreamingAssets",
                    "OnDeviceModels",
                    Path.GetFileName(sourcePath));
                Assert.That(File.Exists(stagedPath), Is.True);
                Assert.That(File.ReadAllBytes(stagedPath), Is.EqualTo(File.ReadAllBytes(sourcePath)));

                scope.Dispose();
                Assert.That(File.Exists(stagedPath), Is.False);
                Assert.That(Directory.Exists(Path.GetDirectoryName(stagedPath)), Is.False);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void Stage_RefusesExistingDestinationAndSourceInsideAssets()
        {
            var stage = RequireMethod(FindType(TypeName), "Stage");
            var root = CreateTemporaryDirectory();
            var assetsPath = Path.Combine(root, "Assets");
            var externalSource = Path.Combine(root, "fixture.gguf");
            Directory.CreateDirectory(assetsPath);
            File.WriteAllText(externalSource, "fixture");

            try
            {
                var scope = (IDisposable)stage.Invoke(null, new object[] { assetsPath, externalSource });
                try
                {
                    AssertInvocationFails(stage, assetsPath, externalSource);
                }
                finally
                {
                    scope.Dispose();
                }

                var internalSource = Path.Combine(assetsPath, "model.gguf");
                File.WriteAllText(internalSource, "fixture");
                AssertInvocationFails(stage, assetsPath, internalSource);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Expected {fullName} to exist.");
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static void AssertInvocationFails(MethodInfo method, params object[] arguments)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, arguments));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
