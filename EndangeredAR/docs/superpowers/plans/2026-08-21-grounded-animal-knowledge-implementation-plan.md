# R2 Grounded Animal Knowledge Implementation Plan

**Goal:** Ground Sensen scientific answers in one reviewed JSON knowledge source while preserving R1 routing, history isolation, and fallback behavior.

**Architecture:** `content/animals/sensen.json` is canonical. Python performs deterministic classification/retrieval for both Local and Cloud. Unity receives structured grounding metadata and uses a generated equivalent for its last-resort local fallback.

**Spec:** `EndangeredAR/docs/superpowers/specs/2026-08-21-grounded-animal-knowledge-design.md`

## Constraints

- Do not rewrite `AIManager`, `AIRouter`, request budgets, or `ChatRequestState`.
- Do not add vector search, embeddings, RAG infrastructure, streaming, native inference, animation execution, or task mutation.
- Do not add secrets, model files, or new package dependencies.
- Keep old conversation files readable.
- Use RED/GREEN tests and one independently revertible commit per task.

### Task 1: Canonical schema and reviewed sources

**Files:**
- Modify `content/animals/sensen.json`
- Add `server/animal_knowledge.py`
- Add `server/tests/test_animal_knowledge.py`
- Modify `EndangeredAR/Assets/Editor/AnimalContentAssetBuilder.cs`
- Modify `EndangeredAR/Assets/Scripts/Animals/AnimalKnowledgeProfile.cs`
- Modify `EndangeredAR/Assets/Resources/Animals/Sensen.asset`
- Modify `EndangeredAR/Assets/Resources/Animals/SensenKnowledge.asset`
- Modify `EndangeredAR/Assets/Tests/EditMode/SensenContentAssetTests.cs`

- [ ] Write schema validation tests for identity, unique IDs, source references, known-unknown population, and required verification metadata.
- [ ] Verify RED against the old JSON and old incorrect Unity scientific name.
- [ ] Add the reviewed schema/data and minimal Python loader/validator.
- [ ] Generate the Unity content asset from canonical JSON instead of factual literals.
- [ ] Run focused Python/Unity tests, then full tests.
- [ ] Commit `feat: add canonical Sensen knowledge sources`.

### Task 2: Deterministic retrieval and classification

**Files:**
- Modify `server/animal_knowledge.py`
- Modify `server/tests/test_animal_knowledge.py`
- Modify `EndangeredAR/Assets/Scripts/Animals/AnimalKnowledgeProfile.cs`
- Modify `EndangeredAR/Assets/Scripts/Chat/LocalKnowledgeChatService.cs`
- Modify `EndangeredAR/Assets/Tests/EditMode/LocalKnowledgeChatServiceTests.cs`

- [ ] Add RED tests for all requested topics, common Chinese paraphrases, population/fabrication/swimming refusals, social chat, off-domain, injection, and animal isolation.
- [ ] Implement deterministic normalization, intent classification, keyword scoring, known-unknown behavior, and app-owned citation resolution.
- [ ] Add the equivalent generated Unity fallback semantics without network or provider logic.
- [ ] Run focused and full suites.
- [ ] Commit `feat: add deterministic animal knowledge retrieval`.

### Task 3: Shared grounded provider context

**Files:**
- Modify `server/dev_server.py`
- Modify `server/tests/test_dev_server.py`

- [ ] Add RED tests that Local and Cloud receive identical evidence, insufficient/off-domain paths do not invite invention, citations cannot be provider-generated, and LocalFirst fallback remains evidence constrained.
- [ ] Inject the retrieval result into one shared prompt builder for `/chat` and `/chat/local`.
- [ ] Preserve `/chat/local` explicit failure semantics and `/chat` cloud-to-rule compatibility.
- [ ] Return reviewed grounded/insufficient answers and application-owned citation metadata.
- [ ] Run focused and full Python tests.
- [ ] Commit `feat: ground local and cloud animal answers`.

### Task 4: Unity response and source display

**Files:**
- Modify `EndangeredAR/Assets/Scripts/AI/AIContracts.cs`
- Modify `EndangeredAR/Assets/Scripts/API/ChatApiClient.cs`
- Modify `EndangeredAR/Assets/Scripts/AI/CloudLLMProvider.cs`
- Modify `EndangeredAR/Assets/Scripts/AI/LocalLLMProvider.cs`
- Modify `EndangeredAR/Assets/Scripts/AI/LocalKnowledgeProvider.cs`
- Modify `EndangeredAR/Assets/Scripts/UI/DemoAppController.cs`
- Modify relevant EditMode tests

- [ ] Add RED serialization/mapping tests for `answerMode`, `evidenceStatus`, and structured citations.
- [ ] Keep missing fields valid and old conversation persistence unchanged.
- [ ] Append one concise source line to grounded replies without redesigning chat UI.
- [ ] Verify citation metadata does not change route source/reason or stale-ticket behavior.
- [ ] Run focused and full Unity tests.
- [ ] Commit `feat: expose grounded citations in Unity chat`.

### Task 5: Twenty-question quality regression

**Files:**
- Add `server/tests/test_grounded_quality.py`
- Add or update a versioned 20-question fixture under `content/quality/`

- [ ] Encode the original question set as expected topic/evidence outcomes, not full-question production switches.
- [ ] Measure technical success, factual correctness, fabricated quantities, unsupported facts, correct refusals, citation coverage, youth readability, and Local/Cloud consistency.
- [ ] Verify the prior scientific-name, range, tree-hole, diet, and invented-population defects are closed.
- [ ] Run full Python and Unity suites.
- [ ] Commit `test: add grounded animal quality regression`.

### Task 6: Verification and review

**Files:**
- Add `EndangeredAR/docs/verification/2026-08-21-r2-grounded-animal-knowledge.md`
- Update operating documentation only where commands/contracts changed.

- [ ] Run fresh Python and Unity EditMode suites and scan Unity logs.
- [ ] Run `git diff --check`, secret scans, GGUF/large-file scans, and unrelated-scope diff review.
- [ ] Perform whole-branch independent review; resolve blocking findings with separate fix commits and rerun affected/full tests.
- [ ] Record before/after 20-question metrics, source set, known limitations, commits, and clean Git status.
- [ ] Commit `docs: verify R2 grounded animal knowledge` and stop before later roadmap work.
