using System.Collections;
using System.IO;
using EndangeredAR.Animals;
using GLTFast;
using UnityEngine;

namespace EndangeredAR.Models
{
    public class SensenGlbLoader : MonoBehaviour
    {
        [SerializeField] private string streamingAssetPath = "Models/Sensen/sensen.glb";
        [SerializeField] private string baseColorTexturePath = "Models/Sensen/sensen_basecolor.png";
        [SerializeField] private Vector3 modelLocalPosition = new Vector3(0f, 0.04f, 0f);
        [SerializeField] private Vector3 modelLocalRotation = new Vector3(0f, 180f, 0f);
        [SerializeField] private Vector3 modelLocalScale = new Vector3(1.45f, 1.45f, 1.45f);
        [SerializeField] private bool hideFallbackRendererWhenLoaderExists = true;
        [SerializeField] private bool fixLegacyDemoPlacement = true;

        private const string ModelRootName = "Sensen GLB Runtime Root";
        private Texture2D baseColorTexture;
        private Coroutine modelLoadRoutine;
        private string loadedModelPath;

        private void Start()
        {
            ApplyDemoPlacement();
            ConfigureModel(streamingAssetPath, baseColorTexturePath);
        }

        public void ConfigureModel(string modelPath)
        {
            var texturePath = modelPath != null && modelPath.Contains("Sensen")
                ? "Models/Sensen/sensen_basecolor.png"
                : string.Empty;
            ConfigureModel(modelPath, texturePath);
        }

        public void ConfigureModel(string modelPath, string texturePath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                ShowFallbackRenderers(true);
                return;
            }

            if (string.Equals(loadedModelPath, modelPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            streamingAssetPath = modelPath;
            baseColorTexturePath = texturePath ?? string.Empty;
            baseColorTexture = null;
            loadedModelPath = modelPath;

            if (modelLoadRoutine != null)
            {
                StopCoroutine(modelLoadRoutine);
            }

            ClearExistingModelRoot();
            ShowFallbackRenderers(true);
            ApplyDemoPlacement();
            modelLoadRoutine = StartCoroutine(LoadModel());
        }

        private IEnumerator LoadModel()
        {
            if (GetComponent<AnimalGestureController>() == null)
            {
                gameObject.AddComponent<AnimalGestureController>();
            }

            if (!ModelFileExists(streamingAssetPath))
            {
                Debug.LogWarning($"SensenGlbLoader: Model not found at {streamingAssetPath}. Keeping the fallback model.");
                modelLoadRoutine = null;
                yield break;
            }

            var modelRoot = new GameObject(ModelRootName);
            modelRoot.transform.SetParent(transform, false);
            modelRoot.transform.localPosition = ResolveModelLocalPosition();
            modelRoot.transform.localEulerAngles = modelLocalRotation;
            modelRoot.transform.localScale = ResolveModelLocalScale();

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

        private IEnumerator HideFallbackWhenModelHasRendered(Transform modelRoot)
        {
            const float timeoutSeconds = 8f;
            var startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
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

            Debug.LogWarning($"SensenGlbLoader: Could not display {streamingAssetPath}. Keeping the capsule fallback model.");
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

                Destroy(child.gameObject);
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
            if (string.IsNullOrWhiteSpace(relativeStreamingAssetPath))
            {
                return false;
            }

            return File.Exists(Path.Combine(Application.streamingAssetsPath, relativeStreamingAssetPath));
        }

        private static bool HasVisibleRenderer(Transform root)
        {
            foreach (var rendererComponent in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rendererComponent.enabled && rendererComponent.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyDemoPlacement()
        {
            if (!fixLegacyDemoPlacement)
            {
                return;
            }

            if (transform.position.y < 0.62f || transform.position.y > 0.88f)
            {
                transform.position = new Vector3(0f, 0.72f, 0f);
            }

            if (transform.localScale.x < 1f || transform.localScale.y < 1f || transform.localScale.z < 1f)
            {
                transform.localScale = Vector3.one;
            }
        }

        private Vector3 ResolveModelLocalPosition()
        {
            return fixLegacyDemoPlacement && modelLocalPosition.y < -0.2f
                ? new Vector3(0f, 0.04f, 0f)
                : modelLocalPosition;
        }

        private Vector3 ResolveModelLocalScale()
        {
            return fixLegacyDemoPlacement && modelLocalScale.x < 1f
                ? new Vector3(1.45f, 1.45f, 1.45f)
                : modelLocalScale;
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
            if (string.IsNullOrWhiteSpace(baseColorTexturePath))
            {
                return null;
            }

            var fullPath = Path.Combine(Application.streamingAssetsPath, baseColorTexturePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"SensenGlbLoader: Base color texture not found at {fullPath}.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Debug.LogWarning($"SensenGlbLoader: Failed to load base color texture at {fullPath}.");
                return null;
            }

            texture.name = "Sensen Base Color";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            return texture;
        }
    }
}
