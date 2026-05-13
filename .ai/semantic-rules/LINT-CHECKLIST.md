# GitVersion Semantic Lint Checklist

**Version:** 0.1  
**Status:** Ratified — bridge artifact between semantic rule catalogue and validator implementation  
**Date:** 2026-05-12  
**Relationship:** One-to-one with SEM-RULES-CATALOGUE.md. The validator executes this checklist mechanically.

---

## Purpose

This checklist can be applied manually to any `GitVersion.yml` today.  
It will be executed automatically by `ConfigurationSemanticValidator` when implemented.  
Each check corresponds to exactly one semantic rule. Failure tells you *why* the config is wrong, not just *that* it is wrong.

---

## Checklist

### SEM-001 — Authority/Carrier Exclusivity

> *A branch may not both author and calculate its version.*

- [ ] Does any branch have `is-release-branch: true` AND `increment` not equal to `None`?
- [ ] Does any branch have `is-main-branch: true` AND `is-release-branch: true`?
- [ ] Does any branch have `is-release-branch: true` AND a `regex` that contains no version pattern (`\d+\.\d+`)?

If any box above is checked → **❌ SEM-001 VIOLATION (Error)**

Remediation: An authority branch must set `increment: None`. Its version comes from its name or from tags — not from incrementing a parent.

---

### SEM-002 — Root Lineage Grounding

> *Primary branches must be absolute roots in the version graph.*

- [ ] Does any branch have `is-main-branch: true` AND `source-branches` absent or non-empty?

If checked → **❌ SEM-002 VIOLATION (Error)**

Remediation: Add `source-branches: []` to every `is-main-branch: true` branch. This declares the branch as a version graph root with no inheritable ancestors.

---

### SEM-003 — Variable Capture Contract

> *Label variables must have a corresponding capture source in the regex.*

- [ ] Does any branch have `label` containing `{BranchName}` or any `{Placeholder}`?
- [ ] If yes: does the branch `regex` contain a named capture group `(?<BranchName>.+)` (or matching placeholder name)?
- [ ] If the capture group is absent → **❌ SEM-003 VIOLATION (Error)**

Remediation: Either add `(?<BranchName>.+)` to the regex, or change the label to a static string.

---

### SEM-004 — Deployment Mode Consistency

> *Deployment strategy and identity signaling must not conflict.*

`ContinuousDeployment` collapses prerelease identity. Labels assert identity.  
Using both without explicit scoping loses information silently.

- [ ] Is `mode: ContinuousDeployment` set at the root level?
- [ ] If yes: does every branch with a non-empty `label` explicitly declare its own `mode`?
- [ ] If any labelled branch lacks an explicit `mode` override → **ℹ SEM-004 ADVISORY (per DEC-016 ARC-001 — labelled CD is valid)**

Remediation (optional): No action is required. To model prerelease versions distinct from deployable releases, configure `mode: ContinuousDelivery` (or `ManualDeployment`) on the labelled branch.

Note: This rule surfaces the Stability Paradox — the user is simultaneously asking for "every commit is a release" AND "meaningful prerelease labels." The fix forces them to choose where information is allowed to disappear.

---

### SEM-005 — Source Branch Reference Integrity

> *You cannot traverse to a branch that doesn't exist.*

- [ ] Does any branch's `source-branches` list contain a key that is not defined in the `branches` map?

If checked → **❌ SEM-005 VIOLATION (Error)**

Remediation: Either define the missing branch, or remove it from `source-branches`.

---

### SEM-006 — Strategies Must Be Declared

> *Implicit strategy composition is fragile across GitVersion version upgrades.*

- [ ] Is the `strategies` block absent from the configuration?

If checked → **ℹ SEM-006 ADVISORY (per DEC-016 ARC-001 — implicit defaults are valid)**

Remediation (optional): No action is required. To lock strategy behaviour across GitVersion upgrades, declare the strategies list explicitly — for GitFlow: `[Fallback, ConfiguredNextVersion, MergeMessage, TaggedCommit, TrackReleaseBranches, VersionInBranchName]`; for TrunkBased: `[ConfiguredNextVersion, Mainline]`.

---

### SEM-007 — Increment Strategy Totality

> *You cannot inherit a version without a parent context.*

- [ ] Does any branch have `increment: Inherit` AND `source-branches` absent or empty?

If checked → **ℹ SEM-007 ADVISORY (per DEC-016 ARC-001 — engine-default inheritance is valid)**

Remediation (optional): No action is required. To make the inheritance chain explicit and stable across GitVersion upgrades, define at least one `source-branches` entry for branches using `increment: Inherit`.

---

## Example Application

The following illustrates applying the checklist to real GitFlow configurations
that were producing unexpected pre-release version output (e.g. `1.62.0-alpha.261`
instead of `1.62.0` on the primary branch merge commit).

### Configuration A (GitFlow, without capture group on hotfix)

| Check | Result |
|---|---|
| SEM-001: master has `is-release-branch: true`, implicit increment | Error |
| SEM-002: master missing `source-branches: []` | Error |
| SEM-003: n/a (no `{BranchName}` label issues) | Pass |
| SEM-004: root `ContinuousDeployment`, no branch mode overrides | Advisory |
| SEM-005: no source-branches declared anywhere — implicit only | Warning |
| SEM-006: no `strategies` block | Advisory |
| SEM-007: feature/bugfix use `increment: Inherit`, no `source-branches` | Advisory |

**Result: 2 Errors, 1 Warning, up to 3 Advisories**

### Configuration B (same template, hotfix without capture group)

| Check | Result |
|---|---|
| SEM-001: master has `is-release-branch: true`, implicit increment | Error |
| SEM-002: master missing `source-branches: []` | Error |
| SEM-003: hotfix `label: beta` (static — no substitution); if changed to `{BranchName}`, regex `^hotfix/` has no capture group | Latent Error |
| SEM-004: same as Configuration A | Advisory |
| SEM-005: same as Configuration A | Warning |
| SEM-006-007: same as Configuration A | Advisories |

**Result: 2 Errors, 1 Latent Error, 1 Warning, up to 3 Advisories**

---

## Bridge to Implementation

The validator's job is to execute this checklist as a pure function:

```
f(IGitVersionConfiguration) → IReadOnlyList<SemanticViolation>
```

Where `SemanticViolation` carries:
- `RuleId` (e.g. `SEM-001`)
- `Severity` (Error | Warning | Advisory)
- `BranchName` (which branch triggered it)
- `Message` (human-readable statement of the violated invariant)
- `Remediation` (what to change)

The validator does not need GitVersion's calculation engine — it reasons purely over the configuration fields enumerated in this checklist. That is what makes it testable at configuration-load time and workflow-agnostic.
