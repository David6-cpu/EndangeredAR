# EndangeredAR Public README Design

**Date:** 2026-08-13  
**Status:** Proposed for implementation

## Goal

Create a public repository landing page that helps competition reviewers, educators, developers, and potential collaborators understand the product within one minute and run the verified Sensen demo without reading the implementation first.

## Audience

1. Competition reviewers evaluating innovation, educational value, and demo completeness.
2. Unity developers evaluating the architecture or preparing a local build.
3. Educators and collaborators interested in endangered-animal science communication.

## Content Structure

The root `README.md` will use Chinese as the primary language with a concise English subtitle. Its sections will appear in this order:

1. Project title, one-sentence positioning, current maturity, and the existing product design board.
2. Core experience flow: scan, meet Sensen, chat, complete the food mission, unlock progress, and export a knowledge card.
3. Product and technical highlights.
4. Current verified scope and explicit limitations.
5. Architecture and data flow.
6. Repository structure.
7. Quick start for Unity and the local AI proxy.
8. Configuration and security notes.
9. Test commands and recorded verification evidence.
10. Roadmap for the next animal and a production backend.
11. Contribution guidance and project status.

## Visual Treatment

- Use the tracked `EndangeredAR/Design/sensen-ar-redesigned-navigation-board.png` as the primary visual.
- Keep badges restrained and factual; do not add unsupported CI, release, coverage, or platform badges.
- Prefer short tables, code blocks, and one Mermaid architecture diagram over long prose.
- Do not embed temporary Downloads screenshots or machine-local absolute paths.

## Fact Boundaries

- Describe the project as a Unity mobile endangered-animal education app with camera-assisted recognition and a simulated/manual recognition fallback.
- State that Sensen is the completed and device-verified vertical slice.
- State that the codebase has a data-driven multi-animal foundation, but do not claim that a second animal experience is complete.
- Do not claim ARFoundation, ARKit image tracking, or ARCore is active; those packages are intentionally absent from the stable demo.
- Explain that the Python proxy supports Moonshot when configured and local role-based fallback when no provider key is present.
- Never include API keys, local `.env.local` contents, developer signing details, LAN addresses, or user-specific filesystem paths.
- Report verification evidence from the repository records: EditMode 83/83, PlayMode 13/13, backend 4/4, and iPhone 17 Pro Max device acceptance.

## Quick-Start Contract

The README must provide a reproducible path:

1. Clone the repository.
2. Open `EndangeredAR` with Unity `2022.3.62f3c1`.
3. Open `Assets/Scenes/DemoScene.unity`.
4. Optionally copy `server/.env.example` to `.env.local` and set the Moonshot key.
5. Start `python3 server/dev_server.py`.
6. Point the Unity API configuration at the reachable proxy address when testing on a phone.
7. Run in the Editor or build the included iOS Xcode project through the project menu.

The instructions must distinguish Editor localhost access from phone LAN access and must note that a public HTTPS backend is recommended for production.

## Acceptance Criteria

- A new visitor can identify the product, current completion level, and core flow without opening another document.
- Every repository-relative image and document link resolves.
- Commands match files and scripts currently present in the repository.
- No secret, local IP, signing identity, or absolute local path appears in the README.
- The README renders cleanly on GitHub in desktop and mobile widths.
- The public default branch receives the committed README after review.
