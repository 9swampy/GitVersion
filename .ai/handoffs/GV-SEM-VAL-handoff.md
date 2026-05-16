# GitVersion Semantic Validation Framework
## Handoff — Semantic Contract Capture & Reference Implementation
## Work Item: GV-SEM-VAL

---

## 1. Purpose

Continues work on **GV-SEM-VAL**: defining and enforcing a workflow-agnostic semantic contract for GitVersion configurations to eliminate non-deterministic versioning behavior.

This handoff preserves:
- The shift from GitFlow-specific linting to generalized semantic invariant enforcement
- The "Authority vs. Carrier" model as the primary lens for configuration correctness
- The TDD "Gold Standard" fixture as the authoritative source of truth

---

## 2. Scope Guardrails

**In scope:**
- Semantic Rule Catalogue: formally defined rules like SEM-001 (Exclusivity)
- Integration Test Fixture: C# TDD scaffold using `GitFlowConfigurationBuilder`
- Q&A Synthesis Protocol: human-to-YAML mapping process

**Out of scope by default:**
- Modifying GitVersion Core Source Code (all work via configuration or external fixtures)
- Automated Fixers (validation/detection only, not remediation)
- Support for Legacy GitVersion (<5.0)

---

## 3. Locked Conclusions

1. **The Stability Paradox is an Invariant**  
   GitVersion cannot natively produce `1.0.0+N` (stable + metadata) without incrementing. We accept `1.0.1-1+N` via `ManualDeployment` as the "Gold Standard" proxy.

2. **Authority vs. Carrier Duality is Illegal**  
   A branch cannot simultaneously derive its version from its name (`is-release-branch`) and calculate an increment. SEM-001 enforces `increment: None` for version authorities.

3. **Scaffolding vs. Artifact Separation**  
   C# Builders are for discovery and TDD. YAML is the final deliverable. The test code is the "Why"; the YAML is the "What".

4. **Semantic rules must be workflow-agnostic**  
   GitFlow, trunk-based, or release-train strategies are policy choices. Semantic correctness must hold across all of them.

5. **Validation occurs at configuration-load time, not during version computation**  
   All target failure modes are observable statically from configuration fields.

6. **Broken configs are assets, not liabilities**  
   PRIMS estate configs form a permanent negative test corpus. Never "clean them up" without preserving their violation signatures.

---

## 4. Milestone: GV-SEM-VAL Step 2 Complete (2026-05-12)

**Architecture validated. Loop closed.**

What is now structurally settled:
1. Synthesis is a verified transformation, not a heuristic
2. Validator is the single semantic oracle
3. YAML is no longer a semantic risk surface
4. Legacy failure modes are demonstrably preventable
5. Intent → Configuration is mechanically reproducible

The PrimsSynthesisStep2Tests trace proves this end-to-end:
- Every boundary is exercised, no stage assumes prior success
- Negative corpus (broken prims config) and positive corpus (synthesised correct config) coexist in the same test suite
- The validator is the final authority, not a post-hoc check

Further work is **extension, not foundation**.

---

## 4a. System State (current)

### Validator (complete)
✅ GitFlow canonical fixture (ADR-001) — 20/20 tests  
✅ TrunkBased canonical fixture (Exemplar #2) — 15/15 tests  
✅ YAML deliverable validated via ConfigurationSerializer — both exemplars  
✅ Semantic Rule Catalogue SEM-001–SEM-007 ratified; SEM-010 formally deferred  
✅ Lint Checklist and Failure UX Contracts (F-001–F-004)  
✅ `ConfigurationSemanticValidator` — pure function, no calculation engine  
✅ Validator tests — 20/20 positive + PRIMS negative corpus + rule unit tests  
✅ `gitversion /validate` CLI surface wired and functional  
✅ `PublicAPI.Shipped.txt` updated  
✅ PR #2 open at https://github.com/9swampy/GitVersion/pull/2  

### Detection-Only Synthesis: Step 1 complete (Appendix B §B.8 Step 1)
✅ `TopologyClassifier` — Layer 1 branch patterns → `CommonTopologies` classification  
✅ `VersionExampleParser` — Layer 2 (pattern, example) → label/mode/role inference  
✅ `AmbiguityDetector` — emits F-001–F-004 structured diagnostics  
✅ `DetectionOnlySynthesis` coordinator — full chain, no YAML emission  
✅ `CommonTopologies` — named contract pairing TopologyKind with exemplar workflow  
✅ 66/66 synthesis tests — including prims dogfood (bare YAML keys + user examples)  
✅ Engineering standards applied: prose names, CommonTopologies contract, no what-comments  

Key discoveries during Step 1:
- YAML branch keys ("release") and user patterns ("release/1.62.0") are different inputs — both handled
- Non-standard names ("work/*") return Unknown → F-001 fires → correct: fail loudly, never silently
- `CommonTopologies` as a paired record collapses two assertions to one production contract  

### Step 2 pre-conditions (not yet started)
- `IsSuccessful = true` from detection is required before YAML emission
- A `SemanticMapping` stage must translate inferences to config fields
- Emitted YAML must pass `ConfigurationSemanticValidator`

### Remaining items
- Upstream PR to `GitTools/GitVersion` — pending  
- Exemplar #3 — deferred (no concrete demand)
- Q&A Synthesis Protocol Step 2 (YAML emission) — pre-conditions above  

---

## 5. Problem Statement

GitVersion lacks a formal, reusable mechanism to detect semantic contradictions in configuration intent before runtime, resulting in unstable version outputs and systemic estate-wide errors.

Success means:
- Statically detect semantic conflicts (authority vs carrier, inheritance voids, deployment inconsistency)
- Explain violations in terms of broken invariants, not YAML mechanics
- Support multiple workflows without code changes

---

## 6. Architectural Status

- **Direction #1: External Validator Utility** — ✅ DELIVERED (`ConfigurationSemanticValidator`, `/validate` CLI)
- **Direction #2: Q&A Informed Synthesis** — ✅ Step 1 (Detection-Only) DELIVERED; Step 2 (YAML emission) pending

---

## 7. Explicit Non-Goals

- Rewriting the GitVersion Engine (work with its quirks, not against them)
- Workflow prescription (GitFlow is an exemplar, not a standard)
- Automatic YAML remediation
- UI development

---

## 8. Remaining Work (extension, not foundation)

1. ~~CLI surface~~ ✅ DONE  
2. ~~`PublicAPI.Shipped.txt`~~ ✅ DONE  
3. ~~Synthesis Step 2~~ ✅ DONE  
4. Upstream PR: target `GitTools/GitVersion` from `9swampy/GitVersion` PR #2  
5. SEM-010 grammar ratification (deferred — trigger: real corpus violation)  
6. Non-GitFlow exemplar expansion (deferred — trigger: concrete demand)  
7. Interactive UX / CLI synthesis surface (product choice, not technical necessity)  
8. Exemplar #3 (optional — before generalising Q&A synthesis utility)

---

## 9. Critical Insights

### The "Identity Crisis" Signal (SEM-001)
`is-release-branch: true` + `increment != None` = the branch doesn't know if it is an Authority (taking the name) or a Calculator (taking the parent + increment). This is the primary source of PRIMS version jumping.

### The -1 Prerelease Counter
`1.0.1-1+N` is the engine's attempt to satisfy SemVer's "less than" requirement while having no explicit label. It is the only way to achieve "Stable + Metadata" without breaking engine changes. Accept it; document it.

### Physics vs Policy (The Machine Lens)
Semantic rules are physics — invariants the version graph must satisfy to remain stable. Workflows are policy — choices within the physics. This framing makes the solution resilient to future workflow changes.

### Validator Value = Explanation, Not Enforcement
Success metric: clarity and trust, not error count. A violation message that explains WHY a config is broken (in terms of the invariant) is more valuable than one that says what field is wrong.

---

## Key File Locations

| Artifact | Path |
|---|---|
| Product Vision | `.ai/vision/product-vision.md` |
| Decisions Log | `.ai/vision/decisions-log.md` |
| Semantic Rule Catalogue | `.ai/semantic-rules/SEM-RULES-CATALOGUE.md` |
| Lint Checklist | `.ai/semantic-rules/LINT-CHECKLIST.md` |
| GitFlow exemplar fixture | `src/GitVersion.Core.Tests/IntegrationTests/CanonicalGitFlowScenarios.cs` |
| GitFlow YAML validation | `src/GitVersion.Core.Tests/IntegrationTests/CanonicalGitFlowScenariosFromYaml.cs` |
| TrunkBased exemplar fixture | `src/GitVersion.Core.Tests/IntegrationTests/CanonicalTrunkBasedScenarios.cs` |
| TrunkBased YAML validation | `src/GitVersion.Core.Tests/IntegrationTests/CanonicalTrunkBasedScenariosFromYaml.cs` |
| PRIMS estate gap analysis | `.ai/observations/prims-estate-gitflow-gap-analysis-2026-05-12.jsonl` |
| ADR-001 COLE document | `.ai/cole/COLE-ROOT-CAPABILITY-ADR001.yaml` |
| Session observations | `.ai/observations/gitflow-config-adr001-2026-05-12.jsonl` |

---

**End of GV-SEM-VAL Handoff v1.**
