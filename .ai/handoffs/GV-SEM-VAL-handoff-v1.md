# GitVersion Semantic Validation Framework
## Handoff v1 — Semantic Contract Capture & Reference Implementation
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

## 4. Current System State

✅ Verified Capability: Functional "Gold Standard" GitFlow C# fixture — 20/20 integration tests passing  
✅ Verified Capability: TrunkBased (Exemplar #2) fixture — 15/15 integration tests passing  
✅ Verified Capability: Draft Semantic Rule Catalogue SEM-001 through SEM-007  
✅ Verified Capability: Lint Checklist derived from catalogue  
✅ Verified Capability: PRIMS estate gap analysis (10 Phase0 observations)  
✅ Verified Capability: YAML deliverable validated via ConfigurationSerializer (both exemplars)  
❌ Known Limitation: Existing PRIMS configs violate SEM-001, SEM-002, SEM-004, SEM-006, SEM-007  
❌ Known Limitation: No automated semantic validator exists yet  

---

## 5. Problem Statement

GitVersion lacks a formal, reusable mechanism to detect semantic contradictions in configuration intent before runtime, resulting in unstable version outputs and systemic estate-wide errors.

Success means:
- Statically detect semantic conflicts (authority vs carrier, inheritance voids, deployment inconsistency)
- Explain violations in terms of broken invariants, not YAML mechanics
- Support multiple workflows without code changes

---

## 6. Candidate Architectural Directions

- **Direction #1: External Validator Utility** — standalone tool/test suite that deserializes YAML and asserts the SEM-RULE catalogue
- **Direction #2: Q&A Informed Synthesis** — "Architect's Interview" that generates Correct-by-Construction YAML

---

## 7. Explicit Non-Goals

- Rewriting the GitVersion Engine (work with its quirks, not against them)
- Workflow prescription (GitFlow is an exemplar, not a standard)
- Automatic YAML remediation
- UI development

---

## 8. Intended Next Steps

1. Freeze SEM-001 through SEM-007 definitions ← IN PROGRESS
2. Confirm Lint Checklist as bridge artifact ← DONE
3. Agree validator output schema before implementation ← DONE (SemanticViolation record)
4. Implement `ConfigurationSemanticValidator` — pure function over `IGitVersionConfiguration`
5. Write validator tests using PRIMS estate configs as negative corpus
6. CLI surface: `gitversion /validate` or `--validate-config`

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
