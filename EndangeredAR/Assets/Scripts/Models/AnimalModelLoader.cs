using System;
using System.Collections;
using System.IO;
using EndangeredAR.Animals;
using GLTFast;
using UnityEngine;

namespace EndangeredAR.Models
{
    public class AnimalModelLoader : MonoBehaviour
    {
        [SerializeField] private string streamingAssetPath;
        [SerializeField] private string baseColorTexturePath;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3 modelLocalPosition;
        [SerializeField] private Vector3 modelLocalRotation;
        [SerializeField] private Vector3 modelLocalScale = Vector3.one;
        [SerializeField] private bool hideFallbackRendererWhenLoaderExists = true;
        [SerializeField] private bool fixLegacyDemoPlacement;

        private const string ModelRootName = "Animal GLB Runtime Root";
        private Texture2D baseColorTexture;
        private Coroutine modelLoadRoutine;
        private string loadedModelPath;
        private GameObject loadedModelPrefab;
        private string loadedAnimalId;
        private CharacterCapabilityProfile loadedCapabilities;
        private bool loadPending;

        public string LoadedAnimalId => loadedAnimalId;
        public CharacterCapabilityProfile LoadedCapabilities => loadedCapabilities;

        public bool TryGetCurrentModelController(out AnimalModelController controller)
        {
            controller = null;
            if (string.IsNullOrWhiteSpace(loadedAnimalId))
            {
                return false;
            }

            var currentRoot = transform.Find(ModelRootName);
            if (currentRoot == null ||
                currentRoot.name != ModelRootName ||
                !currentRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            var candidate = currentRoot.GetComponentInChildren<AnimalModelController>(false);
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                !candidate.transform.IsChildOf(currentRoot) ||
                !string.Equals(candidate.SupportedAnimalId, loadedAnimalId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!candidate.IsAnimatorOwnedBy(currentRoot))
            {
                return false;
            }

            controller = candidate;
            return true;
        }

        private void Start()
        {
            DisableLegacyDemoPlacement();
            BeginLoad();
        }

        private void OnEnable()
        {
            if (loadPending)
            {
                BeginLoad();
            }
        }

        public void Configure(AnimalDefinition definition)
        {
            loadedAnimalId = definition == null ? null : definition.AnimalId;
            loadedCapabilities = definition == null ? null : definition.Capabilities;

            if (definition == null)
            {
                ApplyModelConfiguration(string.Empty, string.Empty, null);
                return;
            }

            modelLocalPosition = definition.ModelLocalOffset;
            modelLocalRotation = definition.ModelEulerAngles;
            modelLocalScale = definition.ModelScale;
            ApplyModelConfiguration(
                definition.ModelRelativePath,
                definition.BaseColorTextureRelativePath,
                definition.ModelPrefab);
        }

        [Obsolete("Use Configure(AnimalDefinition).")]
        public void ConfigureModel(string modelPath)
        {
            ConfigureModel(modelPath, string.Empty);
        }

        [Obsolete("Use Configure(AnimalDefinition).")]
        public void ConfigureModel(string modelPath, string texturePath)
        {
            ApplyModelConfiguration(modelPath, texturePath, null);
        }

        public void Retry()
        {
            baseColorTexture = null;
            loadedModelPath = null;
            loadedModelPrefab = null;
            StopCurrentLoad();
            ClearExistingModelRoot();
            ShowFallbackRenderers(true);
            BeginLoad();
        }

        private void ApplyModelConfiguration(string modelPath, string texturePath, GameObject configuredModelPrefab)
        {
            DisableLegacyDemoPlacement();
            streamingAssetPath = modelPath ?? string.Empty;
            baseColorTexturePath = texturePath ?? string.Empty;
            modelPrefab = configuredModelPrefab;
            baseColorTexture = null;
            loadedModelPath = null;
            loadedModelPrefab = null;

            StopCurrentLoad();
            ClearExistingModelRoot();
            ShowFallbackRenderers(true);
            BeginLoad();
        }

        private void DisableLegacyDemoPlacement()
        {
            if (fixLegacyDemoPlacement)
            {
                fixLegacyDemoPlacement = false;
            }
        }

        private void BeginLoad()
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(streamingAssetPath))
            {
                loadPending = false;
                return;
            }

            if (string.Equals(loadedModelPath, streamingAssetPath, StringComparison.OrdinalIgnoreCase) &&
                loadedModelPrefab == modelPrefab)
            {
                loadPending = false;
                return;
            }

            if (!isActiveAndEnabled)
            {
                loadPending = true;
                return;
            }

            loadPending = false;
            ShowFallbackRenderers(true);
            loadedModelPath = streamingAssetPath;
            loadedModelPrefab = modelPrefab;
            modelLoadRoutine = StartCoroutine(LoadModel());
        }

        private IEnumerator LoadModel()
        {
            ShowFallbackRenderers(true);

            if (GetComponent<AnimalGestureController>() == null)
            {
                gameObject.AddComponent<AnimalGestureController>();
            }

            if (TryInstantiateRiggedPrefab())
            {
                modelLoadRoutine = null;
                yield break;
            }

            if (!ModelFileExists(streamingAssetPath))
            {
                ShowFallbackRenderers(true);
                Debug.LogWarning($"AnimalModelLoader: Model for '{DisplayAnimalId()}' was not found at relative path '{DisplayRelativePath(streamingAssetPath)}'. Keeping the fallback model.");
                modelLoadRoutine = null;
                yield break;
            }

            var modelRoot = CreateModelRoot();

            var gltfAsset = modelRoot.AddComponent<GltfAsset>();
            gltfAsset.StreamingAsset = true;
            gltfAsset.Url = streamingAssetPath;
            gltfAsset.LoadOnStartup = true;

            if (!hideFallbackRendererWhenLoaderExists)
            {
                modelLoadRoutine = null;
                yield break;
            }

            yield return StartCoroutine(HideFallbackWhenModelHasRendered(modelRoot.transform));
            modelLoadRoutine = null;
        }

        private bool TryInstantiateRiggedPrefab()
        {
            if (modelPrefab == null)
            {
                return false;
            }

            if (!HasEnabledRenderer(modelPrefab.transform))
            {
                Debug.LogWarning($"AnimalModelLoader: Rigged prefab for '{DisplayAnimalId()}' has no enabled renderer. Falling back to the GLB model.");
                return false;
            }

            var modelRoot = CreateModelRoot();
            try
            {
                Instantiate(modelPrefab, modelRoot.transform, false);
            }
            catch (Exception exception)
            {
                DestroyOwnedObject(modelRoot);
                Debug.LogWarning($"AnimalModelLoader: Rigged prefab for '{DisplayAnimalId()}' could not be instantiated ({exception.GetType().Name}). Falling back to the GLB model.");
                return false;
            }

            if (!HasVisibleRenderer(modelRoot.transform))
            {
                DestroyOwnedObject(modelRoot);
                Debug.LogWarning($"AnimalModelLoader: Rigged prefab for '{DisplayAnimalId()}' produced no visible renderer. Falling back to the GLB model.");
                return false;
            }

            if (hideFallbackRendererWhenLoaderExists)
            {
                ShowFallbackRenderers(false);
            }

            return true;
        }

        private GameObject CreateModelRoot()
        {
            var modelRoot = new GameObject(ModelRootName);
            modelRoot.transform.SetParent(transform, false);
            modelRoot.transform.localPosition = modelLocalPosition;
            modelRoot.transform.localEulerAngles = modelLocalRotation;
            modelRoot.transform.localScale = modelLocalScale;
            return modelRoot;
        }

        private static bool HasEnabledRenderer(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            foreach (var rendererComponent in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rendererComponent != null && rendererComponent.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyOwnedObject(GameObject ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }

        private IEnumerator HideFallbackWhenModelHasRendered(Transform modelRoot)
        {
            const float timeoutSeconds = 8f;
            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (modelRoot == null)
                {
                    yield break;
                }

                if (HasVisibleRenderer(modelRoot))
                {
                    RepairMagentaMaterials(modelRoot);

                    foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
                    {
                        if (rendererComponent.transform == modelRoot || rendererComponent.transform.IsChildOf(modelRoot))
                        {
                            continue;
                        }

                        rendererComponent.enabled = false;
                    }

                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning($"AnimalModelLoader: Model for '{DisplayAnimalId()}' did not render from relative path '{DisplayRelativePath(streamingAssetPath)}'. Keeping the capsule fallback model.");
        }

        private void StopCurrentLoad()
        {
            if (modelLoadRoutine != null)
            {
                StopCoroutine(modelLoadRoutine);
                modelLoadRoutine = null;
            }
        }

        private void ClearExistingModelRoot()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != ModelRootName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    child.name = ModelRootName + " (Pending Destroy)";
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ShowFallbackRenderers(bool visible)
        {
            var modelRoot = transform.Find(ModelRootName);
            foreach (var rendererComponent in GetComponentsInChildren<Renderer>(true))
            {
                if (modelRoot != null && (rendererComponent.transform == modelRoot || rendererComponent.transform.IsChildOf(modelRoot)))
                {
                    continue;
                }

                rendererComponent.enabled = visible;
            }
        }

        private static bool ModelFileExists(string relativeStreamingAssetPath)
        {
            return TryGetStreamingAssetFilePath(relativeStreamingAssetPath, out var fullPath) && File.Exists(fullPath);
        }

        private static bool HasVisibleRenderer(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            foreach (var rendererComponent in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rendererComponent != null && rendererComponent.enabled && rendererComponent.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private void RepairMagentaMaterials(Transform root)
        {
            if (baseColorTexture == null)
            {
                baseColorTexture = LoadBaseColorTexture();
            }

            foreach (var rendererComponent in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = rendererComponent.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    if (IsBrokenMaterial(material) || baseColorTexture != null)
                    {
                        materials[i] = CreateFallbackMaterial(material, baseColorTexture);
                    }
                }

                rendererComponent.materials = materials;
            }
        }

        private static bool IsBrokenMaterial(Material material)
        {
            return material.shader == null ||
                   material.shader.name == "Hidden/InternalErrorShader" ||
                   IsNearlyMagenta(material.color);
        }

        private static bool IsNearlyMagenta(Color color)
        {
            return color.r > 0.8f && color.g < 0.2f && color.b > 0.8f;
        }

        private static Material CreateFallbackMaterial(Material source, Texture baseColorTexture)
        {
            var shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Sprites/Default");

            var material = new Material(shader)
            {
                name = $"{source.name} Fixed",
                color = baseColorTexture == null ? GetSourceColor(source) : Color.white
            };

            var mainTexture = baseColorTexture != null ? baseColorTexture : GetSourceTexture(source);
            if (mainTexture != null)
            {
                SetTextureIfPresent(material, "_BaseMap", mainTexture);
                SetTextureIfPresent(material, "_MainTex", mainTexture);
            }

            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.35f);
            return material;
        }

        private static Color GetSourceColor(Material source)
        {
            if (source.HasProperty("_BaseColor"))
            {
                return source.GetColor("_BaseColor");
            }

            if (source.HasProperty("_Color") && !IsNearlyMagenta(source.GetColor("_Color")))
            {
                return source.GetColor("_Color");
            }

            return new Color(0.72f, 0.58f, 0.42f, 1f);
        }

        private static Texture GetSourceTexture(Material source)
        {
            var textureNames = source.GetTexturePropertyNames();
            foreach (var textureName in textureNames)
            {
                var texture = source.GetTexture(textureName);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private Texture2D LoadBaseColorTexture()
        {
            if (!TryGetStreamingAssetFilePath(baseColorTexturePath, out var fullPath))
            {
                return null;
            }

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"AnimalModelLoader: Base color texture for '{DisplayAnimalId()}' was not found at relative path '{DisplayRelativePath(baseColorTexturePath)}'.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Debug.LogWarning($"AnimalModelLoader: Failed to load base color texture for '{DisplayAnimalId()}' from relative path '{DisplayRelativePath(baseColorTexturePath)}'.");
                return null;
            }

            texture.name = "Animal Base Color";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            return texture;
        }

        private static bool TryGetStreamingAssetFilePath(string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                return false;
            }

            var streamingAssetsDirectory = Path.GetFullPath(Application.streamingAssetsPath);
            var candidatePath = Path.GetFullPath(Path.Combine(streamingAssetsDirectory, relativePath));
            var directoryPrefix = streamingAssetsDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? streamingAssetsDirectory
                : streamingAssetsDirectory + Path.DirectorySeparatorChar;
            if (!candidatePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidatePath;
            return true;
        }

        private string DisplayAnimalId()
        {
            return string.IsNullOrWhiteSpace(loadedAnimalId) ? "unknown" : loadedAnimalId;
        }

        private static string DisplayRelativePath(string path)
        {
            return TryGetStreamingAssetFilePath(path, out _) ? path : "<invalid>";
        }
    }
}
