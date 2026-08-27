using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class OnDeviceLLMBuildInputsTests
    {
        private const string TypeName = "EndangeredAR.Build.OnDeviceLLMBuildInputs";

        [Test]
        public void ValidateModel_RequiresExactFilenameSizeAndSha256()
        {
            var type = FindType(TypeName);
            var validate = RequireMethod(type, "ValidateModel");
            var directory = CreateTemporaryDirectory();
            var modelPath = Path.Combine(directory, "fixture.gguf");
            var bytes = new byte[] { 0x47, 0x47, 0x55, 0x46, 0x01, 0x02 };
            File.WriteAllBytes(modelPath, bytes);

            try
            {
                var digest = ComputeSha256(bytes);
                var validated = (string)validate.Invoke(null, new object[]
                {
                    modelPath,
                    "fixture.gguf",
                    (long)bytes.Length,
                    digest
                });

                Assert.That(validated, Is.EqualTo(Path.GetFullPath(modelPath)));
                AssertInvocationFails(validate, modelPath, "other.gguf", (long)bytes.Length, digest);
                AssertInvocationFails(validate, modelPath, "fixture.gguf", (long)bytes.Length + 1, digest);
                AssertInvocationFails(validate, modelPath, "fixture.gguf", (long)bytes.Length, new string('0', 64));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ValidateModel_FailsClosedForMissingOrNonFileInput()
        {
            var validate = RequireMethod(FindType(TypeName), "ValidateModel");
            var directory = CreateTemporaryDirectory();

            try
            {
                AssertInvocationFails(validate, null, "fixture.gguf", 1L, new string('0', 64));
                AssertInvocationFails(validate, "", "fixture.gguf", 1L, new string('0', 64));
                AssertInvocationFails(validate, directory, "fixture.gguf", 1L, new string('0', 64));
                AssertInvocationFails(
                    validate,
                    Path.Combine(directory, "missing.gguf"),
                    "fixture.gguf",
                    1L,
                    new string('0', 64));
            }
            finally
            {
                Directory.Delete(directory, true);
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
            Assert.That(method, Is.Not.Null, $"Expected public static {type.FullName}.{name}.");
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

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }
    }
}
