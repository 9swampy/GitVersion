# Semantic Rule Catalogue — GitVersion Configuration

**Version:** 0.1 (draft — derived from ADR-001 and PRIMS estate gap analysis)  
**Status:** Pre-implementation — rules must be stable before validator is written  
**Date:** 2026-05-12

---

## Purpose

This catalogue defines workflow-agnostic semantic invariants for GitVersion configuration. Rules are stated in terms of GitVersion's semantic model, not in terms of any specific workflow (GitFlow, TrunkBased, etc.).

- Exemplars (ADR-001, TrunkBased) **instantiate** these rules
- PRIMS estate configs **violate** them
- The validator **enforces** them
- Rules do not name workflows

---

## Rule Format

```
SEM-RULE-NNN:
  Name:       Short invariant name
  Statement:  The invariant in one sentence
  Rationale:  Why the invariant exists (the mechanism, not the symptom)
  Violations: Concrete config expressions that trip this rule
  Evidence:   Which exemplar(s) and/or negative corpus cases expose this rule
  Severity:   Error | Warning
```

---

## Rules

### SEM-RULE-001: Authority/Carrier Exclusivity

```
Name:      Authority/Carrier Exclusivity
Definitions:
  Version Authority: A branch whose NextVersion is derived from an external
    artifact — a Tag or the Branch Name itself (is-release-branch: true with
    increment: None, or is-main-branch: true). The branch does not calculate
    an increment; it declares or discovers its version.
  Label Carrier: A branch whose NextVersion is derived from a parent branch.
    The branch contributes only a metadata string (the label) to the version
    it inherits.
Statement: A branch must be one or the other, not both. A branch that declares
           itself a version authority (is-release-branch: true) must not also
           specify an active increment strategy (anything other than None or
           Inherit-from-authority). If it does, GitVersion enters an ambiguous
           state where both the authority and the increment compete to determine
           the version, producing "version jumping" or null-reference bugs.
Rationale: The increment field is the discriminator. An authority branch derives
           its version externally — specifying an increment is contradictory.
           is-release-branch: true WITH increment: Minor means "derive from name
           AND add a minor bump" — GitVersion must choose one, and the choice is
           non-deterministic across configurations.
Violations:
  - is-release-branch: true AND increment is not None (i.e. Patch, Minor, Major,
    or Inherit without a clear authority parent)
  - is-main-branch: true AND is-release-branch: true on the same branch
  - is-release-branch: true on a branch whose regex contains no version pattern
    (\d+\.\d+) — the authority claim is unfulfillable
Evidence:
  - PRIMS foundation/strata/git-check: master has is-release-branch: true
    (claims authority) but the regex ^master$ matches no version string
  - PRIMS estate Phase0 obs #2
  - ADR-001 COLE AC-G1-003: hotfix with IsReleaseBranch=false (correct)
Severity: Error
```

---

### SEM-RULE-002: Primary Branch Lineage Isolation

```
Name:      Primary Branch Lineage Isolation
Statement: A primary branch (is-main-branch: true) must declare source-branches: []
           to prevent version inheritance from non-primary ancestors via merge
           commit parent traversal.
Rationale: Without source-branches: [], GitVersion follows the merge commit's
           parent chain through any merged branch (e.g. release → develop),
           inheriting that branch's label and commit count. The primary branch
           produces a prerelease version derived from a non-primary ancestor
           instead of a stable version from its own config and tags.
           source-branches: [] declares "I have no valid version ancestors —
           use only my own tags and config."
Violations:
  - is-main-branch: true without source-branches: []
  - is-main-branch: true with source-branches containing any non-empty list
    (note: support branches are a legitimate exception to this rule)
Evidence:
  - PRIMS 1.62.0-alpha.261 incident (root cause)
  - ADR-001 Phase0 obs #7
  - PRIMS estate Phase0 obs #3
Severity: Error
```

---

### SEM-RULE-003: Label Substitution Requires Named Capture

```
Name:      Label Substitution Requires Named Capture
Statement: A branch whose label contains {BranchName} must have a regex that
           includes a named capture group (?<BranchName>.+).
Rationale: GitVersion substitutes {BranchName} from the named capture group in
           the branch regex. If the group is absent, the literal string
           "{BranchName}" is emitted as the prerelease label, making all branches
           of that type indistinguishable in version output.
Violations:
  - label: '{BranchName}' with regex that contains no (?<BranchName>...) group
  - label: '{BranchName}' with regex: '' (empty)
Evidence:
  - strata hotfix: label: '{BranchName}' (if it were set) with regex: ^hotfix/
    (no capture group)
  - ADR-001 COLE AC-G1-003 negative case: WRONG_LABEL
  - PRIMS estate Phase0 obs #8
Severity: Error
```

---

### SEM-RULE-004: Deployment Mode Topology Coherence

```
Name:      Deployment Mode Topology Coherence
Statement: A branch intended to produce a prerelease label must have an explicit
           deployment mode of ContinuousDelivery or ManualDeployment. A root-level
           mode of ContinuousDeployment must not be relied upon as the default for
           branches that carry prerelease labels.
Rationale: ContinuousDeployment suppresses prerelease labels — every commit is
           treated as a release. A branch that depends on prerelease output
           (develop, feature, bugfix, hotfix, release) must override to
           ContinuousDelivery or ManualDeployment. Using ContinuousDeployment as
           a root default with prerelease-carrying branches that have no explicit
           override produces wrong version output silently.
Violations:
  - Root mode: ContinuousDeployment AND any branch with a non-empty label that
    has no explicit mode: override
  - Equivalently: root mode: ContinuousDeployment with develop/feature/bugfix/
    hotfix branches having no mode: field
Evidence:
  - PRIMS foundation/strata/git-check: root ContinuousDeployment, no branch overrides
  - PRIMS estate Phase0 obs #1 and #4
Severity: Warning (Error if combined with SEM-RULE-002 violation)
```

---

### SEM-RULE-005: Source Branch Reference Integrity

```
Name:      Source Branch Reference Integrity
Statement: Every branch key named in any branch's source-branches list must exist
           as a defined branch in the configuration.
Rationale: GitVersion uses source-branches to constrain lineage traversal. A
           reference to an undefined branch key is silently ignored in some
           versions, or causes a ConfigurationException in others. Either outcome
           is wrong: the former produces unexpected lineage; the latter breaks
           version calculation entirely.
Violations:
  - source-branches: [develop, main, release, support, hotfix] where "support"
    is not defined in branches map
Evidence:
  - ADR-001 COLE AC-G1-004 negative case: SOURCE_BRANCH_VALIDATION_FAILURE
  - ConfigurationException observed when adding bugfix branch with undefined
    source-branches in ADR-001 implementation session
Severity: Error
```

---

### SEM-RULE-006: Strategies Must Be Declared

```
Name:      Strategies Must Be Declared
Statement: The strategies block should be explicitly declared. Relying on
           GitVersion's default strategy set makes configuration behaviour
           fragile across GitVersion version upgrades.
Rationale: GitVersion's default strategy composition has changed between major
           versions. A config that worked on v5 may silently produce different
           versions on v6 if strategy defaults changed. Explicit declaration
           makes the semantic contract version-stable.
Violations:
  - Absence of strategies: block in any GitVersion.yml
Evidence:
  - PRIMS estate Phase0 obs #5: all three configs lack strategies block
Severity: Warning
```

---

### SEM-RULE-007: Increment Strategy Totality

```
Name:      Increment Strategy Totality
Statement: A branch with increment: Inherit must declare at least one entry in
           source-branches. You cannot inherit a version from the void.
Rationale: increment: Inherit means "use the increment strategy of my source
           branch." If source-branches is empty or absent, there is no source
           to inherit from. GitVersion falls back to default behaviour which
           may be correct by accident but is undefined by contract. This produces
           fragile configurations whose behaviour changes when GitVersion's
           internal defaults change.
Violations:
  - increment: Inherit AND source-branches: [] (empty list)
  - increment: Inherit AND source-branches absent
Exception:
  - A branch with increment: Inherit and no source-branches that explicitly
    has a root-level increment set — the root acts as the fallback. This is
    marginal and should still be warned as implicit.
Evidence:
  - Co-authored during semantic rule catalogue session 2026-05-12
  - All PRIMS estate feature/bugfix branches: increment: Inherit, no source-branches
Severity: Warning
```

---

## Rules Under Consideration (Not Yet Ratified)

These are candidate rules from the session context, not yet stable enough to implement:

```
SEM-RULE-007 (candidate): Increment Coherence with Authority Role
  A version authority branch (is-release-branch: true) should use increment: None
  or have its increment derived solely from its branch name, not from parent lineage.
  Under consideration — release: increment: None vs Minor both have defensible cases.

SEM-RULE-008 (candidate): Prerelease Weight Ordering
  Branches higher in the release pipeline should have higher pre-release-weight
  values (release > feature > develop). Inverted weights produce incorrect
  NuGet package ordering.
  Under consideration — weight values are often omitted and default correctly.
```

---

## Mapping: Rules → PRIMS Estate Violations

| Rule | foundation | strata | git-check |
|---|---|---|---|
| SEM-001 (Authority Exclusivity) | ✗ master | ✗ master | ✗ master |
| SEM-002 (Lineage Isolation) | ✗ master | ✗ master | ✗ master |
| SEM-003 (Capture Group) | n/a | ✗ hotfix regex | n/a |
| SEM-004 (Mode Coherence) | ✗ root+all branches | ✗ root+all branches | ✗ root+all branches |
| SEM-005 (Reference Integrity) | implicit only | implicit only | implicit only |
| SEM-006 (Strategies) | ✗ absent | ✗ absent | ✗ absent |

All three repos: **SEM-001, SEM-002, SEM-004, SEM-006**  
strata only: **SEM-003**

---

## Relationship to Exemplars

ADR-001 (GitFlow) satisfies all six rules.  
TrunkBased (preview1) satisfies all six rules.  
Any future exemplar must satisfy all ratified rules before it can be declared canonical.
