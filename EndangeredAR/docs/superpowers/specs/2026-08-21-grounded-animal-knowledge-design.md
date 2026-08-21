# R2 Grounded Animal Knowledge Design

## Objective

Build one small, auditable knowledge foundation for Sensen so scientific answers are derived from reviewed project evidence instead of provider memory. R2 preserves the R1 provider/router boundary and adds deterministic retrieval, evidence status, and application-owned citations.

## Current Knowledge Audit

Knowledge is currently duplicated across:

- `content/animals/sensen.json`, read by Python.
- `Assets/Resources/Animals/Sensen.asset` and `SensenKnowledge.asset`, read by Unity.
- `AnimalContentAssetBuilder.cs`, which hardcodes the generated assets.
- `dev_server.py`, which hardcodes rule matching and prompt facts.
- A few presentation strings in the scene builder.

The copies are not equivalent. The project currently associates the Chinese name `缨冠灰叶猴` with `Trachypithecus poliocephalus`; reviewed taxonomy sources associate that Chinese common name with `Semnopithecus priam`. The current endangered level and several habitat/threat claims also lack a shared source record.

## Species Identity Decision

R2 treats Sensen as:

- Chinese common name: 缨冠灰叶猴
- English common name: Tufted Gray Langur
- Scientific name: `Semnopithecus priam`
- Taxonomic scope: species level; subspecies-specific claims are not generalized unless explicitly marked.

This identity is supported by GBIF and the Mammal Diversity Database. The IUCN 2020 assessment for `Semnopithecus priam` is the primary source for range, habitat, population status, threats, and conservation actions.

## Canonical Source

`content/animals/sensen.json` is the only manually maintained knowledge source. Python reads it directly. Unity ScriptableObjects remain build/runtime artifacts generated from the canonical JSON by `AnimalContentAssetBuilder`; their factual fields must not be independently authored.

The canonical schema contains:

```text
schemaVersion
animalId
identity
presentation
sources[]
facts[]
defaultSuggestions[]
```

Each fact has a stable `factId`, topic, reviewed claim, child-friendly approved answer, keywords, aliases, source IDs, confidence, verification date, and evidence status. Each source has a stable `sourceId`, title, organization/author, type, URL, publication/update date, project verification date, applicable fact IDs, and notes.

`known_unknown` facts are first-class evidence. For example, the IUCN assessment states that global population size is unknown; a population question therefore returns a cited refusal instead of an invented number.

## Source Set

R2 uses this reviewed source set:

| Source ID | Purpose | Source |
| --- | --- | --- |
| `gbif-4267223` | accepted name and Chinese common name | GBIF species 4267223 |
| `mdd-1000692` | taxonomy, English name, distribution summary, current IUCN category | Mammal Diversity Database |
| `iucn-2020-s-priam` | range, habitats, ecology, unknown population size/trend, threats, conservation actions | IUCN Red List 2020 assessment |
| `cites-appendix-i-2023` | CITES Appendix I listing | CITES Appendices |
| `s-priam-diet-2021` | reviewed diet evidence for `S. p. priam` | peer-reviewed diet study |

No model output is a source. Claims without adequate evidence remain absent or are marked insufficient; fields are not filled for completeness.

## Retrieval Contract

Python owns the canonical retrieval used by both HTTP providers. Unity carries a generated equivalent only for the final offline knowledge fallback.

```text
question
  -> normalize Chinese punctuation/case/whitespace
  -> isolate animalId
  -> classify intent
  -> score topic aliases and fact keywords
  -> return RetrievalResult
```

`RetrievalResult` contains:

- `answerMode`: `grounded_fact`, `social_chat`, `off_domain`
- `evidenceStatus`: `evidence_found`, `insufficient_evidence`, `not_required`
- matching facts in deterministic score/order
- application-owned citations derived only from matching `sourceIds`
- an approved grounded or insufficient-evidence answer where applicable

The supported fact topics are identity, taxonomy, scientific name, range, habitat, diet, behavior, threats, population, conservation status, conservation actions, youth actions, and fun facts.

## Scientific Answer Boundary

For scientific questions:

1. Retrieval runs before provider selection.
2. Evidence is serialized as quoted data under an explicit untrusted-evidence delimiter.
3. Local and Cloud receive the same evidence and factual constraints.
4. The provider cannot create source IDs, URLs, facts, actions, or task changes.
5. The returned factual text is constrained to the approved answer represented by the retrieval result.
6. Citations are constructed by application code from actual matching sources.
7. Missing evidence returns a friendly deterministic insufficient-evidence response.

This prevents a Local failure followed by Cloud from replacing missing evidence with provider memory.

Social conversation may use the selected provider naturally, but the prompt forbids introducing unsupported scientific claims. Off-domain questions receive a short deterministic redirect to endangered-animal learning.

## Provider Integration

R1 routing is unchanged:

```text
Unity Chat UI
  -> AIManager / AIRouter
  -> LocalLLMProvider -> Python /chat/local
  -> CloudLLMProvider -> ChatApiClient -> Python /chat
  -> LocalKnowledgeProvider
```

Both Python routes call the same retriever before constructing provider messages. A route may differ by provider availability, but its evidence set and citation set cannot differ. `source` and `routeReason` continue to describe the actual R1 route; `answerMode`, `evidenceStatus`, and `citations` describe knowledge grounding.

## Response Contract

R2 extends the additive JSON contract:

```text
reply
answerMode
evidenceStatus
citations[] { sourceId, title, organization, url }
source
routeReason
```

Missing new fields remain valid for backward compatibility. Existing conversation persistence stays `{ role, content }`; no stored schema migration is required. Unity may append a concise `资料来源` line to the visible answer, but it does not persist provider internals or execute model content.

## Security

- Evidence text is data, never instructions.
- History may contain user prompt injection but cannot override the system fact boundary.
- Citations are application-owned and filtered against canonical source IDs.
- Knowledge text cannot invoke Unity methods or mutate mission, badge, animation, or progress state.
- Cloud credentials remain only in the ignored server environment file.
- Model weights remain outside Git.

## Risks and Controls

- Common names can be ambiguous: store explicit taxon identity and scope notes.
- Taxonomy/status can change: record source dates and `lastVerified` per fact.
- Generated Unity assets can drift: builder and EditMode tests compare them with canonical JSON.
- Keyword retrieval can miss paraphrases: maintain reviewed aliases and a generic scientific-question detector; do not add vectors in R2.
- A provider can ignore prompt instructions: citations and approved factual response selection remain application-owned.
- Subspecies diet data can be overgeneralized: mark its scope and phrase the species answer conservatively.

