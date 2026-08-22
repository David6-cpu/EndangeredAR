using System;
using System.Collections;
using System.IO;
using System.Reflection;
using EndangeredAR.Animals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Tests.EditMode
{
    public class AnimalModelLoaderTests
    {
        private const string LoaderTypeName = "EndangeredAR.Models.AnimalModelLoader, EndangeredAR.Runtime";

        [Test]
        public void Configure_CopiesDefinitionPathsAndLocalPresentation()
        {
            var definition = CreateDefinition(
                "pangolin",
                "Models/Pangolin/pangolin.glb",
                "Models/Pangolin/pangolin_basecolor.png",
                new Vector3(1f, 2f, 3f),
                new Vector3(4f, 5f, 6f),
                new Vector3(1.2f, 1.3f, 1.4f));
            var host = new GameObject("Animal Host");

            try
            {
                var loader = AddGenericLoader(host);
                Configure(loader, definition);

                var serializedLoader = new SerializedObject(loader);
                Assert.That(serializedLoader.FindProperty("streamingAssetPath").stringValue, Is.EqualTo(definition.ModelRelativePath));
                Assert.That(serializedLoader.FindProperty("baseColorTexturePath").stringValue, Is.EqualTo(definition.BaseColorTextureRelativePath));
                Assert.That(serializedLoader.FindProperty("modelLocalPosition").vector3Value, Is.EqualTo(definition.ModelLocalOffset));
                Assert.That(serializedLoader.FindProperty("modelLocalRotation").vector3Value, Is.EqualTo(definition.ModelEulerAngles));
                Assert.That(serializedLoader.FindProperty("modelLocalScale").vector3Value, Is.EqualTo(definition.ModelScale));
                Assert.That(ReadLoadedAnimalId(loader), Is.EqualTo(definition.AnimalId));
            }
            finally
            {
                Destroy(definition, host);
            }
        }

        [Test]
        public void Configure_DoesNotMoveHostExperienceTransform()
        {
            var definition = CreateDefinition(
                "leopard",
                "Models/Leopard/leopard.glb",
                "Models/Leopard/leopard_basecolor.png",
                new Vector3(8f, 9f, 10f),
                new Vector3(11f, 12f, 13f),
                new Vector3(1.6f, 1.7f, 1.8f));
            var host = new GameObject("Animal Host");
            host.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(20f, 30f, 40f));
            host.transform.localScale = new Vector3(0.4f, 0.5f, 0.6f);
            var expectedPosition = host.transform.position;
            var expectedRotation = host.transform.rotation;
            var expectedScale = host.transform.localScale;

            try
            {
                Configure(AddGenericLoader(host), definition);

                Assert.That(host.transform.position, Is.EqualTo(expectedPosition));
                Assert.That(host.transform.rotation, Is.EqualTo(expectedRotation));
                Assert.That(host.transform.localScale, Is.EqualTo(expectedScale));
            }
            finally
            {
                Destroy(definition, host);
            }
        }

        [Test]
        public void Configure_CopiesOptionalRiggedPrefabWithoutChangingTheHost()
        {
            var definition = CreateDefinition(
                "sensen",
                "Models/Sensen/sensen.glb",
                "Models/Sensen/sensen_basecolor.png",
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            var riggedPrefab = new GameObject("Rigged Candidate");
            var host = new GameObject("Animal Host");

            try
            {
                SetDefinitionPrefab(definition, riggedPrefab);
                var loader = AddGenericLoader(host);
                Configure(loader, definition);

                var serializedLoader = new SerializedObject(loader);
                var modelPrefab = serializedLoader.FindProperty("modelPrefab");
                Assert.That(modelPrefab, Is.Not.Null, "AnimalModelLoader needs an optional modelPrefab field.");
                Assert.That(modelPrefab.objectReferenceValue, Is.SameAs(riggedPrefab));
                Assert.That(host.transform.childCount, Is.Zero,
                    "EditMode configuration must not instantiate product content before Play Mode.");
            }
            finally
            {
                Destroy(definition, riggedPrefab, host);
            }
        }

        [Test]
        public void LoadModel_ValidRiggedPrefabIsPreferredOverMissingGlb()
        {
            var definition = CreateDefinition(
                "sensen",
                "Models/Test/MissingAnimal.glb",
                string.Empty,
                Vector3.zero,
                new Vector3(1f, 2f, 3f),
                new Vector3(1.2f, 1.2f, 1.2f));
            var riggedPrefab = new GameObject("Rigged Candidate");
            riggedPrefab.AddComponent<MeshRenderer>();
            var host = new GameObject("Animal Host");
            var fallback = host.AddComponent<MeshRenderer>();

            try
            {
                SetDefinitionPrefab(definition, riggedPrefab);
                var loader = AddGenericLoader(host);
                Configure(loader, definition);

                var loadRoutine = InvokeLoadModel(loader);

                Assert.That(loadRoutine.MoveNext(), Is.False);
                var runtimeRoot = host.transform.Find("Animal GLB Runtime Root");
                Assert.That(runtimeRoot, Is.Not.Null);
                Assert.That(runtimeRoot.localPosition, Is.EqualTo(definition.ModelLocalOffset));
                Assert.That(runtimeRoot.localScale, Is.EqualTo(definition.ModelScale));
                Assert.That(runtimeRoot.Find("Rigged Candidate(Clone)"), Is.Not.Null);
                Assert.That(fallback.enabled, Is.False);
                Assert.That(host.GetComponent<AnimalGestureController>(), Is.Not.Null);
            }
            finally
            {
                Destroy(definition, riggedPrefab, host);
            }
        }

        [Test]
        public void BeginLoad_WhenRiggedPrefabIsAlreadyLoaded_DoesNotRestoreFallbackRenderer()
        {
            var definition = CreateDefinition(
                "sensen",
                "Models/Test/MissingAnimal.glb",
                string.Empty,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            var riggedPrefab = new GameObject("Rigged Candidate");
            riggedPrefab.AddComponent<MeshRenderer>();
            var host = new GameObject("Animal Host");
            var fallback = host.AddComponent<MeshRenderer>();

            try
            {
                SetDefinitionPrefab(definition, riggedPrefab);
                var loader = AddGenericLoader(host);
                Configure(loader, definition);
                Assert.That(InvokeLoadModel(loader).MoveNext(), Is.False);
                Assert.That(fallback.enabled, Is.False);

                InvokePrivate(loader, "BeginLoad");

                Assert.That(fallback.enabled, Is.False,
                    "A redundant lifecycle load must not reveal the capsule after the rigged prefab is already visible.");
            }
            finally
            {
                Destroy(definition, riggedPrefab, host);
            }
        }

        [Test]
        public void LoadModel_RiggedPrefabWithoutRendererFallsBackToGlbPath()
        {
            var definition = CreateDefinition(
                "sensen",
                "Models/Test/MissingAnimal.glb",
                string.Empty,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            var invalidPrefab = new GameObject("Invalid Rigged Candidate");
            var host = new GameObject("Animal Host");
            var fallback = host.AddComponent<MeshRenderer>();
            fallback.enabled = false;

            try
            {
                SetDefinitionPrefab(definition, invalidPrefab);
                var loader = AddGenericLoader(host);
                Configure(loader, definition);

                var loadRoutine = InvokeLoadModel(loader);

                Assert.That(loadRoutine.MoveNext(), Is.False);
                Assert.That(host.transform.Find("Animal GLB Runtime Root"), Is.Null);
                Assert.That(fallback.enabled, Is.True);
            }
            finally
            {
                Destroy(definition, invalidPrefab, host);
            }
        }

        [Test]
        public void Configure_MissingModelLeavesFallbackRendererEnabled()
        {
            var definition = CreateDefinition(
                "missing",
                string.Empty,
                string.Empty,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            var host = new GameObject("Animal Host");
            var fallback = host.AddComponent<MeshRenderer>();
            fallback.enabled = false;

            try
            {
                Configure(AddGenericLoader(host), definition);

                Assert.That(fallback.enabled, Is.True);
                Assert.That(host.transform.Find("Animal GLB Runtime Root"), Is.Null);
            }
            finally
            {
                Destroy(definition, host);
            }
        }

        [Test]
        public void LoadModel_NonexistentRelativePathRestoresDisabledFallbackRenderer()
        {
            const string missingRelativePath = "Models/Test/MissingAnimal.glb";
            var host = new GameObject("Animal Host");
            var fallback = host.AddComponent<MeshRenderer>();
            fallback.enabled = false;

            try
            {
                var loader = AddGenericLoader(host);
                SetStreamingAssetPath(loader, missingRelativePath);

                var loadModel = loader.GetType().GetMethod("LoadModel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(loadModel, Is.Not.Null, "AnimalModelLoader must retain its private load coroutine.");
                var loadRoutine = (IEnumerator)loadModel.Invoke(loader, null);

                Assert.That(loadRoutine.MoveNext(), Is.False);
                Assert.That(fallback.enabled, Is.True);
            }
            finally
            {
                Destroy(host);
            }
        }

        [Test]
        public void Retry_ClearsOwnedRuntimeRootRetainsHostTransformAndKeepsFallbackEnabledForMissingPath()
        {
            const string missingRelativePath = "Models/Test/MissingAnimal.glb";
            var host = new GameObject("Animal Host");
            host.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(20f, 30f, 40f));
            host.transform.localScale = new Vector3(0.4f, 0.5f, 0.6f);
            var expectedPosition = host.transform.position;
            var expectedRotation = host.transform.rotation;
            var expectedScale = host.transform.localScale;
            var fallback = host.AddComponent<MeshRenderer>();
            fallback.enabled = false;
            var runtimeRoot = new GameObject("Animal GLB Runtime Root");
            runtimeRoot.transform.SetParent(host.transform, false);

            try
            {
                var loader = AddGenericLoader(host);
                SetStreamingAssetPath(loader, missingRelativePath);
                var retry = loader.GetType().GetMethod("Retry");
                Assert.That(retry, Is.Not.Null, "AnimalModelLoader must expose Retry().");

                retry.Invoke(loader, null);

                Assert.That(host.transform.Find("Animal GLB Runtime Root"), Is.Null);
                Assert.That(host.transform.position, Is.EqualTo(expectedPosition));
                Assert.That(host.transform.rotation, Is.EqualTo(expectedRotation));
                Assert.That(host.transform.localScale, Is.EqualTo(expectedScale));
                Assert.That(fallback.enabled, Is.True);
            }
            finally
            {
                Destroy(host);
            }
        }

        [Test]
        public void HasVisibleRenderer_DestroyedRootReturnsFalseWithoutThrowing()
        {
            var host = new GameObject("Destroyed Runtime Root");
            var destroyedRoot = host.transform;
            var loaderType = Type.GetType(LoaderTypeName);
            var method = loaderType?.GetMethod("HasVisibleRenderer", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(host);

            object result = null;
            Assert.DoesNotThrow(() => result = method.Invoke(null, new object[] { destroyedRoot }));
            Assert.That(result, Is.EqualTo(false));
        }

        [Test]
        public void GraphicsSettings_AlwaysIncludesGltfPbrShader()
        {
            const string gltfPbrShaderGuid = "99fa998bbbed3408aafa652b466d261d";
            var graphicsSettings = File.ReadAllText(Path.GetFullPath("ProjectSettings/GraphicsSettings.asset"));

            StringAssert.Contains(gltfPbrShaderGuid, graphicsSettings);
        }

        private static Component AddGenericLoader(GameObject host)
        {
            var loaderType = Type.GetType(LoaderTypeName);
            Assert.That(loaderType, Is.Not.Null, "AnimalModelLoader must exist in the runtime assembly.");
            return host.AddComponent(loaderType);
        }

        private static void Configure(Component loader, AnimalDefinition definition)
        {
            var configureMethod = loader.GetType().GetMethod("Configure", new[] { typeof(AnimalDefinition) });
            Assert.That(configureMethod, Is.Not.Null, "AnimalModelLoader must expose Configure(AnimalDefinition).");
            configureMethod.Invoke(loader, new object[] { definition });
        }

        private static string ReadLoadedAnimalId(Component loader)
        {
            var property = loader.GetType().GetProperty("LoadedAnimalId");
            Assert.That(property, Is.Not.Null, "AnimalModelLoader must expose LoadedAnimalId.");
            return (string)property.GetValue(loader);
        }

        private static IEnumerator InvokeLoadModel(Component loader)
        {
            var loadModel = loader.GetType().GetMethod("LoadModel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(loadModel, Is.Not.Null, "AnimalModelLoader must retain its private load coroutine.");
            return (IEnumerator)loadModel.Invoke(loader, null);
        }

        private static void InvokePrivate(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"AnimalModelLoader must retain its private {methodName} method.");
            method.Invoke(component, null);
        }

        private static void SetDefinitionPrefab(AnimalDefinition definition, GameObject prefab)
        {
            var serializedDefinition = new SerializedObject(definition);
            var modelPrefab = serializedDefinition.FindProperty("modelPrefab");
            Assert.That(modelPrefab, Is.Not.Null, "AnimalDefinition needs an optional modelPrefab field.");
            modelPrefab.objectReferenceValue = prefab;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStreamingAssetPath(Component loader, string path)
        {
            var serializedLoader = new SerializedObject(loader);
            serializedLoader.FindProperty("streamingAssetPath").stringValue = path;
            serializedLoader.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimalDefinition CreateDefinition(
            string animalId,
            string modelPath,
            string texturePath,
            Vector3 experiencePosition,
            Vector3 modelOffset,
            Vector3 modelScale)
        {
            var definition = ScriptableObject.CreateInstance<AnimalDefinition>();
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("animalId").stringValue = animalId;
            serializedDefinition.FindProperty("modelRelativePath").stringValue = modelPath;
            serializedDefinition.FindProperty("baseColorTextureRelativePath").stringValue = texturePath;
            serializedDefinition.FindProperty("experiencePosition").vector3Value = experiencePosition;
            serializedDefinition.FindProperty("modelLocalOffset").vector3Value = modelOffset;
            serializedDefinition.FindProperty("modelEulerAngles").vector3Value = new Vector3(10f, 20f, 30f);
            serializedDefinition.FindProperty("modelScale").vector3Value = modelScale;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            foreach (var instance in objects)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
