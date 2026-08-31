# R3.4A.1 Split and Label Policy

## Label

`Greeting` means that the current user input and assistant reply form a direct
social greeting interaction suitable for later consideration as a Wave
candidate. `NotGreeting` covers every other interaction, including scientific
answers, state and memory queries, quoted greetings, negation, technical text,
prompt injection, and action requests.

Emotion labels from R3.4A are not trained and do not gate this recovery.

## Fixed split

The split is authored before model training. Each row records:

- `sourceType`;
- `generationMethod`;
- `label`;
- `scenarioFamily`;
- `promptTemplate`;
- `generationBatch`;
- `reviewStatus`;
- `rightsStatus`;
- `splitGroup`;
- `split`;
- `safetyCritical`.

Scenario family, prompt template, generation batch, and the composite split
group are each exclusive to Train, Dev, or Test. The source type is included in
the composite group but is common across the project-owned corpus. Normalized
User+Reply pairs are unique across all three splits.

Gold v2 uses separate scenario families and split groups. Exact normalized
pairs may not overlap Train, Dev, or Test.

## Input-form adequacy

Metrics count interactions, but the report also records unique effective inputs
for User-only, Reply-only, and Pair. Repeated user or reply text cannot be used
to claim that an input form has 100 independent positive examples. The first
Gold v2 adequacy target is 100 unique Greeting inputs and 200 unique
safety-critical negative inputs for the selected form.
