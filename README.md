# EndangeredAR

基于增强现实、可信动物知识与 iPhone 本机 AI 的珍稀及受保护野生动物智能科普系统

[![Unity](https://img.shields.io/badge/Unity-6000.0.76f1-222C37?logo=unity)](https://unity.com/releases/editor/archive)
![Platform](https://img.shields.io/badge/Platform-iOS%2027%20%7C%20Unity%20Editor-2E6245)
![Status](https://img.shields.io/badge/Status-R3.3C%20On--device%20Dialogue%20In%20Validation-5EBB78)
![Repository](https://img.shields.io/badge/Repository-Public-C9A33A)

## 项目简介

**EndangeredAR** 是一款面向青少年科普、研学活动、动物园及自然教育场景的智能 AR 应用。项目通过 Unity 构建可交互的数字动物角色，结合 iPhone 本机轻量模型和经过审核的动物知识库，为用户提供具有真实来源引用的自然语言科普问答。Cloud 仅保留为 Development-only 显式对照路径，不是正式 iOS 聊天依赖或自动 fallback。

当前核心角色为缨冠灰叶猴“森森”（*Semnopithecus priam*）。系统不仅能够回答用户关于物种身份、食性、分布和保护状态的问题，还能在严格的白名单、角色能力和运行时校验下，根据明确互动意图或可信知识主题驱动角色播放相应动画。

项目坚持“模型负责表达，知识库负责事实，应用层负责权限”的设计原则。AI 可以读取有限的业务上下文，但不能直接修改任务、进度、徽章、解锁状态或动画权限。

![EndangeredAR 产品界面总览](EndangeredAR/Design/sensen-ar-redesigned-navigation-board.png)

## 核心能力

- **AR 数字动物交互**：支持角色展示、单指旋转、双指缩放和页面生命周期管理。
- **iPhone 本机问答**：正式 iOS 聊天通过 llama.cpp + Metal 在设备上运行 Qwen2.5-1.5B；Mac 和 Cloud 路径仅保留为显式开发对照工具。
- **可信动物知识**：科学事实来自 canonical 动物知识库，并返回可追踪引用。
- **事实约束与拒绝编造**：资料不足时明确说明证据不足，不虚构学名、数量、分布或保护等级。
- **安全角色动作**：AI 或知识系统只能产生动作候选，最终必须经过 Policy、Capability、Validator 和角色控制器。
- **知识驱动行为**：食性问题在真实 diet 证据和 citation 成立时，可安全触发森森的 Eat 动画。
- **只读业务上下文**：AI 可读取有限的解锁、知识、徽章和任务完成状态，但没有业务写权限。
- **事件型角色记忆**：应用会在真实业务状态成功保存后，以强类型、幂等且有界的事件记录动物发现、任务完成、知识学习和徽章获得等里程碑。记忆独立保存在本地，可清除、可恢复，并与核心业务进度隔离。
- **受控记忆对话**：长期里程碑经最小化投影后可供本机 Qwen 组织回复；模型不读取原始事件，也不能写 Memory 或 Progress。
- **真机验证**：已在 iOS 27 与 Unity 6000.0.76f1 环境通过 R3.3C0 本机推理 Spike、R3.3C1 Production Chat 核心 Gate 和 History Boundary 稳定性 Gate；完整 R3.3C A-H 仍待继续。

## 当前角色：森森

- 中文名：缨冠灰叶猴
- 学名：*Semnopithecus priam*
- 英文名：Tufted Gray Langur
- IUCN 状态：近危（Near Threatened，NT）
- 种群趋势：下降
- 全球种群总量：缺少可靠的统一估算
- CITES：附录 I

> CITES 附录 I 与 IUCN“濒危（EN）”不是同一评价体系。森森当前的 IUCN 等级是近危（NT），不能描述为 IUCN 濒危（EN）。

## 技术架构概览

```text
Chat UI
  ↓
Unity Intent / Content Authority
  ↓
Canonical Evidence / CurrentProgress / CharacterMemory / SystemPolicy
  ↓
Unity Trusted Prompt Builder
  ↓
OnDeviceLLMProvider
  ↓
iPhone llama.cpp + Qwen2.5-1.5B + Metal
  ↓
Unity Response Validator
  ↓
Trusted citations / Action metadata
  ↓
UI / Character
```

核心原则是：**模型负责表达，知识库负责事实，应用层负责权限。** 正式 iOS 聊天在设备上完成检索、Prompt 构造、Qwen 推理和回答校验，不依赖 Python、HTTP、Mac、局域网或 Cloud。模型不能决定引用、业务写入权限或直接调用 Animator。

## 技术栈

- Unity `6000.0.76f1`
- C# / Python
- iOS 27 / Xcode 27
- llama.cpp
- Qwen2.5-1.5B-Instruct GGUF Q4_K_M（iPhone 本机推理）
- llama.cpp iOS arm64 / Metal
- Moonshot API（Development-only 对照路径）
- JSON canonical knowledge corpus
- Unity Generic Rig / Animator
- Unity UI / TextMeshPro / glTFast `6.18.0`

当前稳定包清单未包含 AR Foundation、ARKit XR Plugin 或 ARCore XR Plugin。扫描体验使用 iOS 相机预览与手动/模拟识别兜底；真实图片追踪仍属于后续工作。

## 当前开发进度

- **R3.3C0 已完成**：iPhone 原生 llama.cpp + Metal 成功加载 Qwen2.5-1.5B，并在 Mac Python、Mac llama-server、Wi-Fi 和蜂窝数据均关闭时完成真实 `on_device_llm` 推理。
- **R3.3C1 核心 Gate 已完成**：正式底部 Chat UI 已接入 Unity 本地事实权限、Trusted Prompt Builder、`OnDeviceLLMProvider`、iPhone Qwen 和 Unity Response Validator。正式聊天对 Python、HTTP、Mac、LAN、Cloud 和 Unity fallback chat 的运行时依赖均为 0。
- **AI Route Provenance 与 Local-LLM-only Gate 已完成**：Development 面板可区分事实权限、语言生成器和最终来源；正式 iOS 本机模型失败时显示 `system_status`，不会以 Cloud、server response 或 Unity 固定话术伪装成正常角色回复。
- **History Boundary 稳定性 Gate 已通过**：`你记得我以前问过什么吗？` 与 `你记得我以前问过你吃什么吗？` 已在真机多次由 `on_device_llm` 按 SystemPolicy 回答；没有编造聊天历史、误入 Diet Grounding 或触发 Eat/Taunt。
- **完整 R3.3C A-H 尚未完成，因此 R3.3C 尚未 fully accepted。** 当前功能分支仍处于最终验收阶段。

## 当前限制

- 当前 on-device 闭环仅完成 iOS；Android 端本机推理尚未接入。
- 当前完整角色闭环主要围绕森森实现，尚未完成大规模多物种扩展。
- 森森为卡通化数字角色原型，后续仍可继续进行物种特征和美术精修。
- R3.3C1 已将受控长期记忆投影接入 iPhone 本机 Qwen，但完整 R3.3C A-H 最终验收仍待继续。
- 角色记忆目前仅记录由真实业务状态产生的结构化里程碑，不保存完整聊天、用户自由文本、LLM 回复、昵称、好感度或情绪值。
- 当前仍为单一本地 Profile，不代表已经支持账号、多用户、云同步或跨设备记忆。
- 正式商业化所需的长期性能监控、隐私政策和多用户体系仍属于后续工作。


## 仓库结构

```text
.
├── EndangeredAR/               # Unity 工程
│   ├── Assets/
│   │   ├── Config/             # API 等 ScriptableObject 配置
│   │   ├── Resources/Animals/  # 动物定义、知识与任务资产
│   │   ├── Scenes/             # DemoScene
│   │   ├── Scripts/            # API、动物、进度、任务、UI
│   │   ├── StreamingAssets/    # GLB 模型与纹理
│   │   └── Tests/              # EditMode / PlayMode 测试
│   ├── Design/                 # 产品设计图
│   └── docs/                   # 设计、实施计划与验收记录
├── content/animals/            # 唯一人工维护的动物知识 JSON
├── content/quality/            # 固定质量回归问题集
└── server/                     # Development 对照、回归与 Python 测试工具
```

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/David6-cpu/EndangeredAR.git
cd EndangeredAR
```

> 仓库包含较大的 GLB 模型，首次克隆需要一些时间。

### 2. 打开 Unity Demo

1. 使用 Unity Hub 安装并打开 Unity `6000.0.76f1`。
2. 在 Unity Hub 中选择仓库内的 `EndangeredAR/` 目录。
3. 打开 `Assets/Scenes/DemoScene.unity`。
4. 等待脚本编译和资源导入完成后进入 Play Mode。

请直接使用已经审查和验证过的 `DemoScene`。除非你明确要重新生成场景，否则不要运行 `Endangered AR > Build Demo Scene`，因为该菜单会重建场景内容。

### 3. 可选：启动开发对照 AI 服务

正式 iOS 聊天不需要 Python 或 Mac llama.cpp。以下服务只用于 Unity Editor、Mac 基准和 Development-only 对照：

```bash
cp server/.env.example .env.local
# 默认 LocalOnly 不需要 MOONSHOT_API_KEY
python3 server/dev_server.py
```

健康检查：

```bash
curl http://127.0.0.1:8000/health
```

预期输出：

```json
{"status": "ok"}
```

要启用本地模型，先在另一个终端启动一个 OpenAI-compatible llama.cpp 服务：

```bash
llama-server \
  -m "<path-to-qwen-gguf>" \
  --host 127.0.0.1 \
  --port 8080 \
  -c 4096 \
  -np 1
```

`-c 4096 -np 1` gives the single inference slot the full 4096-token context. Do not combine this context size with four parallel slots for the current mobile acceptance flow; trusted evidence, read-only context, and bounded session history can exceed a 1024-token per-slot budget.

然后确认 `.env.local` 包含：

```dotenv
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=
LOCAL_LLM_TIMEOUT=7
```

`LOCAL_LLM_MODEL` 可留空；是否需要模型名取决于所使用的兼容服务。完整启动、接口调用和故障排查见 [`server/README.md`](server/README.md)。

Unity Editor 的开发远程路由可使用 `http://127.0.0.1:8000`。`CloudOnly` 仅供 Editor 或 Development Build 显式对照测试，正式 iOS 默认链路不会自动切换 Cloud。

iPhone 真机只有在显式选择 Development 远程对照路由时才需要开发机地址。该地址不属于正式 on-device 聊天配置。现有菜单可设置 Development 配置：

```text
Endangered AR > Set Local API To Mac LAN IP
```

该菜单当前不会同步更新所有 Development 远程配置；它不影响正式 iOS 的 on-device 路由。

### 4. 构建 iOS

1. 在 Unity 中选择 `Endangered AR > Build iOS Xcode Project`。
2. 使用 Xcode 打开生成的 `Unity-iPhone.xcodeproj`。
3. 配置自己的开发团队和 Bundle Identifier。
4. 连接已信任且开启开发者模式的 iPhone，签名并运行。

Unity 6000.0.76f1 生成的 iOS 工程包含当前系统所需的 `UIScene` 生命周期支持，并已在 iOS 27 真机完成正常启动验收。

on-device 构建前需通过 `ENDANGERED_AR_MODEL_PATH` 和 `ENDANGERED_AR_LLAMA_XCFRAMEWORK_PATH` 提供已批准的 GGUF 与 llama.cpp XCFramework。构建工具会校验模型身份、临时打包并在结束后清理 staging；模型权重和框架不进入 Git。

## AI 配置与安全

`.env.local` 的格式来自 [`server/.env.example`](server/.env.example)：

```dotenv
MOONSHOT_API_KEY=
MOONSHOT_BASE_URL=https://api.moonshot.cn/v1
MOONSHOT_MODEL=moonshot-v1-8k
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=
LOCAL_LLM_TIMEOUT=7
```

- `.env.local` 已被 `.gitignore` 排除，**不要提交真实密钥**。
- Unity 客户端不保存 Moonshot Key，也不发送 `Authorization: Bearer` 到供应商。
- 正式 iOS 默认路径：Unity → `OnDeviceLLMProvider` → iOS native llama.cpp → Qwen2.5-1.5B。聊天不经过 Python、HTTP、Mac、LAN 或 Cloud。
- `DevelopmentRemoteOnly`：仅用于显式开发对照，通过 Python adapter 访问 Mac llama.cpp；它不是正式产品的 Local LLM。
- `CloudOnly`：仅允许在 Editor 或 Development Build 中显式选择，用于 Moonshot 对照验证，不是默认 fallback。
- 历史自动 fallback 配置仅为兼容保留；正式 iOS 不会自动尝试 Cloud、server rule、server knowledge 或 Unity 固定聊天兜底。
- `contentAuthority` 标识事实权限来源：R2 canonical knowledge、R3.3A current progress、R3.3B character memory 或 system policy；`languageGenerator` 标识实际语言生成器。
- 正常 iOS 默认回复必须为 `source=on_device_llm`、`languageGenerator=on_device_llm`。本机模型失败时为 `source=system_status`，错误状态不作为森森回复写入聊天历史。
- `answerMode` 区分 `grounded_fact`、`social_chat` 和 `off_domain`；`evidenceStatus` 区分有证据、证据不足和无需证据。
- `citations` 由应用根据检索结果生成，包含稳定 `sourceId`、标题、机构和 URL；模型输出中的伪造引用不会进入响应。
- 森森唯一人工维护知识源是 [`content/animals/sensen.json`](content/animals/sensen.json)。Unity 的 `Sensen.asset` 与 `SensenKnowledge.asset` 由 `AnimalContentAssetBuilder` 从该文件生成，不应手工维护第二份事实。
- 正式 iOS 生成使用有界 Prompt、Token Budget 和超时；Development remote 路由保留独立的有界超时，但不进入生产聊天链路。
- Session History 与 Character Memory 严格分离；正式 iOS Prompt 中的最近聊天按 Token Budget 截断，且不会自动写入长期角色记忆。
- 当前知识系统使用小型确定性检索，不包含向量数据库、Embedding 或流式输出。R2 提供可信事实和引用，iPhone 本机 Qwen 只负责最终语言表达。角色动作只能由应用生成候选并经过 Policy、Capability、Validator 和角色控制器，模型不能直接控制动画或任务。

更完整的后端说明见 [`server/README.md`](server/README.md)。

## 测试与验证

### 后端测试

```bash
python3 -m unittest discover -s server/tests -v
```

### Unity EditMode

```bash
UNITY="${UNITY_EDITOR:?Set UNITY_EDITOR to the Unity executable}"
mkdir -p TestResults
"$UNITY" -batchmode -nographics \
  -projectPath "$PWD/EndangeredAR" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/endangeredar-editmode.xml" \
  -logFile "$PWD/TestResults/endangeredar-editmode.log"
```

### Unity PlayMode

```bash
"$UNITY" -batchmode -nographics \
  -projectPath "$PWD/EndangeredAR" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/endangeredar-playmode.xml" \
  -logFile "$PWD/TestResults/endangeredar-playmode.log"
```

最近一次完整回归记录：

| 验证项 | 结果 |
| --- | ---: |
| Unity EditMode | 552 / 552 passed |
| Unity PlayMode | 39 / 39 passed |
| Python backend | 130 / 130 passed |
| iOS 构建 | Unity 6 导出、Xcode 签名构建与真机安装成功 |
| 真机范围 | R3.3C0、R3.3C1 核心 Gate 与 History Boundary 稳定性 Gate 已通过；完整 A-H pending |

真机构建背景、设备检查项和已知边界见 [`Sensen iPhone Device Acceptance`](EndangeredAR/docs/verification/2026-08-06-sensen-device-acceptance.md)。自动化测试不能替代相机比例、模型材质、手势、PNG 输出等真机人工检查。

## 已知问题与发布边界

- 扫描页提供相机预览与手动/模拟识别；真实图片追踪尚未恢复。
- Python AI adapter 当前仅用于 Development 对照、回归和基准，不是正式 iOS 运行依赖，也不是生产级账号或高并发后台。
- iOS App Store 发布前仍需准备正式应用图标、隐私文案、分发配置以及长时间性能与热状态验证。
- 第二动物模型资源已进入仓库，但角色内容、任务、识别映射和完整真机验收尚未完成。

## Roadmap

1. 完成 R3.3C 剩余 A-H 真机验收，再决定是否合并到稳定 `main`。
2. 完成第二动物的数据资产、独立任务、角色 Prompt 和模型验收。
3. 将扫描解锁与图鉴入口扩展为真正的多动物选择闭环。
4. 为第二动物建立同一 Canonical Knowledge Schema，并先完成来源核验再录入事实。
5. 在稳定包基线之上评估恢复 ARFoundation 图片追踪。
6. 完成 App Store 图标、隐私清单、性能与长时间真机测试。

## 参与项目

欢迎通过 Issue 提交以下内容：

- 濒危动物资料校对与保护行动建议
- Unity 移动端适配问题
- 新动物的低面数、授权清晰的 3D 模型建议
- 科普任务和青少年交互体验改进

提交代码前请确保不包含 API Key、签名文件、个人路径、Unity `Library/` 或构建产物，并至少运行与改动相关的测试。

## 许可与素材

仓库目前尚未添加开源许可证，因此默认保留所有权利。代码、字体、图片和 3D 模型可能具有不同的授权来源；在复用、再发布或商业使用前，请分别确认对应素材的许可条件。后续会在整理模型来源和第三方声明后补充正式的 `LICENSE` 与素材清单。

## 文档

- [产品与多动物架构设计](EndangeredAR/docs/superpowers/specs/2026-07-18-multi-animal-product-design.md)
- [森森稳定基线](EndangeredAR/docs/verification/2026-07-19-sensen-baseline.md)
- [iPhone 真机验收记录](EndangeredAR/docs/verification/2026-08-06-sensen-device-acceptance.md)
- [R1 端云协同 AI 验收说明](EndangeredAR/docs/verification/2026-08-21-r1-hybrid-ai-routing.md)
- [R2 Grounded Animal Knowledge 验收说明](EndangeredAR/docs/verification/2026-08-21-r2-grounded-animal-knowledge.md)
- [R3.3A Read-only Context 验收说明](EndangeredAR/docs/verification/2026-08-24-r3.3a-readonly-context-foundation.md)
- [R3.3B Event-based Character Memory 验收说明](EndangeredAR/docs/verification/2026-08-25-r3.3b-event-memory-acceptance.md)
- [R3.3C AI Route Provenance Gate](EndangeredAR/docs/verification/2026-08-26-r3.3c-ai-route-provenance-gate.md)
- [R3.3C0 iPhone On-device LLM Spike 验收说明](EndangeredAR/docs/verification/2026-08-27-r3.3c0-on-device-llm-spike.md)
- [R3.3C1 Production On-device Chat Integration 验收说明](EndangeredAR/docs/verification/2026-08-27-r3.3c1-production-on-device-chat-integration.md)
- [UI Design System](EndangeredAR/DESIGN.md)

---

这个项目仍在持续开发。当前目标不是堆叠功能，而是把每个动物都做成一条可信、可互动、可学习、可验证的完整体验。
