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
            var knowledge = LoadOrCreate<AnimalKnowledgeProfile>(KnowledgePath);
            var mission = LoadOrCreate<MissionDefinition>(MissionPath);
            var definition = LoadOrCreate<AnimalDefinition>(DefinitionPath);

            ConfigureKnowledge(knowledge);
            ConfigureMission(mission);
            ConfigureDefinition(definition, knowledge, mission);

            EditorUtility.SetDirty(knowledge);
            EditorUtility.SetDirty(mission);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            Debug.Log("Rebuilt audited Sensen content assets.");
        }

        private static void ConfigureKnowledge(AnimalKnowledgeProfile knowledge)
        {
            var serialized = new SerializedObject(knowledge);
            serialized.FindProperty("endangeredLevel").stringValue = "濒危";
            serialized.FindProperty("habitat").stringValue = "热带和亚热带森林";
            serialized.FindProperty("food").stringValue = "嫩叶、果实和花朵";
            SetStringArray(serialized.FindProperty("threats"), "栖息地破碎", "非法捕猎", "种群隔离");
            SetStringArray(serialized.FindProperty("protectionActions"), "少浪费纸张", "拒绝购买野生动物制品", "支持自然保护", "传播正确知识");
            SetStringArray(
                serialized.FindProperty("dailyFacts"),
                "缨冠灰叶猴主要吃嫩叶、果实和花朵。",
                "完整森林能给缨冠灰叶猴提供食物、庇护和迁徙通道。",
                "栖息地破碎、非法捕猎和种群隔离会让它们更加濒危。");
            SetKnowledgeEntries(serialized.FindProperty("entries"));
            serialized.FindProperty("unknownReply").stringValue = "你可以问我吃什么、住在哪里、为什么濒危，或来完成寻找食物任务。";
            SetStringArray(serialized.FindProperty("defaultSuggestions"), "你吃什么？", "帮森森寻找食物", "我怎么保护你？");
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
            MissionDefinition mission)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("animalId").stringValue = "sensen";
            serialized.FindProperty("displayName").stringValue = "缨冠灰叶猴 森森";
            serialized.FindProperty("shortName").stringValue = "森森";
            serialized.FindProperty("scientificName").stringValue = "Trachypithecus poliocephalus";
            serialized.FindProperty("markerName").stringValue = "sensen_marker";
            serialized.FindProperty("modelRelativePath").stringValue = "Models/Sensen/sensen.glb";
            serialized.FindProperty("baseColorTextureRelativePath").stringValue = "Models/Sensen/sensen_basecolor.png";
            serialized.FindProperty("experiencePosition").vector3Value = new Vector3(-1.02f, -0.13f, 0f);
            serialized.FindProperty("modelLocalOffset").vector3Value = new Vector3(0f, 0.04f, 0f);
            serialized.FindProperty("modelEulerAngles").vector3Value = new Vector3(0f, 180f, 0f);
            serialized.FindProperty("modelScale").vector3Value = new Vector3(1.45f, 1.45f, 1.45f);
            serialized.FindProperty("welcomeText").stringValue = "你好呀！我是缨冠灰叶猴森森。谢谢你愿意来到我的森林，今天我们一起认识我的食物、家和保护方法吧。";
            serialized.FindProperty("themeColor").colorValue = Color.white;
            serialized.FindProperty("portrait").objectReferenceValue = null;
            serialized.FindProperty("lockedSilhouette").objectReferenceValue = null;
            serialized.FindProperty("knowledge").objectReferenceValue = knowledge;
            serialized.FindProperty("mission").objectReferenceValue = mission;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetKnowledgeEntries(SerializedProperty entries)
        {
            entries.arraySize = 5;
            SetKnowledgeEntry(entries.GetArrayElementAtIndex(0), "food", new[] { "吃", "食物", "food" }, "我最喜欢森林里的嫩叶，也会吃果实和花朵；人类零食不适合我。", new[] { "帮森森寻找食物", "为什么不能投喂？", "你住在哪里？" });
            SetKnowledgeEntry(entries.GetArrayElementAtIndex(1), "habitat", new[] { "住", "栖息", "家", "哪里" }, "我的家在热带和亚热带森林，连在一起的树冠方便我找食物和同伴。", new[] { "森林被破坏会怎样？", "我能怎么帮你？", "你吃什么？" });
            SetKnowledgeEntry(entries.GetArrayElementAtIndex(2), "threats", new[] { "濒危", "危险", "为什么", "原因" }, "森林变少、非法捕猎和种群隔离会让我变得濒危。", new[] { "什么是种群隔离？", "怎么保护你？", "你的栖息地在哪里？" });
            SetKnowledgeEntry(entries.GetArrayElementAtIndex(3), "protection", new[] { "保护", "帮助", "怎么做", "行动" }, "少浪费纸张、拒绝野生动物制品、支持自然保护，都能让森林更安全。", new[] { "我可以参加什么任务？", "你吃什么？", "为什么要保护森林？" });
            SetKnowledgeEntry(entries.GetArrayElementAtIndex(4), "mission", new[] { "任务", "游戏", "挑战", "徽章" }, "帮我找到天然食物，完成后送你生态守护者徽章！", new[] { "开始寻找食物", "你喜欢吃什么？", "完成后有什么奖励？" });
        }

        private static void SetKnowledgeEntry(SerializedProperty entry, string knowledgeId, string[] keywords, string reply, string[] suggestedQuestions)
        {
            entry.FindPropertyRelative("knowledgeId").stringValue = knowledgeId;
            SetStringArray(entry.FindPropertyRelative("keywords"), keywords);
            entry.FindPropertyRelative("reply").stringValue = reply;
            SetStringArray(entry.FindPropertyRelative("suggestedQuestions"), suggestedQuestions);
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
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
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
    }
}
