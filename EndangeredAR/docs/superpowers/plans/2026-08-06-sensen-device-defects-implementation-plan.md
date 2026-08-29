# Sensen Device Defects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复森森真机验收中的相机比例、局域网 Kimi 连接、任务提前发奖和科普卡片排版问题，并在 iPhone 17 Pro Max 上完成闭环验证。

**Architecture:** 相机继续使用 `WebCamTexture + RawImage`，只修正旋转矩形与 AspectRatioFitter 的比例契约。AI 通过仓库内的 Python 开发代理访问 Moonshot，密钥只从后端环境读取。任务控制器把持久化奖励状态和本轮答题状态分开，卡片则把可保存内容与页面控制按钮拆成两个区域。

**Tech Stack:** Unity 2022.3.62f3c1、C#、Unity UI、NUnit/EditMode/PlayMode、Python 3 标准库 HTTP server、Moonshot OpenAI-compatible API、Xcode 27、iOS 27 Beta。

## Global Constraints

- 不恢复 ARFoundation，不改 Unity 包依赖。
- 不把 `MOONSHOT_API_KEY`、Authorization header 或 provider endpoint 写入 Unity/iOS 客户端。
- 不删除或重置用户持久化动物进度。
- 不重新生成 `DemoScene`；只修改运行时增强逻辑和数据配置。
- 不重新引入 `RoundedRectGraphic`。
- 保留模拟识别、模型加载、手势、解锁、聊天历史和 PNG 保存接口。

验证命令使用调用者提供的 Unity Editor 和仓库外临时目录：

```bash
: "${UNITY:?Set UNITY to the required Unity 2022.3.62f3c1 executable}"
TEST_RESULTS_DIR="${TEST_RESULTS_DIR:-$(mktemp -d)}"
mkdir -p "$TEST_RESULTS_DIR"
```

---

### Task 1: Correct Camera Preview Aspect

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/AR/ARImageScanController.cs`
- Modify: `EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs`

**Interfaces:**
- Consumes: `WebCamTexture.width`, `WebCamTexture.height`, `WebCamTexture.videoRotationAngle`.
- Produces: private `CalculatePreviewAspectRatio(int width, int height, int rotationAngle)` used by `ApplyPreviewOrientation()`.

- [ ] **Step 1: Write the failing aspect-contract test**

Add a reflection-based test that proves rotation does not invert the fitting ratio a second time:

```csharp
[TestCase(1920, 1080, 0, 16f / 9f)]
[TestCase(1920, 1080, 90, 16f / 9f)]
[TestCase(1920, 1080, 270, 16f / 9f)]
[TestCase(1080, 1920, 0, 9f / 16f)]
public void CameraPreviewAspect_UsesRawTextureRatio(
    int width, int height, int rotationAngle, float expected)
{
    var method = typeof(ARImageScanController).GetMethod(
        "CalculatePreviewAspectRatio",
        BindingFlags.Static | BindingFlags.NonPublic);

    Assert.That(method, Is.Not.Null);
    var actual = (float)method.Invoke(null, new object[] { width, height, rotationAngle });
    Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
}
```

- [ ] **Step 2: Run the focused EditMode test and verify RED**

Run:

```bash
"$UNITY" \
  -batchmode -projectPath "$PWD/EndangeredAR" \
  -runTests -testPlatform EditMode \
  -testFilter EndangeredAR.Tests.EditMode.DemoAnimalMigrationTests.CameraPreviewAspect_UsesRawTextureRatio \
  -testResults "$TEST_RESULTS_DIR/camera-aspect-red.xml" \
  -logFile "$TEST_RESULTS_DIR/camera-aspect-red.log"
```

Expected: FAIL because `CalculatePreviewAspectRatio` does not exist.

- [ ] **Step 3: Implement the raw-ratio contract**

Add the helper and replace the rotated conditional:

```csharp
private static float CalculatePreviewAspectRatio(int width, int height, int rotationAngle)
{
    _ = rotationAngle;
    return width > 16 && height > 16 ? width / (float)height : 9f / 16f;
}
```

In `ApplyPreviewOrientation()`:

```csharp
aspect.aspectRatio = CalculatePreviewAspectRatio(
    cameraTexture.width,
    cameraTexture.height,
    cameraTexture.videoRotationAngle);
```

Keep `rect.localEulerAngles = new Vector3(0f, 0f, -cameraTexture.videoRotationAngle)` and the mirrored `uvRect` behavior unchanged.

- [ ] **Step 4: Run focused and full EditMode tests**

Expected: focused test passes, then full EditMode remains green.

- [ ] **Step 5: Commit camera fix**

```bash
git add EndangeredAR/Assets/Scripts/AR/ARImageScanController.cs \
  EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs
git commit -m "fix: preserve camera preview aspect on iPhone"
```

---

### Task 2: Restore Secure LAN Kimi Proxy

**Files:**
- Create: `server/dev_server.py`
- Create: `server/.env.example`
- Create: `server/README.md`
- Create: `server/tests/test_dev_server.py`
- Create: `content/animals/sensen.json`
- Modify: `EndangeredAR/Assets/Config/LocalApiConfig.asset`

**Interfaces:**
- Consumes: `MOONSHOT_API_KEY`, optional `MOONSHOT_BASE_URL`, optional `MOONSHOT_MODEL`, and JSON `POST /chat` with `animalId`, `message`, `history`.
- Produces: `GET /health` and `POST /chat` returning `animalId`, `reply`, `suggestedQuestions`, and `missionHint`.

- [ ] **Step 1: Add backend contract tests**

Use Python `unittest` and import `server.dev_server` without starting a socket:

```python
class DevServerTests(unittest.TestCase):
    def test_system_prompt_is_sensen_specific_and_child_friendly(self):
        prompt = dev_server.make_system_prompt(SENSEN)
        self.assertIn("森森", prompt)
        self.assertIn("120", prompt)
        self.assertNotIn("API Key", prompt)

    def test_message_payload_keeps_recent_history(self):
        payload = dev_server.make_llm_payload(
            SENSEN,
            "我能怎么帮助你？",
            [{"role": "user", "content": "你住在哪里？"}],
        )
        self.assertEqual(payload["messages"][-2]["role"], "user")
        self.assertEqual(payload["messages"][-1]["content"], "我能怎么帮助你？")

    def test_rule_fallback_works_without_provider_key(self):
        reply = dev_server.make_rule_reply(SENSEN, "你吃什么？")
        self.assertIn("嫩叶", reply)
```

- [ ] **Step 2: Run backend tests and verify RED**

Run: `python3 -m unittest discover -s server/tests -v`

Expected: FAIL because the repository-local server module does not exist.

- [ ] **Step 3: Add the repository-local proxy**

Implement these focused functions in `server/dev_server.py` (`json`, `os`, `urllib.error`, and `urllib.request` are imported at module level):

```python
def make_system_prompt(animal: dict) -> str:
    foods = "、".join(animal.get("food", [])) or "森林中的天然食物"
    threats = "、".join(animal.get("threats", [])) or "栖息地破坏"
    actions = "、".join(animal.get("protectionActions", [])) or "保护森林"
    return (
        f"你是濒危动物科普 App 中的角色“{animal.get('nickname', '动物朋友')}”，"
        f"物种是{animal.get('name', '濒危动物')}。食物：{foods}。"
        f"威胁：{threats}。保护行动：{actions}。"
        "请用中文、活泼温柔且适合青少年的语气回答，每次不超过120字。"
        "资料没有答案时明确说不确定，不编造，不提供危险内容。"
    )


def make_rule_reply(animal: dict, message: str) -> str:
    nickname = animal.get("nickname", "我")
    if "吃" in message or "食物" in message:
        foods = "、".join(animal.get("food", []))
        return f"我是{nickname}，我喜欢吃{foods}。森林里的天然食物才适合我。"
    if "保护" in message or "帮助" in message:
        actions = "、".join(animal.get("protectionActions", [])[:2])
        return f"谢谢你愿意帮助我！你可以{actions}，也可以把保护知识告诉朋友。"
    threats = "、".join(animal.get("threats", []))
    return f"我是{nickname}。我面临{threats}，所以很需要大家一起保护森林。"


def make_llm_payload(animal: dict, message: str, history: list[dict]) -> dict:
    recent = [
        {"role": item["role"], "content": item["content"]}
        for item in (history or [])
        if item.get("role") in {"user", "assistant"} and item.get("content")
    ][-20:]
    messages = [{"role": "system", "content": make_system_prompt(animal)}]
    messages.extend(recent)
    messages.append({"role": "user", "content": message})
    return {
        "model": os.environ.get("MOONSHOT_MODEL", "moonshot-v1-8k"),
        "messages": messages,
        "temperature": 0.8,
        "max_completion_tokens": 220,
    }


def call_moonshot(animal: dict, message: str, history: list[dict]) -> str | None:
    api_key = os.environ.get("MOONSHOT_API_KEY")
    if not api_key:
        return None
    base_url = os.environ.get("MOONSHOT_BASE_URL", "https://api.moonshot.cn/v1").rstrip("/")
    body = json.dumps(make_llm_payload(animal, message, history), ensure_ascii=False).encode("utf-8")
    http_request = request.Request(
        f"{base_url}/chat/completions",
        data=body,
        method="POST",
        headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
    )
    try:
        with request.urlopen(http_request, timeout=35) as response:
            result = json.loads(response.read().decode("utf-8"))
    except (error.HTTPError, error.URLError, OSError, json.JSONDecodeError):
        return None
    choices = result.get("choices") or []
    content = (choices[0].get("message") or {}).get("content") if choices else None
    return content.strip() if content else None
```

`make_llm_payload` must prepend one system message, keep only the most recent 20 valid user/assistant history messages, append the new user message, set `max_completion_tokens` to 220, and never serialize the provider key.

The handler must listen on `0.0.0.0:8000`. When Moonshot is unavailable it returns `make_rule_reply` instead of a 500 response.

- [ ] **Step 4: Add non-secret configuration and Sensen content**

`server/.env.example` contains only variable names and harmless defaults:

```text
MOONSHOT_API_KEY=
MOONSHOT_BASE_URL=https://api.moonshot.cn/v1
MOONSHOT_MODEL=moonshot-v1-8k
```

`content/animals/sensen.json` contains the committed species facts and no user data or secrets. Update `LocalApiConfig.asset` to:

```yaml
baseUrl: http://<development-host>:8000
```

Replace `<development-host>` at development time with the current machine's LAN host; do not commit the resolved local address.

- [ ] **Step 5: Run backend and security tests**

Run:

```bash
python3 -m unittest discover -s server/tests -v
"$UNITY" \
  -batchmode -projectPath "$PWD/EndangeredAR" \
  -runTests -testPlatform EditMode \
  -testFilter EndangeredAR.Tests.EditMode.ApiSecurityTests \
  -testResults "$TEST_RESULTS_DIR/api-security-green.xml" \
  -logFile "$TEST_RESULTS_DIR/api-security-green.log"
```

Expected: all pass; repository scan contains no key value or client provider authorization code.

- [ ] **Step 6: Start the backend with the existing local secret environment**

Run without printing values:

```bash
: "${LOCAL_ENV_FILE:?Set LOCAL_ENV_FILE to a Git-ignored local environment file}"
set -a
source "$LOCAL_ENV_FILE"
set +a
python3 server/dev_server.py
```

Verify:

```bash
: "${DEVELOPMENT_HOST:?Set DEVELOPMENT_HOST to the current development machine host}"
curl -sS http://127.0.0.1:8000/health
curl -sS "http://$DEVELOPMENT_HOST:8000/health"
curl -sS -X POST http://127.0.0.1:8000/chat \
  -H 'Content-Type: application/json' \
  -d '{"animalId":"sensen","message":"你住在哪里？","history":[]}'
```

- [ ] **Step 7: Commit backend proxy**

```bash
git add server content/animals/sensen.json EndangeredAR/Assets/Config/LocalApiConfig.asset
git commit -m "feat: restore secure Sensen chat proxy"
```

---

### Task 3: Separate Mission Attempt From Historical Reward

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/Missions/MissionController.cs`
- Modify: `EndangeredAR/Assets/Scripts/UI/DemoAppController.cs`
- Modify: `EndangeredAR/Assets/Tests/EditMode/MissionControllerTests.cs`
- Modify: `EndangeredAR/Assets/Tests/PlayMode/SensenVerticalSliceTests.cs`

**Interfaces:**
- Consumes: persisted `alreadyCompleted` from `AnimalProgressService`.
- Produces: `MissionController.RewardAlreadyClaimed`, replayable `StartMission()`, and UI copy that distinguishes historical reward from current answer.

- [ ] **Step 1: Write failing mission replay tests**

Add EditMode coverage:

```csharp
[Test]
public void RestoredCompletion_StartsReplayWithoutRewardingAgain()
{
    var controller = CreateController();
    controller.Configure(CreateMission("food", 20), alreadyCompleted: true);

    controller.StartMission();
    var result = controller.SelectOption("leaf");

    Assert.That(controller.RewardAlreadyClaimed, Is.True);
    Assert.That(result.Success, Is.True);
    Assert.That(result.PointsAwarded, Is.Zero);
}
```

Add PlayMode coverage that opens the mission panel with persisted completion and asserts the initial label does not start with `已获得：`.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: EditMode fails because `RewardAlreadyClaimed` does not exist or replay cannot start; PlayMode fails on the premature success copy.

- [ ] **Step 3: Implement reward/attempt separation**

In `MissionController` add:

```csharp
private bool rewardAlreadyClaimed;
public bool RewardAlreadyClaimed => rewardAlreadyClaimed;
```

`Configure(definition, alreadyCompleted)` sets `rewardAlreadyClaimed = alreadyCompleted` and resets the current attempt to `NotStarted` when a definition is selected. `StartMission()` always enters `Choosing` for a valid definition. A correct answer returns `PointsAwarded = 0` when the reward was already claimed; the first correct answer sets the flag and grants points once.

In `EnterMissionView()` use:

```csharp
SetText(badgeText, IsCurrentMissionCompleted
    ? "徽章已收藏 · 本轮可再次挑战"
    : "答对后解锁：生态守护者徽章");
```

In `SelectMissionOption()` capture the persisted completion before selection. Only call `MarkMissionCompleted` and show `新获得` when the reward was not previously claimed. Replays show `回答正确 · 徽章已收藏`.

- [ ] **Step 4: Run focused and full Unity tests**

Expected: all mission tests and PlayMode tests pass; repeated correct selection remains zero points.

- [ ] **Step 5: Commit mission-state fix**

```bash
git add EndangeredAR/Assets/Scripts/Missions/MissionController.cs \
  EndangeredAR/Assets/Scripts/UI/DemoAppController.cs \
  EndangeredAR/Assets/Tests/EditMode/MissionControllerTests.cs \
  EndangeredAR/Assets/Tests/PlayMode/SensenVerticalSliceTests.cs
git commit -m "fix: separate mission replay from badge rewards"
```

---

### Task 4: Rebuild the Shareable Knowledge Card

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/UI/DemoAppController.cs`
- Modify: `EndangeredAR/Assets/Tests/PlayMode/SensenVerticalSliceTests.cs`

**Interfaces:**
- Consumes: current animal definition, learned fact, persisted mission and badge state.
- Produces: `cardCaptureRect` scoped to a share surface that excludes controls; `cardBadgeStatusText` and `cardActionText` for short structured content.

- [ ] **Step 1: Write failing card hierarchy tests**

Add PlayMode assertions:

```csharp
[UnityTest]
public IEnumerator KnowledgeCard_CaptureSurfaceExcludesControlButtons()
{
    yield return LoadDemoScene();
    var controller = FindSingle<DemoAppController>();
    var capture = (RectTransform)GetPrivateField(controller, "cardCaptureRect");
    var save = (Button)GetPrivateField(controller, "cardSaveButton");
    var back = (Button)GetPrivateField(controller, "cardBackButton");

    Assert.That(capture.name, Is.EqualTo("Share Card Surface"));
    Assert.That(save.transform.IsChildOf(capture), Is.False);
    Assert.That(back.transform.IsChildOf(capture), Is.False);
    Assert.That(capture.Find("Card Sensen Avatar"), Is.Not.Null);
    Assert.That(capture.Find("Card Content"), Is.Not.Null);
}
```

- [ ] **Step 2: Run PlayMode test and verify RED**

Expected: FAIL because `cardCaptureRect` currently points to the full `Knowledge Card Panel` and includes controls.

- [ ] **Step 3: Build the three-section card**

In `BuildCardPanel()`:

- Keep `cardPanel` as a full-screen light overlay.
- Create `Share Card Surface` from anchors `(0.07, 0.16)` to `(0.93, 0.95)` and assign it to `cardCaptureRect`.
- Put avatar, title, species subtitle, three concise fact rows, badge status, and environmental action inside the share surface.
- Put Save and Back buttons below the share surface as siblings, not children.
- Do not apply the `Backgrounds/bg-card-share` asset to `cardPanel`.

Add two text fields:

```csharp
private Text cardBadgeStatusText;
private Text cardActionText;
```

`UpdateCardContent()` must use short copy:

```csharp
cardContentText.text =
    $"栖息地与食物\n{CurrentKnowledgeFact(0)}\n\n" +
    $"今日知识\n{CurrentLearnedFact()}\n\n" +
    $"任务记录\n{missionLine}";
SetText(cardBadgeStatusText, badgeLine);
SetText(cardActionText, "今日行动：不投喂野生动物，把保护森林的知识告诉更多人。");
```

- [ ] **Step 4: Make PNG saving failure-safe**

Wrap capture and file writing in `try/catch/finally`. Always destroy allocated textures in `finally`; on failure show `保存失败，请稍后重试。` and keep buttons interactive.

- [ ] **Step 5: Run PlayMode and inspect a 1290 x 2796 Game View capture**

Expected: hierarchy tests pass; no text overlap; controls are outside the shareable image; saved PNG is non-empty.

- [ ] **Step 6: Commit card redesign**

```bash
git add EndangeredAR/Assets/Scripts/UI/DemoAppController.cs \
  EndangeredAR/Assets/Tests/PlayMode/SensenVerticalSliceTests.cs
git commit -m "feat: redesign the Sensen knowledge card"
```

---

### Task 5: Full Regression and iPhone Acceptance

**Files:**
- Modify: `EndangeredAR/docs/verification/2026-08-06-sensen-device-acceptance.md`

**Interfaces:**
- Consumes: outputs of Tasks 1-4.
- Produces: signed iOS development build and updated evidence checklist.

- [ ] **Step 1: Run complete automated suites**

Run full EditMode, PlayMode, Python backend tests, `git diff --check`, and the secret scan from `ApiSecurityTests`. Record exact pass counts.

- [ ] **Step 2: Build signed iOS app**

Generate the Xcode project with `EndangeredARIosBuilder.Build`, build using automatic signing without globally overriding `PRODUCT_BUNDLE_IDENTIFIER`, and verify the app identifier is `com.yuanweijie.endangeredar` while `UnityFramework` remains `com.unity3d.framework`.

- [ ] **Step 3: Install and perform device checks**

Verify on iPhone 17 Pro Max:

1. Camera preview preserves real-world proportions and remains upright.
2. Two consecutive chat turns return backend responses; disabling the backend produces local fallback.
3. Mission entry does not claim a new reward; correct answer triggers the appropriate first-time or replay copy.
4. Card layout has no overlap; Save PNG reports success and produces a non-empty file.

- [ ] **Step 4: Update acceptance evidence**

Mark only observed checks as passed. Record any remaining device-only issue without claiming Phase 1 complete.

- [ ] **Step 5: Commit verification record**

```bash
git add EndangeredAR/docs/verification/2026-08-06-sensen-device-acceptance.md
git commit -m "test: record Sensen device defect verification"
```
