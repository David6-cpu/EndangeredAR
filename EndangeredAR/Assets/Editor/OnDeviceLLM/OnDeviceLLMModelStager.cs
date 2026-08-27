#if UNITY_EDITOR
using System;
using System.IO;

namespace EndangeredAR.Build
{
    public static class OnDeviceLLMModelStager
    {
        private const string StreamingAssetsDirectoryName = "StreamingAssets";
        private const string ModelDirectoryName = "OnDeviceModels";

        public static IDisposable Stage(string assetsPath, string validatedModelPath)
        {
            if (string.IsNullOrWhiteSpace(assetsPath) || !Directory.Exists(assetsPath))
            {
                throw new InvalidOperationException("Unity Assets directory is unavailable for model staging.");
            }

            if (string.IsNullOrWhiteSpace(validatedModelPath) || !File.Exists(validatedModelPath))
            {
                throw new InvalidOperationException("Validated model source is unavailable for staging.");
            }

            var fullAssetsPath = EnsureTrailingSeparator(Path.GetFullPath(assetsPath));
            var fullSourcePath = Path.GetFullPath(validatedModelPath);
            if (fullSourcePath.StartsWith(fullAssetsPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The on-device model source must remain outside Unity Assets.");
            }

            var streamingAssetsPath = Path.Combine(fullAssetsPath, StreamingAssetsDirectoryName);
            var modelDirectoryPath = Path.Combine(streamingAssetsPath, ModelDirectoryName);
            var stagedModelPath = Path.Combine(modelDirectoryPath, Path.GetFileName(fullSourcePath));
            if (Directory.Exists(modelDirectoryPath) || File.Exists(stagedModelPath))
            {
                throw new InvalidOperationException("The on-device model staging destination is not clean.");
            }

            try
            {
                Directory.CreateDirectory(modelDirectoryPath);
                File.Copy(fullSourcePath, stagedModelPath, false);
                return new StagingScope(stagedModelPath, modelDirectoryPath, streamingAssetsPath);
            }
            catch
            {
                Cleanup(stagedModelPath, modelDirectoryPath, streamingAssetsPath);
                throw;
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static void Cleanup(string modelPath, string modelDirectoryPath, string streamingAssetsPath)
        {
            DeleteFileIfPresent(modelPath);
            DeleteFileIfPresent(modelPath + ".meta");
            DeleteDirectoryIfEmpty(modelDirectoryPath);
            DeleteFileIfPresent(modelDirectoryPath + ".meta");
            DeleteDirectoryIfEmpty(streamingAssetsPath);
            DeleteFileIfPresent(streamingAssetsPath + ".meta");
        }

        private static void DeleteFileIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteDirectoryIfEmpty(string path)
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
            {
                Directory.Delete(path);
            }
        }

        private sealed class StagingScope : IDisposable
        {
            private readonly string modelPath;
            private readonly string modelDirectoryPath;
            private readonly string streamingAssetsPath;
            private bool disposed;

            public StagingScope(string modelPath, string modelDirectoryPath, string streamingAssetsPath)
            {
                this.modelPath = modelPath;
                this.modelDirectoryPath = modelDirectoryPath;
                this.streamingAssetsPath = streamingAssetsPath;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Cleanup(modelPath, modelDirectoryPath, streamingAssetsPath);
            }
        }
    }
}
#endif
