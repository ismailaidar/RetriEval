# RetriEval Report

**Run at:** 2026-06-06 23:25:32Z  
**Retriever:** `InMemoryRetriever`  
**Grader:** `ChunkIdGrader`  
**k:** 3  
**Cases:** 5 evaluated, 0 errored

## Aggregate Metrics

| Metric | Score | Threshold | Status |
|--------|------:|----------:|--------|
| Hit@3 | 1.0000 | 0.6000 | ✅ pass |
| Precision@3 | 0.5333 | — | — |
| Recall@3 | 1.0000 | — | — |
| MRR | 1.0000 | 0.5000 | ✅ pass |
| MAP | 1.0000 | 0.5000 | ✅ pass |
| NDCG@3 | 1.0000 | 0.5000 | ✅ pass |
| F1@3 | 0.6600 | — | — |

## Per-Query Results

<details>
<summary><b>q-eiffel-location</b> — Hit@k=1.000 MRR=1.000 NDCG=1.000</summary>

**Query:** Where is the Eiffel Tower located?
**Category:** `geography`

**Retrieved chunks:**

| Rank | ID | Score | Relevant |
|-----:|:---|------:|:---------|
| 1 | `chunk-eiffel-1` | 4.0000 | ✅ |
| 2 | `chunk-eiffel-2` | 3.0000 | ❌ |
| 3 | `chunk-louvre-1` | 2.0000 | ❌ |

**Metrics:**

| Hit@3 | P@3 | R@3 | RR | AP | NDCG@3 | F1@3 |
|-------:|-----:|-----:|----:|----:|--------:|------:|
| 1.0000 | 0.3333 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 0.5000 |

</details>

<details>
<summary><b>q-eiffel-designer</b> — Hit@k=1.000 MRR=1.000 NDCG=1.000</summary>

**Query:** Who designed the Eiffel Tower?
**Category:** `geography`

**Retrieved chunks:**

| Rank | ID | Score | Relevant |
|-----:|:---|------:|:---------|
| 1 | `chunk-eiffel-2` | 3.0000 | ✅ |
| 2 | `chunk-eiffel-1` | 2.0000 | ❌ |
| 3 | `chunk-louvre-1` | 1.0000 | ❌ |

**Metrics:**

| Hit@3 | P@3 | R@3 | RR | AP | NDCG@3 | F1@3 |
|-------:|-----:|-----:|----:|----:|--------:|------:|
| 1.0000 | 0.3333 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 0.5000 |

</details>

<details>
<summary><b>q-louvre</b> — Hit@k=1.000 MRR=1.000 NDCG=1.000</summary>

**Query:** What is in the Louvre museum Paris?
**Category:** `culture`

**Retrieved chunks:**

| Rank | ID | Score | Relevant |
|-----:|:---|------:|:---------|
| 1 | `chunk-louvre-1` | 5.0000 | ✅ |
| 2 | `chunk-louvre-2` | 4.0000 | ✅ |
| 3 | `chunk-eiffel-1` | 3.0000 | ❌ |

**Metrics:**

| Hit@3 | P@3 | R@3 | RR | AP | NDCG@3 | F1@3 |
|-------:|-----:|-----:|----:|----:|--------:|------:|
| 1.0000 | 0.6667 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 0.8000 |

</details>

<details>
<summary><b>q-berlin-wall</b> — Hit@k=1.000 MRR=1.000 NDCG=1.000</summary>

**Query:** When was the Berlin Wall built?
**Category:** `history`

**Retrieved chunks:**

| Rank | ID | Score | Relevant |
|-----:|:---|------:|:---------|
| 1 | `chunk-berlin-2` | 3.0000 | ✅ |
| 2 | `chunk-eiffel-1` | 2.0000 | ❌ |
| 3 | `chunk-berlin-1` | 2.0000 | ❌ |

**Metrics:**

| Hit@3 | P@3 | R@3 | RR | AP | NDCG@3 | F1@3 |
|-------:|-----:|-----:|----:|----:|--------:|------:|
| 1.0000 | 0.3333 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 0.5000 |

</details>

<details>
<summary><b>q-dotnet</b> — Hit@k=1.000 MRR=1.000 NDCG=1.000</summary>

**Query:** What languages does .NET support?
**Category:** `technology`

**Retrieved chunks:**

| Rank | ID | Score | Relevant |
|-----:|:---|------:|:---------|
| 1 | `chunk-dotnet-1` | 1.0000 | ✅ |

**Metrics:**

| Hit@3 | P@3 | R@3 | RR | AP | NDCG@3 | F1@3 |
|-------:|-----:|-----:|----:|----:|--------:|------:|
| 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |

</details>

