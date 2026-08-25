using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace EndangeredAR.Memory
{
    public sealed class JsonCharacterMemoryRepository : ICharacterMemoryRepository
    {
        public const int CurrentSchemaVersion = 1;

        private readonly string filePath;
        private readonly Func<DateTime> utcNow;
        private bool futureVersionLoaded;

        public JsonCharacterMemoryRepository(string filePath, Func<DateTime> utcNow)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A character memory file path is required.", nameof(filePath));
            }

            this.filePath = Path.GetFullPath(filePath);
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public CharacterMemoryLoadResult Load()
        {
            futureVersionLoaded = false;
            if (!File.Exists(filePath))
            {
                if (TryLoadSupported(filePath + ".bak", out var backupDocument, out var backupIsFuture))
                {
                    if (backupIsFuture)
                    {
                        futureVersionLoaded = true;
                        return FutureVersion();
                    }

                    RestoreBackup();
                    return Available(backupDocument, CharacterMemoryStoreStatus.RecoveredFromBackup);
                }

                if (File.Exists(filePath + ".bak"))
                {
                    Quarantine(filePath + ".bak");
                    return CreateAndPersistEmpty(CharacterMemoryStoreStatus.RecoveredEmpty);
                }

                return CreateAndPersistEmpty(CharacterMemoryStoreStatus.Available);
            }

            if (TryLoadSupported(filePath, out var document, out var isFuture))
            {
                if (isFuture)
                {
                    futureVersionLoaded = true;
                    return FutureVersion();
                }

                EnsureBackupExists();
                return Available(document, CharacterMemoryStoreStatus.Available);
            }

            Quarantine(filePath);
            if (TryLoadSupported(filePath + ".bak", out var recovered, out var backupFuture))
            {
                if (backupFuture)
                {
                    futureVersionLoaded = true;
                    return FutureVersion();
                }

                RestoreBackup();
                return Available(recovered, CharacterMemoryStoreStatus.RecoveredFromBackup);
            }

            if (File.Exists(filePath + ".bak"))
            {
                Quarantine(filePath + ".bak");
            }

            return CreateAndPersistEmpty(CharacterMemoryStoreStatus.RecoveredEmpty);
        }

        public void Save(CharacterMemoryDocument document)
        {
            if (!futureVersionLoaded &&
                TryLoadSupported(filePath, out _, out var fileIsFuture) &&
                fileIsFuture)
            {
                futureVersionLoaded = true;
            }

            if (futureVersionLoaded)
            {
                throw new InvalidOperationException("A future character memory schema is read-only.");
            }

            var normalized = CharacterMemoryDocumentUtility.Clone(document);
            if (normalized.schemaVersion > CurrentSchemaVersion)
            {
                throw new InvalidOperationException("A future character memory schema cannot be written.");
            }

            normalized.schemaVersion = CurrentSchemaVersion;
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            try
            {
                WriteFlushed(temporaryPath, JsonUtility.ToJson(normalized));
                ReplacePrimary(temporaryPath);
                File.Copy(filePath, filePath + ".bak", true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private CharacterMemoryLoadResult CreateAndPersistEmpty(CharacterMemoryStoreStatus status)
        {
            var document = CharacterMemoryDocumentUtility.CreateEmpty();
            Save(document);
            return Available(document, status);
        }

        private static CharacterMemoryLoadResult Available(
            CharacterMemoryDocument document,
            CharacterMemoryStoreStatus status)
        {
            return new CharacterMemoryLoadResult(
                CharacterMemoryDocumentUtility.Clone(document),
                status,
                true);
        }

        private static CharacterMemoryLoadResult FutureVersion()
        {
            return new CharacterMemoryLoadResult(null, CharacterMemoryStoreStatus.FutureVersion, false);
        }

        private static void WriteFlushed(string path, string json)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private void ReplacePrimary(string temporaryPath)
        {
            if (!File.Exists(filePath))
            {
                File.Move(temporaryPath, filePath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, filePath, filePath + ".bak");
            }
            catch (PlatformNotSupportedException)
            {
                ReplacePrimaryWithMoves(temporaryPath);
            }
            catch (IOException)
            {
                ReplacePrimaryWithMoves(temporaryPath);
            }
        }

        private void ReplacePrimaryWithMoves(string temporaryPath)
        {
            var backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(filePath, backupPath);
            File.Move(temporaryPath, filePath);
        }

        private bool TryLoadSupported(
            string path,
            out CharacterMemoryDocument document,
            out bool isFuture)
        {
            document = null;
            isFuture = false;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var parsed = JsonUtility.FromJson<CharacterMemoryDocument>(File.ReadAllText(path));
                if (parsed == null || parsed.schemaVersion <= 0)
                {
                    return false;
                }

                if (parsed.schemaVersion > CurrentSchemaVersion)
                {
                    isFuture = true;
                    return true;
                }

                document = CharacterMemoryDocumentUtility.Clone(parsed);
                document.schemaVersion = CurrentSchemaVersion;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private void EnsureBackupExists()
        {
            if (!File.Exists(filePath + ".bak"))
            {
                File.Copy(filePath, filePath + ".bak");
            }
        }

        private void RestoreBackup()
        {
            File.Copy(filePath + ".bak", filePath, true);
        }

        private void Quarantine(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var timestamp = utcNow().ToUniversalTime().ToString("yyyyMMdd-HHmmss");
            var quarantinePath = path + ".corrupt-" + timestamp;
            var collisionIndex = 1;
            while (File.Exists(quarantinePath))
            {
                quarantinePath = path + ".corrupt-" + timestamp + "-" + collisionIndex;
                collisionIndex++;
            }

            File.Move(path, quarantinePath);
        }
    }
}
