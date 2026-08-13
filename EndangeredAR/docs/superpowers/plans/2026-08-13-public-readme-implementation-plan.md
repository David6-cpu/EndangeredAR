# EndangeredAR Public README Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a factual, visually useful, and reproducible root README for the public EndangeredAR GitHub repository.

**Architecture:** Keep the repository root `README.md` as the public landing page and retain `server/README.md` as a focused backend reference. Reuse tracked design and verification artifacts, link to deeper documents instead of duplicating them, and validate every public claim against current files and tests.

**Tech Stack:** GitHub Flavored Markdown, Mermaid, Unity 2022.3.62f3c1, Python 3 standard library, GitHub CLI.

## Global Constraints

- Chinese is the primary language with a concise English subtitle.
- Sensen is the only completed and device-verified animal vertical slice.
- Do not claim that ARFoundation, ARKit image tracking, or ARCore is active.
- Do not include API keys, `.env.local` contents, local IP addresses, signing identities, or user-specific absolute paths.
- Use only repository-tracked visuals and repository-relative links.
- Report verified evidence as EditMode 83/83, PlayMode 13/13, backend 4/4, and iPhone 17 Pro Max device acceptance.
- Do not modify Unity scenes, packages, models, or runtime behavior.

---

### Task 1: Create the public repository landing page

**Files:**
- Create: `README.md`
- Reference: `EndangeredAR/Design/sensen-ar-redesigned-navigation-board.png`
- Reference: `EndangeredAR/docs/verification/2026-08-06-sensen-device-acceptance.md`

**Interfaces:**
- Consumes: tracked design board, project manifest, content assets, verification records, and backend commands.
- Produces: a root GitHub landing page with product positioning, architecture, setup, validation, limitations, and roadmap.

- [x] **Step 1: Verify the README does not already exist**

Run:

```bash
test ! -e README.md
```

Expected: exit code `0`.

- [x] **Step 2: Create the root README**

Create `README.md` with these exact section responsibilities:

```text
Title and factual badges
Chinese positioning plus English subtitle
Tracked design-board image
Current status and core experience flow
Feature table and technical highlights
Mermaid architecture diagram
Repository structure
Prerequisites and quick start
AI proxy configuration and phone networking note
Test commands and recorded evidence
Current limitations and roadmap
Contribution and security guidance
```

The quick start must use repository-relative paths and environment variables instead of machine-specific paths.

- [x] **Step 3: Confirm all named files and commands exist**

Run:

```bash
test -f EndangeredAR/Assets/Scenes/DemoScene.unity
test -f EndangeredAR/Assets/Editor/EndangeredARIosBuilder.cs
test -f EndangeredAR/Assets/Config/LocalApiConfig.asset
test -f server/dev_server.py
test -f server/.env.example
test -f EndangeredAR/Design/sensen-ar-redesigned-navigation-board.png
```

Expected: every command exits `0`.

### Task 2: Make the backend reference safe for a public repository

**Files:**
- Modify: `server/README.md`

**Interfaces:**
- Consumes: `server/.env.example` and `server/dev_server.py` behavior.
- Produces: concise backend-only instructions linked from the root README.

- [x] **Step 1: Identify public-document leaks**

Run:

```bash
rg -n '/Users/|192\.168\.|MOONSHOT_API_KEY=.+' server/README.md
```

Expected before the change: the existing machine-specific path and example LAN IP are reported.

- [x] **Step 2: Replace machine-specific instructions**

Document the following portable flow:

```bash
cp server/.env.example .env.local
python3 server/dev_server.py
curl http://127.0.0.1:8000/health
```

Explain that a phone must use the development machine's reachable LAN address or a deployed HTTPS endpoint without publishing a concrete address.

- [x] **Step 3: Verify the backend tests**

Run:

```bash
python3 -m unittest discover -s server/tests -v
```

Expected: `Ran 4 tests` and `OK`.

### Task 3: Validate, commit, and publish the documentation

**Files:**
- Validate: `README.md`
- Validate: `server/README.md`
- Validate: `EndangeredAR/docs/superpowers/specs/2026-08-13-public-readme-design.md`
- Validate: `EndangeredAR/docs/superpowers/plans/2026-08-13-public-readme-implementation-plan.md`

**Interfaces:**
- Consumes: completed documentation from Tasks 1 and 2.
- Produces: a clean commit pushed to both public `main` and the working feature branch.

- [x] **Step 1: Scan public docs for prohibited values**

Run:

```bash
rg -n '/Users/|192\.168\.|gho_|sk-[A-Za-z0-9_-]{16,}|MOONSHOT_API_KEY=.+' README.md server/README.md
```

Expected: no matches.

- [x] **Step 2: Verify repository-relative links**

Check each local link target extracted from `README.md` with `test -e`. Expected: all image and document paths exist.

- [x] **Step 3: Check formatting and scope**

Run:

```bash
git diff --check
git status --short
```

Expected: no whitespace errors and only the four intended documentation files differ from the previously published commit.

- [x] **Step 4: Commit the README implementation**

```bash
git add README.md server/README.md EndangeredAR/docs/superpowers/plans/2026-08-13-public-readme-implementation-plan.md
git commit -m "docs: add public project README"
```

- [ ] **Step 5: Push the verified commit**

```bash
git push origin HEAD:feature/multi-animal-foundation
git push origin HEAD:main
```

Expected: both remote branches point to the new documentation commit.

- [ ] **Step 6: Verify the GitHub repository**

Run:

```bash
gh repo view David6-cpu/EndangeredAR --json url,visibility,defaultBranchRef
```

Expected: `visibility` is `PUBLIC`, default branch is `main`, and the repository homepage renders the new README.
