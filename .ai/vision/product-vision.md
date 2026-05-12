# Product Vision — GitVersion Semantic Contract Framework

**Status:** Active  
**Date:** 2026-05-12  
**Session context:** ADR-001 GitFlow exemplar complete (20/20 tests); TrunkBased exemplar complete (15/15 tests); PRIMS estate gap analysis complete (10 Phase0 observations)

---

## The Product We Are Building

> A reusable specification mechanism for GitVersion semantics, where GitFlow is only an exemplar.

More precisely: a **semantic contract** — a set of workflow-agnostic invariants that any GitVersion configuration must satisfy — backed by proven exemplar implementations and a validator that enforces the contract against any real-world config.

---

## What This Is Not

- Not a GitFlow validator
- Not an ADR-001 compliance checker
- Not a tool specific to the PRIMS estate

ADR-001 is **Exemplar #1**. The PRIMS estate gap analysis is the **negative corpus**. The validator is a **derived artifact** that executes the contract. None of these are the product itself.

---

## Correct Layering (Non-Negotiable)

```
Semantic Model
  (workflow-agnostic invariants — branch authority, increment, ordering, override rules)
      │
      ├── Exemplar #1: GitFlow (ADR-001)      ← proves the model with synthetic git histories
      ├── Exemplar #2: TrunkBased (preview1)  ← validates abstraction seam
      └── Exemplar #N: future workflows
          │
          ├── Negative corpus (PRIMS estate configs)  ← real configs that violate the model
          │
          └── Validator implementation
                (enforces the semantic model against any IGitVersionConfiguration)
                    │
                    └── PRIMS estate compliance  ← downstream outcome, not the goal
```

ADR-001 must NOT sit at the root of validation logic. Rules must not encode GitFlow structures.

---

## What the Semantic Model Consists Of

Three orthogonal dimensions:

1. **Branch Authority** — which branches are version authorities (derive version from branch name or tag) vs. label carriers (derive version from parent lineage + label)
2. **Increment Mechanics** — how each branch type advances the version (None / Patch / Minor / Major / Inherit) and what overrides exist (commit messages, branch names, tags)
3. **Ordering Semantics** — the deployment mode per branch (ManualDeployment / ContinuousDelivery / ContinuousDeployment) and its interaction with prerelease label format

A fourth cross-cutting concern:
4. **Topology Coherence** — whether the declared source-branch graph is consistent with the intended authority/carrier roles

---

## Seam Between Exemplars (Validated to Date)

| Dimension | GitFlow | TrunkBased | Generalises? |
|---|---|---|---|
| Primary branch concept | main/master | main/master | **YES** |
| {BranchName} label on work branches | yes | yes | **YES** |
| Tags = exact clean version | yes | yes | **YES** |
| +semver: force-bump | yes | yes | **YES** |
| Strategy | TaggedCommit+Track | Mainline | NO |
| Primary branch mode | ManualDeployment | ContinuousDeployment | NO |
| Prerelease format | label.1+N | label.N | NO |
| Work branch topology | develop+release+feature+hotfix | feature+hotfix | NO |
| Feature increment | Inherit | Minor | NO |

The four YES rows define the semantic model core. Everything in the NO column is workflow-specific configuration.

---

## Promotion Criteria to Exemplar #3

A third exemplar should be built before generalising the validator beyond the two current cases. Candidate: release-train / preview-stream workflow. Promotion is triggered when a real use case requires it.

---

## Upstream Intent

The semantic contract and validator are intended as upstream contributions to the GitVersion project, not as PRIMS-specific tooling. They address a gap in GitVersion's own offering: the JSON schema validates structure, but no tooling validates semantic coherence. Every GitVersion user who has ever had a config silently misbehave is the intended beneficiary.
