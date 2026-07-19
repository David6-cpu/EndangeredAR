# Multi-Animal Foundation and Sensen Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变当前森森主流程和 UI 视觉的前提下，把单体 Demo 迁移为可测试、可持久化、可按配置扩展的多动物基础架构，并让森森成为第一个完整的数据驱动物种。

**Architecture:** 用 `AnimalDefinition`、`AnimalKnowledgeProfile`、`MissionDefinition` 三类 ScriptableObject 作为内容源；`AnimalCatalogService` 提供只读查询；`AnimalProgressService` 管理版本化本地 JSON；`AnimalExperienceController` 协调当前动物、模型、任务和进度。现有 `DemoAppController` 继续负责页面和 UI 事件，但不再持有动物配置、硬编码任务规则或森森专属 fallback。迁移使用兼容适配器，不一次性重写场景和 UI。

**Tech Stack:** Unity 2022.3.62f3c1、C#、UGUI、glTFast 6.18.0、Unity Test Framework 1.1.33、`JsonUtility`、StreamingAssets。

## Global Constraints

- 本计划是三阶段系列的第 1 阶段，只迁移森森并建立扩展底座；大熊猫、雪豹图鉴 UI 和正式内容放到后续计划。
- 不恢复 AR Foundation、ARKit、ARCore，不新增第三方包，不改 XR 依赖。
- 不做 UI 视觉重构；场景迁移只增加组件和引用，不重建 Canvas 层级。
- 不重新引入 `RoundedRectGraphic`，不改变按钮点击链路和 Safe Area 结构。
- 不提交 API Key、`.env`、用户存档、`Library/`、`UserSettings/`、`.vscode/`。
- 行为变更任务先写失败测试，再实现最小代码，再运行对应测试。Task 2 仅建立测试脚手架并锁定既有行为，字符化测试在程序集可编译后允许首次即通过，禁止为制造 RED 改坏既有运行时代码。
- 运行 Unity 批处理前先关闭编辑器，避免项目锁和 Library 并发写入。
- Phase 1 保留现有四个任务按钮：嫩叶、花朵正确；人类零食、塑料错误。果实仅作为知识内容，不新增第五个按钮。
- Phase 1 的扫描器只允许 `sensen_marker -> sensen` 进入体验；任何未知或尚未入目录的动物事件都必须停止导航。
- 扫描成功后进入独立 3D 页并使用 `AnimalDefinition.ExperiencePosition` 固定展示；不使用 tracked marker Transform 做空间放置。
- PlayMode 测试必须在场景 `Awake` 前注入临时存档路径，禁止先读取真实 `Application.persistentDataPath`。

统一命令变量：

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/yuanweijie/Documents/Larian Studios/animalsAR/EndangeredAR"
REPO="/Users/yuanweijie/Documents/Larian Studios/animalsAR"
```

---

## Task 1: Secure and Commit the Current Stable Baseline

**Files:**
- Create: `$REPO/.gitignore`
- Modify: `$PROJECT/Assets/Config/LocalApiConfig.asset`
- Create: `$PROJECT/docs/verification/2026-07-18-sensen-baseline.md`

- [ ] **Step 1: Add a Unity-safe root `.gitignore`**

```gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/
.vscode/
.vs/
.idea/
*.csproj
*.sln
*.suo
*.user
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
.DS_Store
.env
.env.*
!.env.example
*.ipa
*.xcarchive
sysinfo.txt
```

- [ ] **Step 2: Scrub client credentials before the first source commit**

Set `useDirectLlm: 0`, keep `baseUrl: http://127.0.0.1:8000`, and set `moonshotApiKey:` empty. Never print the previous value.

```bash
cd "$REPO"
rg -n --hidden --glob '!EndangeredAR/Library/**' --glob '!EndangeredAR/.git/**' \
  'sk-[A-Za-z0-9_-]{12,}|moonshotApiKey: .+' EndangeredAR
```

Expected: no credential value. Rotate the previously used provider key outside the repo before release.

- [ ] **Step 3: Record and run the stable baseline**

The verification note must record: no red errors; Learning/Scan/Profile clicks; camera/manual recognition; textured GLB; rotation/pinch; backend or local chat; mission rewards once; non-empty PNG.

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -quit \
  -logFile /private/tmp/endangered-ar-baseline.log
rg -n 'error CS|Compilation failed|Scripts have compiler errors|Aborting batchmode' \
  /private/tmp/endangered-ar-baseline.log
```

Expected: Unity exits 0; `rg` prints nothing.

- [ ] **Step 4: Commit only source and product assets**

```bash
cd "$REPO"
git add .gitignore EndangeredAR/Assets EndangeredAR/Packages \
  EndangeredAR/ProjectSettings EndangeredAR/DESIGN.md \
  EndangeredAR/Design EndangeredAR/docs
git status --short
```

Expected: no `Library/`, `UserSettings/`, `.vscode/`, `.env*`, or builds.

```bash
git commit -m "chore: establish stable Unity project baseline"
git tag sensen-stable-baseline
```

---

## Task 2: Add Runtime and EditMode Test Assemblies

**Files:**
- Create: `EndangeredAR/Assets/Scripts/EndangeredAR.Runtime.asmdef`
- Create: `EndangeredAR/Assets/Scripts/AssemblyInfo.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/EndangeredAR.Tests.EditMode.asmdef`
- Create: `EndangeredAR/Assets/Tests/EditMode/ExistingDemoSmokeTests.cs`

- [ ] **Step 1: Add runtime assembly and test visibility**

```json
{
  "name": "EndangeredAR.Runtime",
  "rootNamespace": "EndangeredAR",
  "references": ["glTFast", "UnityEngine.UI"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("EndangeredAR.Tests.EditMode")]
[assembly: InternalsVisibleTo("EndangeredAR.Tests.PlayMode")]
```

- [ ] **Step 2: Add EditMode test assembly**

```json
{
  "name": "EndangeredAR.Tests.EditMode",
  "rootNamespace": "EndangeredAR.Tests.EditMode",
  "references": ["EndangeredAR.Runtime"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "optionalUnityReferences": ["TestAssemblies"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 3: Lock the existing one-time mission reward**

This is a characterization test for stable existing behavior. Once the new test assembly compiles, the test is expected to pass immediately; do not mutate `MissionController` merely to manufacture a behavioral RED.

```csharp
[Test]
public void SensenFoodMission_AwardsPointsOnlyOnce()
{
    var go = new GameObject("Mission Test");
    try
    {
        var controller = go.AddComponent<MissionController>();
        controller.StartFoodMission();
        Assert.That(controller.SelectFood("嫩叶").Success, Is.True);
        Assert.That(controller.SelectFood("花朵").Success, Is.True);
        Assert.That(controller.Points, Is.EqualTo(20));
    }
    finally { Object.DestroyImmediate(go); }
}
```

- [ ] **Step 4: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/editmode.xml \
  -logFile /private/tmp/editmode.log
```

Expected: one pass, zero failures.

```bash
cd "$REPO"
git add EndangeredAR/Assets/Scripts/EndangeredAR.Runtime.asmdef \
  EndangeredAR/Assets/Scripts/AssemblyInfo.cs EndangeredAR/Assets/Tests
git commit -m "test: add Unity runtime and edit mode assemblies"
```

---

## Task 3: Define Animal, Knowledge, and Mission Contracts

**Files:**
- Create: `EndangeredAR/Assets/Scripts/Animals/AnimalDefinition.cs`
- Create: `EndangeredAR/Assets/Scripts/Animals/AnimalKnowledgeProfile.cs`
- Create: `EndangeredAR/Assets/Scripts/Missions/MissionDefinition.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AnimalContentContractTests.cs`

- [ ] **Step 1: Write failing contract tests**

```csharp
[Test] public void AnimalDefinition_RequiresStableAnimalId()
[Test] public void AnimalDefinition_ExposesModelPresentationWithoutMutation()
[Test] public void KnowledgeProfile_ReturnsUnknownFallbackWhenNoKeywordMatches()
[Test] public void MissionDefinition_RejectsDuplicateOptionIds()
```

Create ScriptableObjects through internal `Configure(...)`; destroy in teardown. Expected first run: missing-type compile failure.

- [ ] **Step 2: Implement `AnimalDefinition` as a read-only view**

Serialized fields: `animalId`, display/short/scientific names, marker, model and texture paths, experience position, model local offset/euler/scale, welcome, theme, portrait, locked silhouette, knowledge, mission.

```csharp
[CreateAssetMenu(menuName = "Endangered AR/Animal Definition")]
public sealed class AnimalDefinition : ScriptableObject
{
    public string AnimalId => animalId?.Trim();
    public string DisplayName => displayName;
    public string ShortName => shortName;
    public string ScientificName => scientificName;
    public string MarkerName => markerName;
    public string ModelRelativePath => modelRelativePath;
    public string BaseColorTextureRelativePath => baseColorTextureRelativePath;
    public Vector3 ExperiencePosition => experiencePosition;
    public Vector3 ModelLocalOffset => modelLocalOffset;
    public Vector3 ModelEulerAngles => modelEulerAngles;
    public Vector3 ModelScale => modelScale;
    public string WelcomeText => welcomeText;
    public AnimalKnowledgeProfile Knowledge => knowledge;
    public MissionDefinition Mission => mission;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AnimalId) && knowledge != null && mission != null;
}
```

Keep `Configure(...)` internal; runtime never mutates content.

- [ ] **Step 3: Implement knowledge and mission data**

`AnimalKnowledgeProfile`: endangered level, habitat, food, threats, protection actions, daily facts, `AnimalKnowledgeEntry[]`, unknown reply, default suggestions, `TryFindAnswer`. Matching is case-insensitive and skips null keywords.

`AnimalKnowledgeEntry`: knowledge ID, keywords, reply, suggestions.

`MissionDefinition`: mission ID/title/prompt/options, correct/wrong feedback, learned knowledge ID/fact, badge ID, points, `TryGetOption`, `Validate`.

`MissionOptionDefinition`: option ID, label, correct flag. Validation rejects blank/duplicate IDs and no correct option.

- [ ] **Step 4: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter EndangeredAR.Tests.EditMode.AnimalContentContractTests \
  -testResults /private/tmp/content-contract.xml \
  -logFile /private/tmp/content-contract.log

git add EndangeredAR/Assets/Scripts/Animals/AnimalDefinition.cs \
  EndangeredAR/Assets/Scripts/Animals/AnimalKnowledgeProfile.cs \
  EndangeredAR/Assets/Scripts/Missions/MissionDefinition.cs \
  EndangeredAR/Assets/Tests/EditMode/AnimalContentContractTests.cs
git commit -m "feat: define data-driven animal content contracts"
```

Expected: four passes.

---

## Task 4: Build a Validated Animal Catalog

**Files:**
- Create: `EndangeredAR/Assets/Scripts/Animals/AnimalCatalog.cs`
- Create: `EndangeredAR/Assets/Scripts/Animals/AnimalCatalogService.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AnimalCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

```csharp
[Test] public void Build_KeepsConfiguredDefinitionsInSourceOrder()
[Test] public void Build_SkipsNullBlankAndDuplicateDefinitions()
[Test] public void TryGet_UsesCaseInsensitiveAnimalId()
[Test] public void TryGet_UnknownIdReturnsFalseWithoutThrowing()
```

Keep first duplicate, skip later duplicates, record an issue.

- [ ] **Step 2: Implement catalog and wrapper**

```csharp
public sealed class AnimalCatalog
{
    public AnimalCatalog(IEnumerable<AnimalDefinition> source);
    public IReadOnlyList<AnimalDefinition> Animals { get; }
    public IReadOnlyList<string> Issues { get; }
    public bool TryGet(string animalId, out AnimalDefinition definition);
}

public sealed class AnimalCatalogService : MonoBehaviour
{
    [SerializeField] private AnimalDefinition[] definitions;
    [SerializeField] private string defaultAnimalId = "sensen";
    public AnimalCatalog Catalog { get; private set; }
    public AnimalDefinition DefaultAnimal { get; private set; }
    public void Initialize();
    public bool TryGet(string animalId, out AnimalDefinition definition);
}
```

Bad content never throws. `Initialize()` is idempotent, logs issues once, and falls back to the first valid animal.

- [ ] **Step 3: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter EndangeredAR.Tests.EditMode.AnimalCatalogTests \
  -testResults /private/tmp/catalog.xml -logFile /private/tmp/catalog.log

git add EndangeredAR/Assets/Scripts/Animals/AnimalCatalog.cs \
  EndangeredAR/Assets/Scripts/Animals/AnimalCatalogService.cs \
  EndangeredAR/Assets/Tests/EditMode/AnimalCatalogTests.cs
git commit -m "feat: add validated animal catalog service"
```

---

## Task 5: Add Versioned Local Animal Progress

**Files:**
- Create: `EndangeredAR/Assets/Scripts/Progress/AnimalProgressDocument.cs`
- Create: `EndangeredAR/Assets/Scripts/Progress/JsonAnimalProgressRepository.cs`
- Create: `EndangeredAR/Assets/Scripts/Progress/AnimalProgressService.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AnimalProgressRepositoryTests.cs`

- [ ] **Step 1: Write failing temp-directory tests**

```csharp
[Test] public void Load_MissingFileReturnsCurrentEmptyDocument()
[Test] public void SaveAndLoad_PreservesIndependentAnimalState()
[Test] public void Unlock_ReturnsTrueOnlyForFirstUnlock()
[Test] public void Conversations_AreTrimmedToTwentyMessagesPerAnimal()
[Test] public void Load_CorruptJsonCreatesBackupAndReturnsDefault()
[Test] public void Load_PreservesUnknownAnimalRecords()
```

Use a unique `Path.GetTempPath()` directory; never touch real app data.

- [ ] **Step 2: Implement version 1 records**

```csharp
[Serializable] public sealed class AnimalProgressDocument
{
    public int schemaVersion = JsonAnimalProgressRepository.CurrentSchemaVersion;
    public List<AnimalProgressRecord> animals = new List<AnimalProgressRecord>();
}
[Serializable] public sealed class AnimalProgressRecord
{
    public string animalId;
    public bool unlocked;
    public string unlockedAtUtc;
    public List<string> learnedKnowledgeIds = new List<string>();
    public bool missionCompleted;
    public List<string> earnedBadgeIds = new List<string>();
    public List<ConversationRecord> recentConversation = new List<ConversationRecord>();
}
[Serializable] public sealed class ConversationRecord
{
    public string role;
    public string content;
}
```

- [ ] **Step 3: Implement repository and service**

```csharp
public sealed class JsonAnimalProgressRepository
{
    public const int CurrentSchemaVersion = 1;
    public JsonAnimalProgressRepository(string filePath, Func<DateTime> utcNow = null);
    public AnimalProgressDocument Load();
    public void Save(AnimalProgressDocument document);
}
```

Write `<file>.tmp`; close; then `File.Replace(temp, destination, <file>.bak)`. If unsupported, copy temp over destination and delete temp only after success. On bad JSON, copy to `<file>.corrupt-yyyyMMdd-HHmmss` and return defaults. Keep unknown records.

```csharp
public sealed class AnimalProgressService : MonoBehaviour
{
    public event Action<string> ProgressChanged;
    public void Initialize(string overridePath = null);
    public bool IsUnlocked(string animalId);
    public bool Unlock(string animalId);
    public AnimalProgressRecord GetOrCreate(string animalId);
    public void MarkMissionCompleted(string animalId, string badgeId, string knowledgeId);
    public void ReplaceConversation(string animalId, IEnumerable<ConversationRecord> messages);
    public IReadOnlyList<ConversationRecord> GetConversation(string animalId);
    public int UnlockedCount { get; }
}
```

Default file: `Application.persistentDataPath/animal-progress.json`. Unlock saves immediately and is idempotent. Retain at most 20 messages.

- [ ] **Step 4: Run twice and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.AnimalProgressRepositoryTests \
  -testResults /private/tmp/progress-1.xml -logFile /private/tmp/progress-1.log
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.AnimalProgressRepositoryTests \
  -testResults /private/tmp/progress-2.xml -logFile /private/tmp/progress-2.log

git add EndangeredAR/Assets/Scripts/Progress \
  EndangeredAR/Assets/Tests/EditMode/AnimalProgressRepositoryTests.cs
git commit -m "feat: persist versioned per-animal progress"
```

Expected: both runs pass six tests.

---

## Task 6: Generate Audited Sensen Content Assets

**Files:**
- Create: `EndangeredAR/Assets/Editor/AnimalContentAssetBuilder.cs`
- Create: `EndangeredAR/Assets/Resources/Animals/SensenKnowledge.asset`
- Create: `EndangeredAR/Assets/Resources/Animals/SensenMission.asset`
- Create: `EndangeredAR/Assets/Resources/Animals/Sensen.asset`
- Create: `EndangeredAR/Assets/Tests/EditMode/SensenContentAssetTests.cs`

- [ ] **Step 1: Write failing asset integrity tests**

Assert: ID `sensen`; marker `sensen_marker`; model path `Models/Sensen/sensen.glb`; non-null knowledge/mission; four visible options with two correct natural foods; configured definition.

- [ ] **Step 2: Implement idempotent editor generation**

```csharp
[MenuItem("Endangered AR/Data/Rebuild Sensen Content")]
public static void RebuildSensenContent();
```

Create/update exact paths while preserving GUIDs. Canonical presentation:

```text
缨冠灰叶猴 森森 / 森森 / Trachypithecus poliocephalus
sensen_marker
Models/Sensen/sensen.glb
Models/Sensen/sensen_basecolor.png
experiencePosition (-1.02, -0.13, 0)
modelLocalOffset (0, 0.04, 0)
modelEulerAngles (0, 180, 0)
modelScale (1.45, 1.45, 1.45)
```

Migrate only current reviewed knowledge. Mission options: leaf/嫩叶/correct, flower/花朵/correct, snack/人类零食/wrong, plastic/塑料/wrong. Keep fruit in the food knowledge text only. Reward 20; badge `eco-guardian-sensen`.

- [ ] **Step 3: Generate, test, commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -executeMethod EndangeredAR.Editor.AnimalContentAssetBuilder.RebuildSensenContent \
  -logFile /private/tmp/rebuild-sensen.log -quit
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.SensenContentAssetTests \
  -testResults /private/tmp/sensen-content.xml -logFile /private/tmp/sensen-content.log

git add EndangeredAR/Assets/Editor/AnimalContentAssetBuilder.cs \
  EndangeredAR/Assets/Resources/Animals \
  EndangeredAR/Assets/Tests/EditMode/SensenContentAssetTests.cs
git commit -m "content: add audited Sensen animal configuration"
```

---

## Task 7: Make MissionController Definition-Driven

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/Missions/MissionController.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/MissionControllerTests.cs`

- [ ] **Step 1: Write failing state-machine tests**

```csharp
[Test] public void Configure_ResetsStateForDifferentMission()
[Test] public void WrongOption_ReturnsDefinitionFeedbackWithoutReward()
[Test] public void CorrectOption_CompletesAndAwardsDefinitionReward()
[Test] public void RepeatedCorrectOption_DoesNotAwardTwice()
[Test] public void InvalidOption_ReturnsFailureWithoutThrowing()
```

- [ ] **Step 2: Implement data-driven API**

```csharp
public void Configure(MissionDefinition definition, bool alreadyCompleted = false);
public void StartMission();
public MissionResult SelectOption(string optionId);
```

`MissionResult` exposes success, feedback, learned fact/ID, badge ID, points awarded. Keep obsolete `StartFoodMission` and `SelectFood` wrappers only until Task 11. Remove `Contains("嫩叶")` and all animal-specific correctness logic.

- [ ] **Step 3: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.MissionControllerTests \
  -testResults /private/tmp/mission.xml -logFile /private/tmp/mission.log

git add EndangeredAR/Assets/Scripts/Missions/MissionController.cs \
  EndangeredAR/Assets/Tests/EditMode/ExistingDemoSmokeTests.cs \
  EndangeredAR/Assets/Tests/EditMode/MissionControllerTests.cs
git commit -m "refactor: drive missions from animal definitions"
```

Old smoke test still asserts 20 points only once.

---

## Task 8: Make Local Fallback Animal-Specific

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/Chat/LocalKnowledgeChatService.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/LocalKnowledgeChatServiceTests.cs`

- [ ] **Step 1: Write failing isolation tests**

```csharp
[Test] public void Answer_UsesOnlyProvidedAnimalProfile()
[Test] public void Answer_UnknownQuestionUsesProvidedProfileFallback()
[Test] public void Answer_NullProfileReturnsSafeGenericFallback()
```

- [ ] **Step 2: Use the selected profile**

```csharp
public ChatAnswer Answer(AnimalKnowledgeProfile profile, string message)
{
    if (profile != null && profile.TryFindAnswer(message, out var entry))
        return new ChatAnswer(entry.Reply, entry.SuggestedQuestions, true);
    return profile == null
        ? ChatAnswer.GenericFallback
        : new ChatAnswer(profile.UnknownReply, profile.DefaultSuggestions, false);
}
```

Remove embedded森森 entries. Keep an obsolete `Answer(string)` until Task 11; if its serialized default profile is unset, it may resolve the first valid profile generically from `Resources/Animals` so the existing scene remains functional without embedding any animal-specific content.

- [ ] **Step 3: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.LocalKnowledgeChatServiceTests \
  -testResults /private/tmp/fallback.xml -logFile /private/tmp/fallback.log

git add EndangeredAR/Assets/Scripts/Chat/LocalKnowledgeChatService.cs \
  EndangeredAR/Assets/Tests/EditMode/LocalKnowledgeChatServiceTests.cs
git commit -m "refactor: isolate local answers by animal knowledge"
```

---

## Task 9: Generalize GLB Loading Without Breaking the Scene

**Files:**
- Create: `EndangeredAR/Assets/Scripts/Models/AnimalModelLoader.cs`
- Modify: `EndangeredAR/Assets/Scripts/Models/SensenGlbLoader.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AnimalModelLoaderTests.cs`

- [ ] **Step 1: Write failing configuration tests**

```csharp
[Test] public void Configure_CopiesDefinitionPathsAndLocalPresentation()
[Test] public void Configure_DoesNotMoveHostExperienceTransform()
[Test] public void Configure_MissingModelLeavesFallbackRendererEnabled()
```

- [ ] **Step 2: Extract generic loader**

```csharp
public class AnimalModelLoader : MonoBehaviour
{
    public string LoadedAnimalId { get; }
    public void Configure(AnimalDefinition definition);
    public void Retry();
}
```

Reuse material repair/fallback behavior. Use `Animal GLB Runtime Root`; read paths and local presentation from the definition; never modify `experienceHostTransform` world position/scale; log animal ID and relative path only; do not load in EditMode.

The base loader must retain legacy serialized field names or use `FormerlySerializedAs`, and it must permanently disable the old `fixLegacyDemoPlacement` host correction.

- [ ] **Step 3: Keep the old script GUID as an adapter**

```csharp
[Obsolete("Use AnimalModelLoader. Kept for scene serialization.")]
public sealed class SensenGlbLoader : AnimalModelLoader { }
```

- [ ] **Step 4: Test and visually verify**

Run tests, then in Play Mode confirm texture, fallback hide timing, rotation, pinch, and that the host is not reset to old `y=0.72`.

```bash
git add EndangeredAR/Assets/Scripts/Models/AnimalModelLoader.cs \
  EndangeredAR/Assets/Scripts/Models/SensenGlbLoader.cs \
  EndangeredAR/Assets/Tests/EditMode/AnimalModelLoaderTests.cs
git commit -m "refactor: generalize bundled GLB animal loading"
```

---

## Task 10: Coordinate Selection, Unlocking, and Per-Animal State

**Files:**
- Create: `EndangeredAR/Assets/Scripts/Animals/AnimalExperienceController.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AnimalExperienceControllerTests.cs`

- [ ] **Step 1: Write failing flow tests**

```csharp
[Test] public void Prepare_UnknownAnimalDoesNotChangeCurrent()
[Test] public void SelectFromScan_FirstTimeUnlocksAndConfiguresAnimal()
[Test] public void SelectFromScan_RepeatDoesNotDuplicateUnlockReward()
[Test] public void SelectFromCatalog_LockedAnimalIsRejected()
[Test] public void SelectFromCatalog_UnlockedAnimalRestoresMissionState()
[Test] public void SwitchingAnimalsDoesNotReuseMissionOrConversationState()
```

- [ ] **Step 2: Implement explicit results and controller**

```csharp
public enum AnimalSelectionStatus { Selected, NewlyUnlocked, Locked, UnknownAnimal }
public readonly struct AnimalSelectionResult
{
    public AnimalSelectionStatus Status { get; }
    public AnimalDefinition Animal { get; }
    public bool IsSuccess => Status == AnimalSelectionStatus.Selected ||
                             Status == AnimalSelectionStatus.NewlyUnlocked;
}
```

```csharp
public sealed class AnimalExperienceController : MonoBehaviour
{
    public event Action<AnimalDefinition> CurrentAnimalChanged;
    public AnimalDefinition CurrentAnimal { get; private set; }
    public AnimalProgressRecord CurrentProgress { get; }
    public void Initialize();
    public AnimalSelectionResult Prepare(string animalId);
    public AnimalSelectionResult SelectFromScan(string animalId);
    public AnimalSelectionResult SelectFromCatalog(string animalId);
}
```

Successful selection configures mission/model, moves `experienceHostTransform` to `ExperiencePosition`, and exposes selected knowledge/progress. `Prepare` never unlocks. Only scan creates unlock.

- [ ] **Step 3: Run and commit**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.AnimalExperienceControllerTests \
  -testResults /private/tmp/experience.xml -logFile /private/tmp/experience.log

git add EndangeredAR/Assets/Scripts/Animals/AnimalExperienceController.cs \
  EndangeredAR/Assets/Tests/EditMode/AnimalExperienceControllerTests.cs
git commit -m "feat: coordinate animal selection and unlock state"
```

---

## Task 11: Migrate DemoAppController and the Existing Scene

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/UI/DemoAppController.cs`
- Modify: `EndangeredAR/Assets/Scripts/AR/ARImageScanController.cs`
- Modify: `EndangeredAR/Assets/Editor/EndangeredARDemoSceneBuilder.cs`
- Create: `EndangeredAR/Assets/Editor/AnimalArchitectureSceneMigrator.cs`
- Modify: `EndangeredAR/Assets/Scenes/DemoScene.unity`
- Create: `EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs`

- [ ] **Step 1: Write failing migration guards**

```csharp
[Test] public void DemoController_NoLongerDeclaresEmbeddedAnimalProfileArray()
[Test] public void DemoController_NoLongerDeclaresNestedAnimalProfileType()
[Test] public void DemoScene_HasCatalogProgressAndExperienceServices()
[Test] public void DemoScene_CatalogContainsSensenDefinition()
```

- [ ] **Step 2: Add services before existing startup state**

```csharp
[SerializeField] private AnimalCatalogService animalCatalog;
[SerializeField] private AnimalProgressService animalProgress;
[SerializeField] private AnimalExperienceController animalExperience;
```

Initialize before old `SetCurrentAnimal`. Missing services log one actionable error and leave startup page alive; no null exception.

- [ ] **Step 3: Replace embedded profile paths**

```csharp
private AnimalDefinition CurrentAnimal =>
    animalExperience != null && animalExperience.CurrentAnimal != null
        ? animalExperience.CurrentAnimal
        : animalCatalog?.DefaultAnimal;
```

Migrate exactly:

- simulated animals iterate catalog;
- `ARImageScanController` Phase 1 mapping contains only `sensen_marker -> sensen`;
- unmatched marker names resolve to no animal instead of silently falling back to Sensen;
- scan detected calls `Prepare`; unknown IDs return immediately and cannot activate model navigation;
- tracked/manual success calls `SelectFromScan` before model page and enters only when `IsSuccess` is true;
- tracked marker Transform is intentionally ignored because placement is fixed on the independent 3D page;
- future catalog entry calls `SelectFromCatalog` and cannot bypass lock;
- model uses `AnimalModelLoader.Configure(CurrentAnimal)`;
- mission uses `StartMission` and option IDs;
- success writes mission/badge/knowledge progress;
- fallback receives `CurrentAnimal.Knowledge`;
- profile uses `UnlockedCount`;
- learn/card use selected knowledge and mission.

- [ ] **Step 4: Restore and persist conversation**

On selection, load the current animal's records. After each completed user/assistant message, trim to 20 and save. Never persist thinking text or raw network errors.

- [ ] **Step 5: Delete obsolete embedded data**

Delete `AnimalProfile[]`, `GetValidAnimalProfiles`, `FindAnimalProfile`, nested `AnimalProfile`, `isAnimalUnlocked` as source of truth, and hardcoded森森 learned-fact branches. UI mirrors refresh from `AnimalProgressRecord`.

- [ ] **Step 6: Migrate scene non-destructively**

`AnimalArchitectureSceneMigrator.MigrateDemoScene()` opens the current scene, adds/finds catalog/progress/experience services, assigns `Sensen.asset`, reuses existing placeholder/mission/fallback/demo objects, wires by `SerializedObject`, and saves without calling `BuildDemoScene()` or rebuilding Canvas.

Before migration, record the actual scene null/optional references and button names. After migration, reopen the scene and exercise manual scan, model back, four mission choices, fallback chat, and profile update. Any generated Canvas object addition or deletion is a test failure.

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -executeMethod EndangeredAR.Editor.AnimalArchitectureSceneMigrator.MigrateDemoScene \
  -logFile /private/tmp/migrate-scene.log -quit
cd "$REPO"
git diff --stat -- EndangeredAR/Assets/Scenes/DemoScene.unity
git diff -- EndangeredAR/Assets/Scenes/DemoScene.unity | sed -n '1,240p'
```

Expected: service objects/references only. If Canvas objects churn, undo only this generated scene diff and fix the migrator.

- [ ] **Step 7: Update builder, test all EditMode, commit**

Future `BuildDemoScene()` creates new services, but do not invoke it on the reviewed scene.

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testResults /private/tmp/editmode-all.xml \
  -logFile /private/tmp/editmode-all.log

git add EndangeredAR/Assets/Scripts/UI/DemoAppController.cs \
  EndangeredAR/Assets/Scripts/AR/ARImageScanController.cs \
  EndangeredAR/Assets/Editor/EndangeredARDemoSceneBuilder.cs \
  EndangeredAR/Assets/Editor/AnimalArchitectureSceneMigrator.cs \
  EndangeredAR/Assets/Scenes/DemoScene.unity \
  EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs
git commit -m "refactor: migrate Sensen demo to animal services"
```

Expected: zero failed tests and no compile errors.

---

## Task 12: Enforce Backend-Only LLM Access

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/API/ApiConfig.cs`
- Modify: `EndangeredAR/Assets/Scripts/API/ChatApiClient.cs`
- Modify: `EndangeredAR/Assets/Editor/EndangeredARDemoSceneBuilder.cs`
- Modify: `EndangeredAR/Assets/Config/LocalApiConfig.asset`
- Create: `EndangeredAR/Assets/Tests/EditMode/ApiSecurityTests.cs`

- [ ] **Step 1: Write failing reflection/security tests**

Assert `ApiConfig` has no provider key/direct-mode field and `ChatApiClient` has backend `/chat` only.

- [ ] **Step 2: Remove direct provider runtime/editor paths**

Delete direct-mode flag, provider URL/model/key, direct coroutine, provider DTOs, editor env-import menu, and Bearer header construction. Keep backend base URL and `{animalId,message,history}` payload. Failure returns to selected-animal local fallback.

- [ ] **Step 3: Scan, test, commit**

```bash
cd "$REPO"
rg -n --hidden --glob '!EndangeredAR/Library/**' \
  'moonshotApiKey|useDirectLlm|SendDirectMoonshotMessage|Authorization.*Bearer|sk-[A-Za-z0-9_-]{12,}' \
  EndangeredAR/Assets EndangeredAR/Packages EndangeredAR/ProjectSettings
```

Expected: no output.

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter EndangeredAR.Tests.EditMode.ApiSecurityTests \
  -testResults /private/tmp/api-security.xml -logFile /private/tmp/api-security.log

git add EndangeredAR/Assets/Scripts/API \
  EndangeredAR/Assets/Editor/EndangeredARDemoSceneBuilder.cs \
  EndangeredAR/Assets/Config/LocalApiConfig.asset \
  EndangeredAR/Assets/Tests/EditMode/ApiSecurityTests.cs
git commit -m "security: route all LLM traffic through backend"
```

---

## Task 13: Add PlayMode Regression and Verify the Vertical Slice

**Files:**
- Create: `EndangeredAR/Assets/Tests/PlayMode/EndangeredAR.Tests.PlayMode.asmdef`
- Create: `EndangeredAR/Assets/Tests/PlayMode/SensenVerticalSliceTests.cs`
- Modify: `EndangeredAR/docs/verification/2026-07-18-sensen-baseline.md`

- [ ] **Step 1: Add PlayMode assembly**

```json
{
  "name": "EndangeredAR.Tests.PlayMode",
  "rootNamespace": "EndangeredAR.Tests.PlayMode",
  "references": ["EndangeredAR.Runtime"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "optionalUnityReferences": ["TestAssemblies"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Write scene-level tests**

```csharp
[UnityTest] public IEnumerator Startup_HasNoMissingCoreServices()
[UnityTest] public IEnumerator ManualScan_SelectsAndUnlocksSensen()
[UnityTest] public IEnumerator RepeatScan_DoesNotIncreaseUnlockedCount()
[UnityTest] public IEnumerator SensenExperience_KeepsModelGestureController()
[UnityTest] public IEnumerator NetworkUnavailable_LocalFallbackStillReturnsAnswer()
```

Set internal static `AnimalProgressService.RepositoryPathOverrideForTests` before `SceneManager.LoadScene`, then load `DemoScene` and assert the active repository path is temporary before any selection or save. Do not overwrite real app progress. Internal read-only hooks are allowed; no public test buttons.

- [ ] **Step 3: Run both suites**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testResults /private/tmp/editmode-final.xml \
  -logFile /private/tmp/editmode-final.log
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests \
  -testPlatform PlayMode -testResults /private/tmp/playmode-final.xml \
  -logFile /private/tmp/playmode-final.log
```

Expected: zero failures; no null exception, missing script, or compile error.

- [ ] **Step 4: Run iPhone 17 Pro Max regression**

Check 1290x2796 Game View and iOS device: Safe Area, three nav buttons, camera orientation/aspect, manual recognition, same model placement/gesture, mission, fallback, PNG, relaunch unlock, recent conversation, clean logs. Update the verification note without credentials or private user paths.

- [ ] **Step 5: Commit verification**

```bash
git add EndangeredAR/Assets/Tests/PlayMode \
  EndangeredAR/docs/verification/2026-07-18-sensen-baseline.md
git commit -m "test: verify data-driven Sensen vertical slice"
```

---

## Phase 1 Completion Gate

- [ ] Unity compiles with no red Console errors.
- [ ] All EditMode and PlayMode tests pass.
- [ ] `DemoAppController` owns no `AnimalProfile[]` or animal-specific task/fallback rules.
- [ ] Sensen content comes from committed ScriptableObjects.
- [ ] First scan unlocks森森 and relaunch preserves it.
- [ ] Recent森森 conversation restores without thinking/error placeholders.
- [ ] GLB material, rotation, pinch, and placement match baseline.
- [ ] No provider API key or direct-provider client path exists.
- [ ] Existing Canvas hierarchy and button behavior remain intact.
- [ ] A stable migrated Sensen commit exists.

## Follow-On Plans

1. **Three-Animal Catalog and Unlock Flow:** 图鉴首页、锁定卡片、中央扫描、大熊猫/雪豹明确占位模型、重复扫描与图鉴直达统一互动页。
2. **Three-Animal Content and Release Readiness:** 三份审核知识、三套角色 Prompt、三项专属任务、徽章与科普卡片、正式模型替换、真机性能和发布验收。
