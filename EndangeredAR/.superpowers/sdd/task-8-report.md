# Task 8 Legacy Resolver Follow-up

Date: 2026-07-19

## Changes

- A legacy profile is usable when it has a nonblank unknown reply, or an entry with both a nonblank reply and at least one nonblank keyword.
- Definition candidates are grouped by normalized animal ID. Every duplicate-key group is skipped before selecting the lexicographically first unique key.
- Standalone profiles use the same duplicate-safe process with normalized asset names.
- Serialized defaults retain first priority whenever usable. Explicit and null profile behavior, defensive copies, and animal-content isolation remain unchanged.

## Tests

- Red: `EndangeredAR.Tests.EditMode.LocalKnowledgeChatServiceTests` ran 10 tests: 7 passed, 3 failed. The failures were the reply-only, duplicate-definition-ID, and duplicate-profile-name regressions.
- Green: `EndangeredAR.Tests.EditMode.LocalKnowledgeChatServiceTests` ran 10 tests: 10 passed, 0 failed.
- Full EditMode: 49 passed, 0 failed, 0 skipped.

Artifacts:

- `/private/tmp/task-8-legacy-red.xml`
- `/private/tmp/task-8-legacy-green.xml`
- `/private/tmp/task-8-editmode-full.xml`
