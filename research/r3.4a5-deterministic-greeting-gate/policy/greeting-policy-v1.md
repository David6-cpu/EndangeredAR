# Deterministic Greeting Policy v1

Policy version: `r3.4a5-greeting-intent-v1`

Scope version: `r3.4a5-greeting-scope-v1`

## Product question

The raw policy answers only whether the current user message contains a
direct, natural greeting intent. It does not inspect the assistant reply,
emotion, Memory, Progress, scientific facts, or task state.

## Normalization

- Unicode Form KC normalization;
- trim leading and trailing whitespace;
- lowercase invariant Latin text;
- collapse internal whitespace without deleting word boundaries;
- normalize common Chinese full-width punctuation;
- ignore repeated terminal period, exclamation mark, and question mark;
- accept up to three repeated direct-greeting particles (`呀`, `啊`, `哇`).

Normalization never removes negation, quotes, or arbitrary internal text.
Positive matching is token-boundary based; it does not use
`Contains("你好")`.

## Raw Greeting policy

Accepted families:

- direct: `你好`, `您好`, including a direct `森森` address;
- time of day: morning, forenoon, afternoon, evening, and `早安`;
- informal: `嗨`, `哈喽`, `哈啰`, `hello`, `hi`;
- meeting: direct first-meeting or glad-to-meet expressions;
- reunion: direct return or meet-again expressions.

`晚安` is rejected in v1 because it is primarily a farewell and its Wave
timing is ambiguous. `你好吗` is rejected because it is a wellbeing question,
not an unambiguous direct greeting for this high-precision action gate.

A mixed request may retain raw Greeting intent when a greeting forms a clear
opening clause. Product eligibility is still rejected by authoritative or
existing-action scope metadata. Prompt injection, technical commands,
negation, quoted speech, and Greeting-definition requests are rejected before
positive matching.

## Product Scope

Eligibility requires all of the following:

- raw Greeting intent is true;
- answer mode is strongly typed as SocialChat;
- `ContentAuthority.None`;
- final source is `AIFinalSource.OnDeviceLlm`;
- response validation passed;
- request ticket is current;
- current animal is valid;
- the interaction page is active;
- no existing Eat, Taunt, or other accepted action candidate.

Capability, controller support, controller busy state, action validation, and
animation playback remain future R3.4B/R3.4C gates.

## Side-effect boundary

`GreetingIntentResult` contains only `IsGreeting`, `ReasonCode`, and
`PolicyVersion`. `GreetingProductScopeResult` contains only `IsEligible`,
`ReasonCode`, and `PolicyVersion`. Neither result exposes an animation state,
trigger, action, controller call, Memory write, or Progress write.
