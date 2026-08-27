#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;

namespace EndangeredAR.Build
{
    public static class OnDeviceLLMBuildInputs
    {
        public const string ModelPathEnvironmentVariable = "ENDANGERED_AR_MODEL_PATH";
        public const string FrameworkPathEnvironmentVariable = "ENDANGERED_AR_LLAMA_XCFRAMEWORK_PATH";
        public const string ModelFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
        public const long ModelSizeBytes = 1117320736L;
        public const string ModelSha256 = "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e";

        public static string ResolveAndValidateModel()
        {
            return ValidateModel(
                Environment.GetEnvironmentVariable(ModelPathEnvironmentVariable),
                ModelFileName,
                ModelSizeBytes,
                ModelSha256);
        }

        public static string ResolveAndValidateFramework()
        {
            return OnDeviceLLMIosPostprocessor.ValidateFramework(
                Environment.GetEnvironmentVariable(FrameworkPathEnvironmentVariable));
        }

        public static string ValidateModel(
            string path,
            string expectedFileName,
            long expectedSize,
            string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(expectedFileName) ||
                expectedSize <= 0 ||
                string.IsNullOrWhiteSpace(expectedSha256))
            {
                throw new InvalidOperationException("On-device model input is not configured.");
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException("On-device model input is missing or is not a file.");
            }

            if (!string.Equals(Path.GetFileName(fullPath), expectedFileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("On-device model filename does not match the locked identity.");
            }

            var info = new FileInfo(fullPath);
            if (info.Length != expectedSize)
            {
                throw new InvalidOperationException("On-device model size does not match the locked identity.");
            }

            var actualSha256 = ComputeSha256(fullPath);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("On-device model SHA-256 does not match the locked identity.");
            }

            return fullPath;
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(stream);
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
#endif
