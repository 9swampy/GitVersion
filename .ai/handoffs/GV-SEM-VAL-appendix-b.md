# Appendix B — Grammar-Driven Q&A Synthesis Protocol

**Status:** Draft / Planning Artifact  
**Date:** 2026-05-12  
**Non-binding.** Records intent and design constraints only. No implementation implied.

---

## B.1 Purpose

Defines a guided, example-first mechanism for deriving a semantically valid `GitVersion.yml`
from sparse, human-oriented inputs, delegating correctness enforcement to the existing
semantic validator.

> From *"fill out abstract YAML knobs"*  
> to *"state naming and output intent, then derive mechanics"*

---

## B.2 Design Principles (Non-Negotiable)

1. **Intent precedes mechanics** — users express what versions should look like, not how computed
2. **All synthesis output is provisional** — generated YAML must pass `ConfigurationSemanticValidator`
3. **Inference is allowed; guessing is not** — under-determined intent → stop, explain, require explicit choice
4. **Exemplars are dictionaries, not laws** — GitFlow/TrunkBased provide defaults and vocabulary only
5. **Static only** — no Git history, no repository inspection, no runtime data

---

## B.3 Inputs (Three Layers)

### Layer 1 — Nomenclature (Required)

Minimal branch name/pattern declarations. Example:

```
master
develop
feature/Branch
bugfix/Branch
release/1.2.3
hotfix/Branch
```

Enables topology inference and exemplar dictionary selection.

### Layer 2 — Output Examples (Optional but Powerful)

Example version strings per branch class:

```
master         → 1.62.0
develop        → 1.62.0-alpha{N}
feature/Branch → 1.62.0-Branch{N}
release/1.62.0 → 1.62.0-beta{N}
hotfix/Branch  → 1.62.0-Branch{N}
```

Placeholders (`{N}`, `{BranchName}`) may be symbolic. Parsing is grammar-based, not regex-based.
Overrides exemplar defaults; derives labels, increment sources, deployment modes, authority/carrier roles.

### Layer 3 — Explicit Overrides (Escape Hatch)

Structured overrides for cases that cannot be inferred:
- Increment source is commits, not merges
- Version authority is tags only
- Pre-release weight constraints

---

## B.4 Grammar Foundation

Synthesis uses the same grammar sources as validation:

- **Variables:** `GitVersionVariables.AvailableVariables` (28 variables — fully enumerable)
- **Format specifiers:** Standard C# `IFormattable` rules via pluggable formatter chain
  (StringFormatter, FormattableFormatter, NumericFormatter, DateFormatter)
- **Conditional syntax:** `LegacyCompositeFormatter` semicolon rules (`;` conditional)

All placeholder usage discovered during synthesis must be validated by SEM-010 (Grammar Integrity)
once ratified.

**The grammar IS extractable from the codebase** — no hand-coding required.

---

## B.5 Derivation Stages

### Stage 1 — Topology Classification
From Layer 1: infer model (GitFlow-like, Trunk-like, Hybrid), select exemplar dictionary,
identify candidate version authorities. No YAML emitted.

### Stage 2 — Example Alignment
From Layer 2: parse version strings against known grammar, identify labels vs core version
segments, detect conflicts (multiple authorities, SEM-001 duality). Conflicts block synthesis.

### Stage 3 — Semantic Mapping

| Intent Signal | Derived Field |
|---|---|
| Static base version | `is-mainline`, `is-release-branch`, tag authority |
| Suffix presence | `label` |
| Suffix monotonicity (dot vs concat vs metadata) | `mode` |
| Patch drift | `increment` |
| Pre-release sequencing | `pre-release-weight` |

No heuristics may violate existing SEM rules.

### Stage 4 — YAML Emission
Emit minimal YAML (only required fields; defaults implicit).
Hand off immediately to `ConfigurationSemanticValidator`.
Synthesis succeeds **only if validation passes**.

---

## B.6 Failure Contracts (F-001 through F-004)

See `FAILURE-UX-CONTRACTS.md` in this directory.

---

## B.7 Scope Constraints (Explicit Non-Goals)

- ❌ Not a general GitVersion authoring layer
- ❌ Does not infer organizational policy
- ❌ Does not optimize or minimize YAML
- ❌ Does not interpret Jira, commit messages, or tags
- ❌ Does not auto-fix PRIMS repositories
- ✅ Bridges intent → validator-accepted config only

---

## B.8 Clarified Next Steps (Sequenced)

### Step 1 — Detection-Only Synthesis (No Emission)
Prove Layer 1 + Layer 2 inputs can be classified and explained without generating YAML.
Success signal: system can say "I know / I don't know / I need this clarified" with reasons.

### Step 2 — Lock Failure UX Text Contracts
Required before any synthesis prototype ships. (Done — see FAILURE-UX-CONTRACTS.md)

### Step 3 — One Worked Trace (Single Exemplar Only)
Exactly one end-to-end trace: inputs → derived semantics → proposed YAML → /validate pass.
Constraints: GitFlow exemplar only, no conditional formatting, no custom variables.

---

## B.9 Additional Risks

### Risk 1 — False Confidence via Partial Examples
A single example can appear sufficient but silently mask ambiguity.
Mitigation: require at least two signals before inferring increment source; otherwise F-002.

### Risk 2 — Grammar Validation Lag
SEM-010 not yet ratified. Synthesis grammar handling must be best-effort + validator-gated.

### Risk 3 — Exemplar Gravity
UX text must consistently say "based on exemplar defaults" — never "recommended" or "standard."

---

## B.9a Step 2 Tightened Invariants (post Step 1 review)

### The Primary Invariant (non-negotiable)

> **Synthesis is not allowed to create intent; it may only encode intent that already exists and has been proven sufficient.**

If synthesis emits a field the user did not express, synthesis has a bug.

### Mapping Must Be Injective

Each `VersionExampleInference` must map to exactly one branch rule.
No inference may partially populate multiple rules, split responsibility, or defer to YAML defaults.
If this constraint is violated, synthesis aborts with an **internal failure** — not user error.
This preserves the validator-as-oracle separation.

### Emitter Must Be Capable of Empty Sections

Canonical YAML constants are structural scaffolding only. They must not carry semantics the mapper is unaware of.
The emitter must be capable of emitting an empty branch section if the mapper says so — even if the canonical template usually contains it.
Otherwise exemplars silently become policy.

### Only Explicitly Mapped Fields May Differ from Engine Defaults

Fields the mapper did not explicitly populate (e.g. `increment: Patch`, `prevent-increment-of-merged-branch-version: true`) must be omitted from synthesis output, not inherited from the canonical template.
Risk: semantic attribution to defaults, not user intent.

### Single-Example Sequencing Must Still Confirm Authority

A single example that embeds both authority and label (e.g. `release/1.62.0 → 1.62.0-beta1244`) appears complete but still hides sequencing source.
Rule: a single example that embeds both authority and label **must still confirm sequencing source explicitly**.
F-001 fires even if the result looks complete, until sequencing is confirmed.
This preserves intent integrity over convenience.

### Grammar Recognition Is Tentative Until SEM-010 Ratified

Synthesis may *recognise* grammar patterns, but only SEM-validated grammar may be *relied upon*.
Mapper must treat grammar recognition as tentative; validator remains authoritative.
Forward-compatibility preservation.

---

## B.10 Exit Criteria for Implementation Phase

This appendix graduates from draft when:
1. SEM-010 (Grammar Integrity) is ratified or explicitly deferred
2. Increment-authority ambiguity handling is UX-agreed
3. A single exemplar-backed synthesis proof is demonstrated end-to-end
