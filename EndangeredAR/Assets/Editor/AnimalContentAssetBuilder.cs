using System;
using System.IO;
using EndangeredAR.Animals;
using EndangeredAR.Missions;
using UnityEditor;
using UnityEngine;

namespace EndangeredAR.Editor
{
    public static class AnimalContentAssetBuilder
    {
        private const string AnimalsFolder = "Assets/Resources/Animals";
        private const string KnowledgePath = AnimalsFolder + "/SensenKnowledge.asset";
        private const string MissionPath = AnimalsFolder + "/SensenMission.asset";
        private const string DefinitionPath = AnimalsFolder + "/Sensen.asset";

        [MenuItem("Endangered AR/Data/Rebuild Sensen Content")]
        public static void RebuildSensenContent()
        {
            var document = LoadCanonicalDocument();
            var knowledge = LoadOrCreate<AnimalKnowledgeProfile>(KnowledgePath);
            var mission = LoadOrCreate<MissionDefinition>(MissionPath);
            var definition = LoadOrCreate<AnimalDefinition>(DefinitionPath);

            ConfigureKnowledge(knowledge, document);
            ConfigureMission(mission);
            ConfigureDefinition(definition, knowledge, mission, document);

            EditorUtility.SetDirty(knowledge);
            EditorUtility.SetDirty(mission);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            Debug.Log("Rebuilt audited Sensen content assets.");
        }

        private static void ConfigureKnowledge(AnimalKnowledgeProfile knowledge, CanonicalAnimalDocument document)
        {
            var habitat = FindFact(document, "sensen.habitat");
            var diet = FindFact(document, "sensen.diet");
            var threats = FindFact(document, "sensen.threats");
            var youthActions = FindFact(document, "sensen.youth_actions");
            var status = FindFact(document, "sensen.conservation_status");
            var serialized = new SerializedObject(knowledge);
            serialized.FindProperty("endangeredLevel").stringValue = FirstSegment(status.displayValue);
            serialized.FindProperty("habitat").stringValue = habitat.displayValue;
            serialized.FindProperty("food").stringValue = diet.displayValue;
            SetStringArray(serialized.FindProperty("threats"), threats.items);
            SetStringArray(serialized.FindProperty("protectionActions"), youthActions.items);
            SetStringArray(
                serialized.FindProperty("dailyFacts"),
                diet.claim,
                habitat.claim,
                threats.claim);
            SetKnowledgeEntries(serialized.FindProperty("entries"), document.facts, document.presentation.defaultSuggestions);
            SetKnowledgeSources(serialized.FindProperty("sources"), document.sources);
            serialized.FindProperty("unknownReply").stringValue = document.presentation.unknownReply;
            SetStringArray(serialized.FindProperty("defaultSuggestions"), document.presentation.defaultSuggestions);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMission(MissionDefinition mission)
        {
            var serialized = new SerializedObject(mission);
            serialized.FindProperty("missionId").stringValue = "sensen-food";
            serialized.FindProperty("title").stringValue = "帮森森寻找食物";
            serialized.FindProperty("prompt").stringValue = "请选择森森能吃的天然食物。";
            SetMissionOptions(serialized.FindProperty("options"));
            serialized.FindProperty("correctFeedback").stringValue = "答对了！嫩叶和花朵都是森森的天然食物。";
            serialized.FindProperty("wrongFeedback").stringValue = "人类零食和塑料不是森森的食物。";
            serialized.FindProperty("learnedKnowledgeId").stringValue = "food";
            serialized.FindProperty("learnedFact").stringValue = "天然的嫩叶和花朵适合森森。";
            serialized.FindProperty("badgeId").stringValue = "eco-guardian-sensen";
            serialized.FindProperty("points").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDefinition(
            AnimalDefinition definition,
            AnimalKnowledgeProfile knowledge,
            MissionDefinition mission,
            CanonicalAnimalDocument document)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("animalId").stringValue = document.animalId;
            serialized.FindProperty("displayName").stringValue = $"{document.identity.chineseName} {document.identity.nickname}";
            serialized.FindProperty("shortName").stringValue = document.identity.nickname;
            serialized.FindProperty("scientificName").stringValue = document.identity.scientificName;
            serialized.FindProperty("markerName").stringValue = "sensen_marker";
            serialized.FindProperty("modelRelativePath").stringValue = "Models/Sensen/sensen.glb";
            serialized.FindProperty("baseColorTextureRelativePath").stringValue = "Models/Sensen/sensen_basecolor.png";
            serialized.FindProperty("experiencePosition").vector3Value = new Vector3(-1.02f, -0.13f, 0f);
            serialized.FindProperty("modelLocalOffset").vector3Value = new Vector3(0f, 0.04f, 0f);
            serialized.FindProperty("modelEulerAngles").vector3Value = new Vector3(0f, 180f, 0f);
            serialized.FindProperty("modelScale").vector3Value = new Vector3(1.45f, 1.45f, 1.45f);
            serialized.FindProperty("welcomeText").stringValue = document.presentation.welcomeText;
            serialized.FindProperty("themeColor").colorValue = Color.white;
            serialized.FindProperty("portrait").objectReferenceValue = null;
            serialized.FindProperty("lockedSilhouette").objectReferenceValue = null;
            serialized.FindProperty("knowledge").objectReferenceValue = knowledge;
            serialized.FindProperty("mission").objectReferenceValue = mission;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetKnowledgeEntries(
            SerializedProperty entries,
            CanonicalFact[] facts,
            string[] suggestedQuestions)
        {
            facts = facts ?? Array.Empty<CanonicalFact>();
            entries.arraySize = facts.Length;
            for (var index = 0; index < facts.Length; index++)
            {
                SetKnowledgeEntry(entries.GetArrayElementAtIndex(index), facts[index], suggestedQuestions);
            }
        }

        private static void SetKnowledgeEntry(
            SerializedProperty entry,
            CanonicalFact fact,
            string[] suggestedQuestions)
        {
            entry.FindPropertyRelative("knowledgeId").stringValue = fact.factId;
            entry.FindPropertyRelative("topic").stringValue = fact.topic;
            entry.FindPropertyRelative("claim").stringValue = fact.claim;
            SetStringArray(entry.FindPropertyRelative("keywords"), fact.keywords);
            SetStringArray(entry.FindPropertyRelative("aliases"), fact.aliases);
            entry.FindPropertyRelative("reply").stringValue = fact.approvedAnswer;
            entry.FindPropertyRelative("displayValue").stringValue = fact.displayValue;
            SetStringArray(entry.FindPropertyRelative("items"), fact.items);
            SetStringArray(entry.FindPropertyRelative("sourceIds"), fact.sourceIds);
            entry.FindPropertyRelative("confidence").stringValue = fact.confidence;
            entry.FindPropertyRelative("evidenceStatus").stringValue = fact.evidenceStatus;
            entry.FindPropertyRelative("lastVerified").stringValue = fact.lastVerified;
            entry.FindPropertyRelative("notes").stringValue = fact.notes;
            SetStringArray(entry.FindPropertyRelative("suggestedQuestions"), suggestedQuestions);
        }

        private static void SetKnowledgeSources(SerializedProperty sources, CanonicalSource[] values)
        {
            values = values ?? Array.Empty<CanonicalSource>();
            sources.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                var source = sources.GetArrayElementAtIndex(index);
                var value = values[index];
                source.FindPropertyRelative("sourceId").stringValue = value.sourceId;
                source.FindPropertyRelative("title").stringValue = value.title;
                source.FindPropertyRelative("organization").stringValue = value.organization;
                source.FindPropertyRelative("sourceType").stringValue = value.sourceType;
                source.FindPropertyRelative("url").stringValue = value.url;
                source.FindPropertyRelative("publishedOrUpdatedDate").stringValue = value.publishedOrUpdatedDate;
                source.FindPropertyRelative("projectVerifiedDate").stringValue = value.projectVerifiedDate;
                SetStringArray(source.FindPropertyRelative("appliesToFactIds"), value.appliesToFactIds);
                source.FindPropertyRelative("notes").stringValue = value.notes;
            }
        }

        private static void SetMissionOptions(SerializedProperty options)
        {
            options.arraySize = 4;
            SetMissionOption(options.GetArrayElementAtIndex(0), "leaf", "嫩叶", true);
            SetMissionOption(options.GetArrayElementAtIndex(1), "flower", "花朵", true);
            SetMissionOption(options.GetArrayElementAtIndex(2), "snack", "人类零食", false);
            SetMissionOption(options.GetArrayElementAtIndex(3), "plastic", "塑料", false);
        }

        private static void SetMissionOption(SerializedProperty option, string optionId, string label, bool isCorrect)
        {
            option.FindPropertyRelative("optionId").stringValue = optionId;
            option.FindPropertyRelative("label").stringValue = label;
            option.FindPropertyRelative("isCorrect").boolValue = isCorrect;
        }

        private static void SetStringArray(SerializedProperty property, params string[] values)
        {
            values = values ?? Array.Empty<string>();
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }

        private static CanonicalAnimalDocument LoadCanonicalDocument()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var path = Path.Combine(repositoryRoot, "content", "animals", "sensen.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Canonical Sensen knowledge was not found.", path);
            }

            var document = JsonUtility.FromJson<CanonicalAnimalDocument>(File.ReadAllText(path));
            if (document == null || document.schemaVersion != 1 || document.animalId != "sensen")
            {
                throw new InvalidDataException("Canonical Sensen knowledge has an unsupported schema or identity.");
            }

            return document;
        }

        private static CanonicalFact FindFact(CanonicalAnimalDocument document, string factId)
        {
            var fact = Array.Find(document.facts ?? Array.Empty<CanonicalFact>(), value => value != null && value.factId == factId);
            if (fact == null)
            {
                throw new InvalidDataException($"Canonical Sensen fact '{factId}' is missing.");
            }

            return fact;
        }

        private static string FirstSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var separator = value.IndexOf('；');
            return separator < 0 ? value : value.Substring(0, separator);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            var existingAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (existingAsset != null)
            {
                throw new InvalidOperationException($"Expected {typeof(T).Name} at '{path}', found {existingAsset.GetType().Name}.");
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [Serializable]
        private sealed class CanonicalAnimalDocument
        {
            public int schemaVersion;
            public string animalId;
            public CanonicalIdentity identity;
            public CanonicalPresentation presentation;
            public CanonicalSource[] sources;
            public CanonicalFact[] facts;
        }

        [Serializable]
        private sealed class CanonicalIdentity
        {
            public string chineseName;
            public string nickname;
            public string scientificName;
        }

        [Serializable]
        private sealed class CanonicalPresentation
        {
            public string welcomeText;
            public string unknownReply;
            public string[] defaultSuggestions;
        }

        [Serializable]
        private sealed class CanonicalFact
        {
            public string factId;
            public string topic;
            public string claim;
            public string approvedAnswer;
            public string displayValue;
            public string[] keywords;
            public string[] aliases;
            public string[] items;
            public string[] sourceIds;
            public string confidence;
            public string evidenceStatus;
            public string lastVerified;
            public string notes;
        }

        [Serializable]
        private sealed class CanonicalSource
        {
            public string sourceId;
            public string title;
            public string organization;
            public string sourceType;
            public string url;
            public string publishedOrUpdatedDate;
            public string projectVerifiedDate;
            public string[] appliesToFactIds;
            public string notes;
        }
    }
}
