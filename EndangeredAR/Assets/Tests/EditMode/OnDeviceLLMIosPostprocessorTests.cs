using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMIosPostprocessorTests
    {
        private const string TypeName = "EndangeredAR.Build.OnDeviceLLMIosPostprocessor";

        [Test]
        public void ValidateFramework_RequiresOnlyTheIosArm64DeviceSlice()
        {
            var type = FindType(TypeName);
            var validate = RequireMethod(type, "ValidateFramework");
            var root = CreateTemporaryDirectory();

            try
            {
                var framework = CreateFramework(root, includeDevice: true, includeSimulator: false);
                var validated = (string)validate.Invoke(null, new object[] { framework });
                Assert.That(validated, Is.EqualTo(Path.GetFullPath(framework)));

                Directory.Delete(framework, true);
                framework = CreateFramework(root, includeDevice: false, includeSimulator: true);
                AssertInvocationFails(validate, framework);

                Directory.Delete(framework, true);
                framework = CreateFramework(root, includeDevice: true, includeSimulator: true);
                AssertInvocationFails(validate, framework);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ValidateFramework_FailsForMissingBinaryOrHeaders()
        {
            var validate = RequireMethod(FindType(TypeName), "ValidateFramework");
            var root = CreateTemporaryDirectory();

            try
            {
                AssertInvocationFails(validate, Path.Combine(root, "missing.xcframework"));

                var framework = CreateFramework(root, includeDevice: true, includeSimulator: false);
                File.Delete(Path.Combine(framework, "ios-arm64", "llama.framework", "llama"));
                AssertInvocationFails(validate, framework);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateFramework(string root, bool includeDevice, bool includeSimulator)
        {
            var framework = Path.Combine(root, "llama.xcframework");
            Directory.CreateDirectory(framework);
            File.WriteAllText(Path.Combine(framework, "Info.plist"), "<?xml version=\"1.0\"?><plist><dict/></plist>");
            if (includeDevice)
            {
                CreateSlice(Path.Combine(framework, "ios-arm64"));
            }

            if (includeSimulator)
            {
                CreateSlice(Path.Combine(framework, "ios-arm64_x86_64-simulator"));
            }

            return framework;
        }

        private static void CreateSlice(string slice)
        {
            var framework = Path.Combine(slice, "llama.framework");
            var headers = Path.Combine(framework, "Headers");
            Directory.CreateDirectory(headers);
            File.WriteAllText(Path.Combine(framework, "llama"), "binary");
            File.WriteAllText(Path.Combine(headers, "llama.h"), "header");
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
