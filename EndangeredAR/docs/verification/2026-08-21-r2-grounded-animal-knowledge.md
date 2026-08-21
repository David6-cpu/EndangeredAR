# R2 Grounded Animal Knowledge Acceptance

Date: 2026-08-21  
Branch: `codex/r2-grounded-animal-knowledge`  
Base: `f0df272d` (`docs: close R1 acceptance gates`)

## 1. Scope and result

R2 establishes a single-animal grounded knowledge foundation for Sensen without replacing the R1 Provider/Router architecture. Scientific questions are classified and retrieved before Local or Cloud generation. Provider text cannot overwrite approved factual answers or application-owned citations.

Implemented:

- canonical Sensen knowledge and source metadata;
- deterministic Chinese keyword/alias retrieval with animal isolation;
- grounded, insufficient-evidence, social, off-domain, and prompt-extraction handling;
- identical evidence for Local and Cloud providers;
- backward-compatible Unity response fields and concise citation display;
- the original R1.5 twenty-question quality regression.

Not implemented: vectors, Embedding, Chroma/Milvus/FAISS, fine-tuning, streaming, native mobile LLM, animation control, task mutation, reward changes, or bulk animal ingestion.

## 2. Canonical source and schema

The only manually maintained Sensen knowledge source is:

`content/animals/sensen.json`

Top-level fields:

- `schemaVersion`, `animalId`
- `identity`: Chinese/English/common/scientific names and taxonomy
- `presentation`: role copy, suggestions, unknown reply, asset keys
- `sources[]`: traceable source metadata
- `facts[]`: stable scientific facts

Each fact has `factId`, `topic`, `claim`, `approvedAnswer`, `displayValue`, `keywords`, `aliases`, `items`, `sourceIds`, `confidence`, `evidenceStatus`, `lastVerified`, and `notes`.

Each source has `sourceId`, `title`, `organization`, `sourceType`, `url`, `publishedOrUpdatedDate`, `projectVerifiedDate`, `appliesToFactIds`, and `notes`.

`EndangeredAR/Assets/Resources/Animals/Sensen.asset` and `SensenKnowledge.asset` are generated from this JSON by `AnimalContentAssetBuilder`. They are runtime artifacts, not a second authoring source.

## 3. Reviewed species identity and sources

The project identity is explicitly fixed to:

- Chinese name: 缨冠灰叶猴
- English name: Tufted Gray Langur
- Scientific name: `Semnopithecus priam`
- Family: Cercopithecidae
- Genus: `Semnopithecus`

Sources:

| Source ID | Organization | Use |
| --- | --- | --- |
| `gbif-4267223` | GBIF Secretariat | accepted identity and taxonomy |
| `mdd-1000692` | American Society of Mammalogists, Mammal Diversity Database | taxonomy, English name, range, status display |
| `iucn-2020-s-priam` | IUCN Red List | range, ecology, threats, population known-unknown, status, conservation actions |
| `cites-appendix-i-2023` | CITES Secretariat | Appendix I listing |
| `vanaraj-pragasan-2021-diet` | Ethology Ecology & Evolution | scoped diet observations for one `S. p. priam` group |

The global population fact is deliberately recorded as `known_unknown`: the reviewed assessment provides no exact global count and reports a declining trend. A local study number is never promoted to a global estimate.

## 4. Data flow and constraints

```text
Unity Chat UI
  -> AIManager / AIRouter
  -> LocalLLMProvider or CloudLLMProvider
  -> Python /chat/local or /chat
  -> load content/animals/<animalId>.json
  -> deterministic retrieve(animalId, normalized question)
  -> shared grounded prompt for Local and Cloud
  -> application selects approvedAnswer and canonical citations
  -> AIResponse -> existing chat history and bubble
  -> Unity LocalKnowledgeChatService only when HTTP providers fail
```

The Python and Unity fallback retrievers both use topic keywords and aliases from the canonical document. Fact matches are evaluated before conversational markers, so a greeting cannot turn “你好，你的学名是什么” into ungrounded chat. Prompt-extraction attempts are deterministic and do not reach a model unless they also contain a supported scientific fact, in which case only the fact is returned.

Scientific-answer rules:

1. Provider messages receive the same retrieved evidence.
2. Evidence is wrapped as untrusted data and cannot override system rules.
3. For `evidence_found`, response text comes from `approvedAnswer`.
4. For `known_unknown` or unsupported questions, the model is skipped.
5. Citations are resolved from retrieved `sourceIds`; provider-supplied links are ignored.
6. A requested animal ID must be an exact valid identifier; sanitizing cannot map another string to Sensen.
7. Social replies containing high-risk scientific markers, numbers, locations, status labels, or Latin binomials are replaced by the safe canonical social reply.

## 5. Response and Unity compatibility

`AIResponse` remains compatible with old responses and adds:

```text
answerMode
evidenceStatus
citations[]:
  sourceId
  title
  organization
  url
```

`source` and `routeReason` remain owned by the actual R1 route. Model output cannot set them. The existing `{role, content}` history schema is unchanged, so old history remains readable. The chat bubble displays at most two sanitized organization/title labels as a short `资料来源：...` line.

## 6. Insufficient evidence behavior

- Exact global population: cited known-unknown response; no invented number.
- Swimming: unsupported response with no citation.
- “资料里没有答案”: explicit unknown response.
- “编一个真实数量”: deterministic refusal.
- Tree-hole claim: corrected using the reviewed behavior fact.
- Off-domain math and prompt extraction: short redirect/refusal without fake citations.

## 7. Twenty-question comparison

Fixture: `content/quality/sensen-r1.5-questions.json`  
Regression: `server/tests/test_sensen_quality_regression.py`

| Metric | R1.5 baseline | R2 result |
| --- | ---: | ---: |
| Technical completion | 20/20 | 20/20 Local handler and 20/20 Cloud handler |
| Obvious factual fabrications | 4 | 0 |
| Unsupported/unverified factual answers | 5 | 0 |
| Required refusals handled correctly | 3/6 | 6/6 |
| Canonical citation coverage on sourced fact/known-unknown cases | 0/13 | 13/13 |
| Local/Cloud equality on evidence-constrained cases | not enforced | 13/13 |
| Middle-school suitability | 20/20 | 20/20 by wording review |

Closed R1.5 defects:

- fake scientific name `Rhinolophus helenae` -> `Semnopithecus priam`;
- invented China/Yunnan/Guangxi/Guizhou range -> India southeast and Sri Lanka;
- invented tree-hole habitat -> reviewed forest habitat and diurnal/semi-terrestrial behavior;
- invented population and added insect diet -> cited unknown global count and refusal;
- unsupported swimming answer -> explicit insufficient evidence;
- missing-data policy failure -> explicit uncertainty;
- quadratic-equation answer -> endangered-animal scope redirect.

The regression does not hardcode full questions into production code. It verifies the production retriever against aliases and topic terms stored in the canonical knowledge document.

## 8. Tests and review

Final automated results:

- Python: 54/54
- Unity EditMode: 136/136
- Unity compile/error scan: no C# compiler errors, unhandled exceptions, or `NullReferenceException`

Whole-branch local review compared `f0df272d..HEAD`. Two blocking boundary defects were found and fixed with failing tests first:

1. social markers previously preceded factual matching, so a greeting could bypass evidence;
2. Python and Unity differed on prompt-extraction/missing-evidence classification, and the Python animal lookup accepted IDs that became valid only after sanitizing.

No unresolved blocking finding remains in the local review. No external independent reviewer was invoked for R2 because no separate code-disclosure authorization was provided for this branch.

## 9. Security and repository checks

Required final checks:

- `git diff --check`
- tracked-file scan for `.env.local`, GGUF, API keys, and Authorization headers
- large tracked-file scan
- Unity compiler/exception scan
- `git status --short --branch`

Expected boundaries:

- Moonshot key remains only in ignored local environment configuration;
- GGUF remains under ignored `.local-models/` and is not tracked;
- knowledge text is untrusted prompt data and cannot call Unity methods;
- response `action`/`emotion` remain inert;
- no task, progress, badge, reward, AR package, or animation logic was changed by R2.

## 10. Known risks and acceptance

- Deterministic aliases are intentionally small and may return insufficient evidence for novel phrasing until reviewed aliases are added.
- Social chat remains model-generated when it contains no detected science claim. The conservative post-check may replace a harmless reply containing numbers or science terms with the canonical social fallback.
- Source reviews can become stale; `lastVerified` and `projectVerifiedDate` require future maintenance.
- This closes Sensen only. A second animal must receive its own reviewed document and must never reuse Sensen facts.
- The Python development server remains a local/demo boundary, not a production authentication or high-concurrency service.

R2 meets the requested Grounded Animal Knowledge acceptance conditions. No R3 work is included.
