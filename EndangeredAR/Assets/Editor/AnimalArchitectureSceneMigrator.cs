using System;
using System.Collections.Generic;
using System.Linq;
using EndangeredAR.AI;
using EndangeredAR.API;
using EndangeredAR.AR;
using EndangeredAR.Animals;
using EndangeredAR.Chat;
using EndangeredAR.Missions;
using EndangeredAR.Models;
using EndangeredAR.Progress;
using EndangeredAR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndangeredAR.Editor
{
    public static class AnimalArchitectureSceneMigrator
    {
        private const string DemoScenePath = "Assets/Scenes/DemoScene.unity";
        private const string SensenDefinitionPath = "Assets/Resources/Animals/Sensen.asset";
        private const string AIConfigPath = "Assets/Config/LocalAIConfig.asset";

        [MenuItem("Endangered AR/Migrate Demo Scene Animal Architecture")]
        public static void MigrateDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            var rectTransformBaseline = CaptureHierarchy<RectTransform>(scene);
            var canvasBaseline = CaptureHierarchy<Canvas>(scene);
            var changed = false;

            var demo = FindSingleRequired<DemoAppController>(scene, "Demo App Controller");
            var scanner = FindSingleRequired<ARImageScanController>(scene, "AR Image Scan Controller");
            var mission = FindSingleRequired<MissionController>(scene, "Mission Controller");
            var chatApi = FindSingleRequired<ChatApiClient>(scene, "Chat API Client");
            var localChat = FindSingleRequired<LocalKnowledgeChatService>(scene, "Local Knowledge Chat Service");
            var modelLoader = FindSingleRequired<AnimalModelLoader>(scene, "Sensen Placeholder AnimalModelLoader");
            var sensen = AssetDatabase.LoadAssetAtPath<AnimalDefinition>(SensenDefinitionPath);
            if (sensen == null)
            {
                throw new InvalidOperationException($"Missing required animal definition at {SensenDefinitionPath}.");
            }

            var catalog = GetOrCreateRootComponent<AnimalCatalogService>(scene, "Animal Catalog Service", ref changed);
            var progress = GetOrCreateRootComponent<AnimalProgressService>(scene, "Animal Progress Service", ref changed);
            var experience = GetOrCreateRootComponent<AnimalExperienceController>(scene, "Animal Experience Controller", ref changed);
            var aiManager = GetOrCreateRootComponent<AIManager>(scene, "AI Manager", ref changed);
            var aiConfig = GetOrCreateAIConfig();

            changed |= ConfigureCatalog(catalog, sensen);
            changed |= ConfigureScanner(scanner);
            changed |= SetReference(localChat, "defaultProfile", sensen.Knowledge);

            changed |= SetReferences(experience, new Dictionary<string, UnityEngine.Object>
            {
                { "animalCatalogService", catalog },
                { "animalProgressService", progress },
                { "missionController", mission },
                { "modelLoader", modelLoader },
                { "experienceHostTransform", modelLoader.transform }
            });

            changed |= SetReferences(aiManager, new Dictionary<string, UnityEngine.Object>
            {
                { "aiConfig", aiConfig },
                { "chatApiClient", chatApi },
                { "localKnowledgeService", localChat }
            });

            changed |= SetReferences(demo, new Dictionary<string, UnityEngine.Object>
            {
                { "scanController", scanner },
                { "aiManager", aiManager },
                { "localChatService", localChat },
                { "missionController", mission },
                { "animalCatalog", catalog },
                { "animalProgress", progress },
                { "animalExperience", experience },
                { "animalPlaceholder", modelLoader.gameObject }
            });

            AssertHierarchyUnchanged(rectTransformBaseline, CaptureHierarchy<RectTransform>(scene), "RectTransform");
            AssertHierarchyUnchanged(canvasBaseline, CaptureHierarchy<Canvas>(scene), "Canvas");

            if (changed)
            {
                EditorSceneManager.SaveScene(scene, DemoScenePath);
            }

            Debug.Log(changed
                ? "AnimalArchitectureSceneMigrator: migrated DemoScene with animal and AI services plus serialized references."
                : "AnimalArchitectureSceneMigrator: DemoScene already matches the animal architecture; no changes saved.");
        }

        private static AIConfig GetOrCreateAIConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AIConfig>(AIConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<AIConfig>();
            AssetDatabase.CreateAsset(config, AIConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static bool ConfigureCatalog(AnimalCatalogService catalog, AnimalDefinition sensen)
        {
            var serialized = new SerializedObject(catalog);
            var definitions = RequireProperty(serialized, "definitions");
            var defaultAnimalId = RequireProperty(serialized, "defaultAnimalId");
            var changed = definitions.arraySize != 1 ||
                          definitions.GetArrayElementAtIndex(0).objectReferenceValue != sensen ||
                          defaultAnimalId.stringValue != sensen.AnimalId;
            if (!changed)
            {
                return false;
            }

            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = sensen;
            defaultAnimalId.stringValue = sensen.AnimalId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool ConfigureScanner(ARImageScanController scanner)
        {
            var serialized = new SerializedObject(scanner);
            var defaultAnimalId = RequireProperty(serialized, "defaultAnimalId");
            var mappings = RequireProperty(serialized, "markerAnimals");
            var mappingMatches = mappings.arraySize == 1 &&
                                 mappings.GetArrayElementAtIndex(0).FindPropertyRelative("markerName").stringValue == "sensen_marker" &&
                                 mappings.GetArrayElementAtIndex(0).FindPropertyRelative("animalId").stringValue == "sensen";
            if (defaultAnimalId.stringValue == "sensen" && mappingMatches)
            {
                return false;
            }

            defaultAnimalId.stringValue = "sensen";
            mappings.arraySize = 1;
            mappings.GetArrayElementAtIndex(0).FindPropertyRelative("markerName").stringValue = "sensen_marker";
            mappings.GetArrayElementAtIndex(0).FindPropertyRelative("animalId").stringValue = "sensen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetReferences(Component component, IReadOnlyDictionary<string, UnityEngine.Object> references)
        {
            var serialized = new SerializedObject(component);
            var changed = false;
            foreach (var reference in references)
            {
                var property = RequireProperty(serialized, reference.Key);
                if (property.objectReferenceValue == reference.Value)
                {
                    continue;
                }

                property.objectReferenceValue = reference.Value;
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static bool SetReference(Component component, string propertyName, UnityEngine.Object value)
        {
            return SetReferences(component, new Dictionary<string, UnityEngine.Object>
            {
                { propertyName, value }
            });
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{serialized.targetObject.GetType().Name} is missing serialized property '{propertyName}'.");
            }

            return property;
        }

        private static T GetOrCreateRootComponent<T>(Scene scene, string gameObjectName, ref bool changed)
            where T : Component
        {
            var existing = FindComponents<T>(scene);
            if (existing.Count > 1)
            {
                throw new InvalidOperationException($"DemoScene contains multiple {typeof(T).Name} components.");
            }

            if (existing.Count == 1)
            {
                if (existing[0].transform.parent != null)
                {
                    throw new InvalidOperationException($"Existing {typeof(T).Name} must remain a root GameObject.");
                }

                return existing[0];
            }

            var root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == gameObjectName);
            if (root == null)
            {
                root = new GameObject(gameObjectName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var component = root.GetComponent<T>();
            if (component == null)
            {
                component = root.AddComponent<T>();
            }

            changed = true;
            return component;
        }

        private static T FindSingleRequired<T>(Scene scene, string description) where T : Component
        {
            var components = FindComponents<T>(scene);
            if (components.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {description}, found {components.Count}. Migration stopped without rebuilding the scene.");
            }

            return components[0];
        }

        private static List<T> FindComponents<T>(Scene scene) where T : Component
        {
            var components = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return components;
        }

        private static string[] CaptureHierarchy<T>(Scene scene) where T : Component
        {
            return FindComponents<T>(scene)
                .Select(component => GetHierarchyPath(component.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }

        private static void AssertHierarchyUnchanged(string[] before, string[] after, string componentName)
        {
            if (!before.SequenceEqual(after, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Migration changed the {componentName} hierarchy. DemoScene was not saved.");
            }
        }
    }
}
