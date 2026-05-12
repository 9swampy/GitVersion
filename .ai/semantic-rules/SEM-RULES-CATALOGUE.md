# Semantic Rule Catalogue — GitVersion Configuration

**Version:** 0.1  
**Status:** Ratified — validator implementation complete  
**Date:** 2026-05-12

---

## Purpose

This catalogue defines workflow-agnostic semantic invariants for GitVersion configuration. Rules are stated in terms of GitVersion's semantic model, not in terms of any specific workflow (GitFlow, TrunkBased, etc.).

- Exemplar configurations **instantiate** these rules (GitFlow, TrunkBased)
- Real-world configurations that produce unexpected version output **demonstrate** these violations
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
  - GitFlow configuration where master has is-release-branch: true but
    regex ^master$ matches no version string — VersionInBranchName fails
    and lineage traversal produces unexpected pre-release output
  - GitFlow canonical fixture: hotfix with IsReleaseBranch=false (correct)
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
  - GitFlow configuration: primary branch merge commit producing
    unexpected pre-release output (e.g. alpha.261) rather than stable
    version — caused by merge parent chain traversal through develop
  - GitFlow canonical fixture Phase0 observation #7
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
  - GitFlow hotfix configuration with label: '{BranchName}' and regex: ^hotfix/
    (no capture group) — literal string emitted instead of branch name
  - GitFlow canonical fixture WRONG_LABEL negative case
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
  - GitFlow configurations with root ContinuousDeployment and no branch-level
    mode overrides — prerelease labels silently suppressed on develop/feature/hotfix
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
  - Multiple observed GitFlow configurations without strategies block — behaviour
    changed silently across GitVersion major versions
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
  - Feature/bugfix branches in observed GitFlow configurations: increment: Inherit
    with no source-branches — lineage falls through to engine defaults
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

SEM-RULE-008 (candidate): Shadowed Directive Advisory
  A branch that is a Version Authority (is-release-branch: true) and also has
  CommitMessageIncrementing: Enabled (explicitly or by inheritance) presents a
  configuration where commit directives (+semver: major/minor/patch) will be
  silently ignored. The version is determined by the branch name; directives
  are inert.

  This is NOT a violation — it is by design. But it is a common source of
  confusion: a contributor commits "+semver: major" on a release branch and
  wonders why the version did not jump.

  Status: ADVISORY only (informational, non-blocking, exit code 0).
  Severity: Advisory (distinct from Warning — no corrective action needed)

  The corrected Authority/Directive model (co-authored session 2026-05-12):
    - Version Authorities (tags, branch names): fix the base version
    - Commit Directives (+semver:): propose an increment — inert when an authority governs
    - Branch Configuration: default increment — yields to both authorities and directives
  
  Ratification blocker: the validator currently operates purely over
  IGitVersionConfiguration (static config). Detecting shadowed directives at
  configuration-load time requires knowing whether CommitMessageIncrementing
  is enabled for a Version Authority branch — which IS statically derivable.
  Ratification pending: determine if the advisory is useful enough to warrant
  the per-branch traversal cost in the validator.

SEM-RULE-009 (candidate): Prerelease Weight Ordering
  Branches higher in the release pipeline should have higher pre-release-weight
  values (release > feature > develop). Inverted weights produce incorrect
  NuGet package ordering.
  Under consideration — weight values are often omitted and default correctly.

SEM-RULE-010 (DEFERRED — 2026-05-12): Grammar Integrity
  Deferred per C.3.1 (no concrete corpus — no real config currently uses
  assembly-informational-format, assembly-versioning-format, or
  assembly-file-versioning-format fields that would trip this rule).

  Ratification trigger: when at least one real config uses a {Variable} placeholder
  in an assembly format field, or when synthesis emits YAML containing such fields.

  SYNTHESIS GATE RESOLVED: The synthesis intake gate is satisfied by direct use of
  GitVersionVariables.AvailableVariables (internal, accessible via InternalsVisibleTo).
  Synthesis does not require SEM-010 to be ratified as a validator rule — it takes
  a direct codebase dependency on the variable list instead.

  TWO DISTINCT USES (not conflated):
    (a) Validator rule: checks assembly format strings in GitVersion.yml — DEFERRED
    (b) Synthesis intake gate: checks user-provided output examples against known
        variables — resolved via direct dependency, not via validator rule

  Source of truth when ratified:
    Variables:  GitVersionVariables.AvailableVariables (28 variables, static list)
    Formatters: ValueFormatter chain (StringFormatter, FormattableFormatter,
                NumericFormatter, DateFormatter)
    Conditional: LegacyCompositeFormatter semicolon syntax

  This grammar IS extractable from the codebase — no hand-coding required.

  Violations:
    - {UnknownVariable} in any label or format field
    - {CommitsSinceVersionSource:!!invalid!!} — invalid format specifier for type
    - {BranchName:0000} — numeric format on string variable

  Severity: Error
```

---

## Mapping: Rules → Example Configuration Violations

The following table shows how these rules apply to three observed GitFlow
configurations (Config A, Config B, Config C) that were producing unexpected
version output. All three share the same template origin.

| Rule | Config A | Config B | Config C |
|---|---|---|---|
| SEM-001 (Authority Exclusivity) | master | master | master |
| SEM-002 (Lineage Isolation) | master | master | master |
| SEM-003 (Capture Group) | n/a | hotfix regex | n/a |
| SEM-004 (Mode Coherence) | root+all branches | root+all branches | root+all branches |
| SEM-005 (Reference Integrity) | implicit only | implicit only | implicit only |
| SEM-006 (Strategies) | absent | absent | absent |

Config A and C: **SEM-001, SEM-002, SEM-004, SEM-006**  
Config B additionally: **SEM-003**

---

## Relationship to Exemplars

ADR-001 (GitFlow) satisfies all six rules.  
TrunkBased (preview1) satisfies all six rules.  
Any future exemplar must satisfy all ratified rules before it can be declared canonical.
