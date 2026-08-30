# Architecture and Wave Asset Audit

Audit baseline: `ae79f227f71c5e0dd3ee90106a5d4d1639826440`

## Completion boundary

1. `OnDeviceAIResponseComposer` receives the Qwen completion and passes it to
   `AuthorityAwareResponseValidator.Validate`.
2. `ComposeResponse` stores the validator-approved `generated.Text.Trim()` in
   `AIResponse.reply`, resolves `answerMode`, and copies `ContentAuthority`.
3. `AIManager` applies memory-dialogue preparation and route provenance before
   returning the response to `DemoAppController`.
4. `DemoAppController.FinishCloudAnswer` rejects stale memory-dependent claims.
5. `TryResolveAICompletionWithAction` creates the existing action candidate and
   applies policy, capability, and interaction validation.
6. `TryResolveAICompletion` adds app-owned display material such as citations
   and mission hints.

The future classifier insertion point is after stale-response refresh succeeds
and before display/history persistence or action execution. Its text input
should remain the original validated reply, before application-owned citation
lines are appended. R3.4A does not add this call.

## Existing action surface

- `AIAction`: `None`, `Taunt`, `Eat` only.
- `CharacterCapabilityProfile`: Sensen advertises `Taunt` and `Eat` only.
- Candidate generation: `GroundedActionCandidateFactory` and the deterministic
  provider policy create current Eat/Taunt candidates in the completion flow.
- Enforcement: `AIActionPolicy` -> `CharacterCapabilityProfile` ->
  `AIInteractionValidator` -> `AnimalModelController`.
- `AnimalModelController`: supports only `Idle`, `Taunt`, and `Eat` animation
  specifications.

## Animator and clip inventory

- The formal Sensen Animator has only `Idle`, `Taunt`, and `Eat` states and
  parameters.
- The tracked FBX clip inventory contains Idle, Taunt, and the separate Eat
  clip.
- No tracked `Wave`, `Happy`, `Sad`, `Comfort`, or `Greeting` animation clip was
  found.
- The current character uses a Generic rig. A newly authored compatible clip is
  technically possible, but there is no reusable Wave asset to validate now.
- Taunt must not be relabeled or reused as Wave.

**Conclusion: R3.4B Asset Stage required.** R3.4A does not create Wave, edit the
Animator, edit capability assets, or connect any prediction to an action.
