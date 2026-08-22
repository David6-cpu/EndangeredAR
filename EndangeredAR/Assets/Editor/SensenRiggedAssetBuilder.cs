using System;
using System.Linq;
using EndangeredAR.Animals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EndangeredAR.Editor
{
    public static class SensenRiggedAssetBuilder
    {
        public const string ModelPath = "Assets/Art/Characters/Sensen/Rigged/Models/sensen_rigged_100k.fbx";
        public const string TexturePath = "Assets/Art/Characters/Sensen/Rigged/Textures/sensen_basecolor_1024.png";
        public const string MaterialPath = "Assets/Art/Characters/Sensen/Rigged/Materials/SensenRigged.mat";
        public const string ControllerPath = "Assets/Animations/Sensen/SensenRigged.controller";
        public const string PrefabPath = "Assets/Prefabs/Animals/SensenRigged.prefab";
        private const string DefinitionPath = "Assets/Resources/Animals/Sensen.asset";
        private const float TargetCharacterHeight = 1.15f;

        [MenuItem("Endangered AR/Characters/Rebuild Rigged Sensen")]
        public static void Build()
        {
            RequireAsset(ModelPath);
            RequireAsset(TexturePath);
            EnsureParentFolders(MaterialPath);
            EnsureParentFolders(ControllerPath);
            EnsureParentFolders(PrefabPath);

            ConfigureTextureImporter();
            ConfigureModelImporter();

            var material = CreateOrUpdateMaterial();
            var controller = CreateOrUpdateController();
            var prefab = CreateOrUpdatePrefab(material, controller);
            AssignDefinitionPrefab(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt the optimized rigged Sensen character foundation.");
        }

        private static void ConfigureTextureImporter()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter was not found for '{TexturePath}'.");
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            SetMobileTextureSettings(importer, "iPhone");
            SetMobileTextureSettings(importer, "Android");
            importer.SaveAndReimport();
        }

        private static void SetMobileTextureSettings(TextureImporter importer, string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 70;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"ModelImporter was not found for '{ModelPath}'.");
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importConstraints = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeGameObjects = false;
            importer.addCollider = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.clipAnimations = BuildClipSettings(importer);
            importer.SaveAndReimport();
        }

        private static ModelImporterClipAnimation[] BuildClipSettings(ModelImporter importer)
        {
            var source = importer.clipAnimations != null && importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            if (source == null || source.Length < 2)
            {
                throw new InvalidOperationException("The approved Sensen FBX must contain Idle and Taunt clips.");
            }

            var clips = source
                .Where(clip => IsClip(clip, "Idle") || IsClip(clip, "Taunt"))
                .ToArray();
            if (clips.Length != 2)
            {
                throw new InvalidOperationException("Exactly one Idle and one Taunt clip are required.");
            }

            foreach (var clip in clips)
            {
                var idle = IsClip(clip, "Idle");
                clip.name = idle ? "Idle" : "Taunt";
                clip.loopTime = idle;
                clip.loopPose = idle;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootRotation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
            }

            return clips;
        }

        private static bool IsClip(ModelImporterClipAnimation clip, string suffix)
        {
            return (!string.IsNullOrEmpty(clip.name) && clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(clip.takeName) && clip.takeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        private static Material CreateOrUpdateMaterial()
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("The Built-in Standard shader is unavailable.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SensenRigged" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.color = Color.white;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.3f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController CreateOrUpdateController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var idleClip = FindClip(clips, "Idle");
            var tauntClip = FindClip(clips, "Taunt");
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Taunt", AnimatorControllerParameterType.Trigger);

            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(child.state);
            }

            foreach (var transition in stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            var idle = stateMachine.AddState("Idle");
            idle.motion = idleClip;
            var taunt = stateMachine.AddState("Taunt");
            taunt.motion = tauntClip;
            stateMachine.defaultState = idle;

            var toTaunt = idle.AddTransition(taunt);
            toTaunt.hasExitTime = false;
            toTaunt.duration = 0.1f;
            toTaunt.canTransitionToSelf = false;
            toTaunt.AddCondition(AnimatorConditionMode.If, 0f, "Taunt");

            var toIdle = taunt.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.95f;
            toIdle.duration = 0.12f;
            toIdle.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreateOrUpdatePrefab(Material material, AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException("The optimized Sensen model asset could not be loaded.");
            }

            var prefabRoot = new GameObject("SensenRigged");
            try
            {
                var riggedModelRoot = new GameObject("RiggedModelRoot");
                riggedModelRoot.transform.SetParent(prefabRoot.transform, false);
                var optimizedModel = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (optimizedModel == null)
                {
                    throw new InvalidOperationException("The optimized FBX could not be instantiated.");
                }

                optimizedModel.name = "OptimizedModel";
                optimizedModel.transform.SetParent(riggedModelRoot.transform, false);
                optimizedModel.transform.localPosition = Vector3.zero;
                optimizedModel.transform.localRotation = Quaternion.identity;
                optimizedModel.transform.localScale = Vector3.one;

                var renderers = optimizedModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1)
                {
                    throw new InvalidOperationException($"Expected one SkinnedMeshRenderer, found {renderers.Length}.");
                }

                renderers[0].sharedMaterial = material;
                var animator = optimizedModel.GetComponent<Animator>() ?? optimizedModel.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.avatar == null)
                {
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                        .OfType<Avatar>()
                        .FirstOrDefault(avatar => avatar.isValid);
                }

                var animationController = prefabRoot.AddComponent<AnimalModelController>();
                var serializedAnimationController = new SerializedObject(animationController);
                serializedAnimationController.FindProperty("animator").objectReferenceValue = animator;
                serializedAnimationController.FindProperty("supportedAnimalId").stringValue = "sensen";
                serializedAnimationController.ApplyModifiedPropertiesWithoutUndo();

                var bounds = renderers[0].bounds;
                if (bounds.size.y <= 0.0001f)
                {
                    throw new InvalidOperationException("The optimized Sensen renderer has invalid bounds.");
                }

                var normalizationScale = TargetCharacterHeight / bounds.size.y;
                riggedModelRoot.transform.localScale = Vector3.one * normalizationScale;
                riggedModelRoot.transform.localPosition = new Vector3(
                    -bounds.center.x * normalizationScale,
                    -bounds.min.y * normalizationScale,
                    -bounds.center.z * normalizationScale);

                var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("SensenRigged.prefab could not be saved.");
                }

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static void AssignDefinitionPrefab(GameObject prefab)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DefinitionPath);
            if (definition == null)
            {
                throw new InvalidOperationException("Sensen AnimalDefinition was not found.");
            }

            var serialized = new SerializedObject(definition);
            var property = serialized.FindProperty("modelPrefab");
            if (property == null)
            {
                throw new InvalidOperationException("AnimalDefinition.modelPrefab is unavailable.");
            }

            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static AnimationClip FindClip(AnimationClip[] clips, string suffix)
        {
            var clip = clips.SingleOrDefault(value => value.name.Equals(suffix, StringComparison.OrdinalIgnoreCase));
            if (clip == null)
            {
                throw new InvalidOperationException($"Animation clip ending with '{suffix}' was not imported.");
            }

            return clip;
        }

        private static void RequireAsset(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new InvalidOperationException($"Required approved asset is missing: {path}");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureParentFolders(string assetPath)
        {
            var folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolder(folder);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
