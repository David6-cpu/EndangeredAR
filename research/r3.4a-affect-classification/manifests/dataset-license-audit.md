# Dataset and License Audit

Audit date: 2026-08-30

This is an engineering rights screen, not legal advice. A repository license is
not assumed to license third-party dialogue embedded in a dataset, and a public
download is not treated as commercial or redistribution permission.

| Field | CPED | Ren-CECps | NLPCC 2013/2014 emotion corpora |
| --- | --- | --- | --- |
| Original publisher | South China University of Technology researchers | Changqin Quan and Fuji Ren, University of Tokushima | NLPCC shared-task organizers |
| Original paper | [CPED, arXiv 2205.14727](https://arxiv.org/abs/2205.14727) | [EMNLP 2009, ACL Anthology D09-1150](https://aclanthology.org/D09-1150/) | NLPCC task pages and later [NLPCC 2017 overview](https://coai.cs.tsinghua.edu.cn/hml/media/files/Overview_of_the_NLPCC_2017_Shared_Task__Emotion_Generation_Challenge.pdf) |
| Official source | [scutcyr/CPED](https://github.com/scutcyr/CPED) and linked LUGE page | Paper-linked institutional download; access terms require an author license agreement | Historical organizer/task downloads; no current commercial grant located |
| Language | Chinese | Chinese | Chinese |
| Scale | 133,000+ utterances, 12,000+ dialogues, 40 TV shows | 1,500 blog posts, about 11,000 paragraphs and 35,000 sentences | About 14,000 Weibo posts in 2013 and about 20,000 in 2014, according to task descriptions and downstream papers |
| Text type | Scripted television dialogue | Blog posts | Sina Weibo posts |
| Average length | About 8.3 Chinese characters per utterance | Long-form document, paragraph, and sentence annotations; no project-comparable short-reply average published | Short social posts; project-comparable average not established |
| Multi-turn | Yes | No dialogue turns | No assistant dialogue-act pairing |
| Emotion labels | 13 fine-grained emotions plus sentiment | Eight emotion dimensions with intensity | Seven emotions plus none/neutral variants depending on task year |
| Dialogue-act labels | 19 | None | None |
| Published split | TV-show-grouped: 26 train, 5 validation, 9 test | No project-ready grouped split | Competition train/test split; exact usable artifacts vary by year |
| Stated license | Repository `LICENSE` is Apache-2.0 | Free with a separate license agreement; no agreement obtained | No clear commercial/redistribution license found in reviewed official material |
| Research boundary | Local research only for this spike | Do not download or use without an executed license | Do not download or use without written rights clarification |
| Competition boundary | Not a competition dataset | Not applicable | Historical shared-task use does not establish product rights |
| Commercial boundary | Unclear for television dialogue and derived weights | Unclear and agreement-dependent | Unclear |
| Redistribution | Repository code license is explicit; dialogue redistribution is not treated as cleared | Not cleared | Not cleared |
| Copyright source | Forty Chinese television shows | Blog content | Social-media content |
| Personal information risk | Dataset includes character/person attributes; those columns are excluded from the experiment | Blogs can include personal content | Social posts can include handles and personal content |
| EndangeredAR fit | Best available local research candidate because it has short Chinese multi-turn text, emotion, and dialogue act | Poor domain and task fit; emotion only | Poor task fit; emotion only and higher privacy/domain risk |
| Decision | **Adopt for local-only, non-distributed research.** Exclude speaker/personality metadata. Keep raw/processed data and derived ONNX outside Git. | **Reject for R3.4A.** | **Reject for R3.4A.** |

## CPED local manifest

- Upstream commit: `1e4b81c28a123f22387e06664f37e5dc9322380f`
- Train: 94,187 utterances in 8,086 dialogues; measured mean text length 8.40.
- Validation: 11,137 utterances in 934 dialogues; measured mean 8.32.
- Test: 27,438 utterances in 2,815 dialogues; measured mean 8.47.
- Only `TV_ID`, `Dialogue_ID`, `Utterance_ID`, `Emotion`, `DA`, and
  `Utterance` are read.
- Names, gender, age, personality, speaker IDs, audio, and video are excluded.
- Existing TV-level splits are preserved. A normalized-text fingerprint filter
  removes exact/template-normalized overlap before training. Gold data is kept
  separate and is never used for vocabulary, threshold, temperature, or model
  selection.

## Rights conclusion

CPED is sufficient to answer the model feasibility question locally, but it is
not sufficient to approve a public model artifact. R3.4A may publicly commit
training code, mappings, aggregate metrics, configuration, and project-authored
gold vectors. It must not commit CPED text, processed rows, checkpoints, or ONNX
weights. A public ONNX requires a later legal review or retraining on data with
explicit derivative-model and commercial rights.
