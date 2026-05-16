# CLI Output Draft — `gitversion /validate`

**Status:** Draft, pre-implementation  
**Purpose:** Crystallise UX before writing code. Three scenarios cover the full output surface.  
**Constraint:** Output must teach the physics, not just report the verdict.

---

## Design Principles

- Hard errors exit non-zero; advisories and clean passes exit 0
- Every violation names the rule ID and branch — never just "invalid config"
- Remediation is one actionable sentence
- Advisory messages explain *why* the situation is by design, not a bug
- Clean pass is a positive signal, not silence

---

## Scenario 1 — Hard Error (SEM-001: Identity Crisis on master)

Input: PRIMS foundation-style config — `master: is-release-branch: true, regex: ^master$`

```
$ gitversion /validate
GitVersion Semantic Configuration Validator

❌  SEM-001  Error   branch 'master'
    Authority/Carrier Exclusivity violated.
    Branch 'master' declares is-release-branch: true but its regex '^master$'
    cannot match a version string (no version pattern or capture group).
    GitVersion will attempt VersionInBranchName on 'master', fail to parse
    a version, and fall back to lineage traversal — inheriting the first
    ancestor label (commonly 'alpha' from develop).

    Remediation: Set is-main-branch: true and is-release-branch: false.
    Primary branches derive their version from tags, not from their name.

ℹ   SEM-004  Advisory  branch 'develop'
    This configuration is valid. Branch 'develop' uses ContinuousDeployment
    mode while carrying the prerelease label 'alpha'. GitVersion will include
    the label in version output while treating each commit as a deployable
    version.

    Remediation: No action is required. To model prerelease versions distinct
    from deployable releases, configure mode: ContinuousDelivery on this branch.

ℹ   SEM-006  Advisory  (root)
    This configuration is valid. No strategies block is declared; GitVersion
    will use its built-in defaults. The default strategy set has changed
    across major versions, so behaviour may change silently on upgrade.

    Remediation: No action is required. To lock strategy behaviour across
    GitVersion upgrades, declare the strategies list explicitly — for GitFlow:
    [Fallback, ConfiguredNextVersion, MergeMessage, TaggedCommit,
    TrackReleaseBranches, VersionInBranchName].

─────────────────────────────────────────────
  1 error · 0 warnings · 2 advisories
  Configuration is semantically invalid. Fix errors before relying on output.

Exit code: 1
```

---

## Scenario 2 — Advisory Only (SEM-008: Shadowed Directive)

Input: Valid config where a release branch has CommitMessageIncrementing: Enabled
(the default) — directives on that branch are inert.

```
$ gitversion /validate
GitVersion Semantic Configuration Validator

ℹ   SEM-008  Advisory  branch 'release'
    Commit directives shadowed by version authority.
    Branch 'release' is a Version Authority (is-release-branch: true).
    Its version is determined by the branch name (e.g. release/1.2.0 → 1.2.0).
    Any +semver: commit directives on this branch are inert by design.

    Note: This is not a misconfiguration. Directives are increment proposals;
    they yield to an explicit version authority. A contributor committing
    '+semver: major' on a release branch will not change the version.

    No action required.

─────────────────────────────────────────────
  0 errors · 0 warnings · 1 advisory
  Configuration is semantically valid.

Exit code: 0
```

---

## Scenario 3 — Clean Pass

Input: ADR-001 canonical GitFlow or TrunkBased config

```
$ gitversion /validate
GitVersion Semantic Configuration Validator

✅  All semantic invariants satisfied.
    Branches, deployment modes, source-branch lineage, and label
    templates are consistent with the declared authority/carrier model.

─────────────────────────────────────────────
  0 errors · 0 warnings · 0 advisories
  Configuration is semantically valid.

Exit code: 0
```

---

## Output Contract

| Category | Exit Code | Meaning |
|---|---|---|
| Error | 1 | Illegal semantic state — output non-deterministic |
| Warning | 0 | Legal but fragile — output may be surprising |
| Advisory | 0 | Legal and intentional — explanation offered |
| Clean | 0 | All invariants satisfied |

**Important:** Warnings and advisories never block CI. Only errors block.  
This preserves "explanation over enforcement" — the validator teaches, CI gates.

---

## Directive Precedence (for error messages and docs)

When explaining why a version came out unexpectedly:

```
1. Tag on current commit          → exact version, overrides everything
2. Version Authority branch name  → fixes base version, directives inert
3. Commit directive (+semver:)    → proposes increment, yields to authority
4. Branch configuration           → default increment, yields to directives
```

This stack is **never surfaced as a rule violation** — it is surfaced as
**explanatory context** when a user asks why their directive did nothing.
SEM-008 (candidate) is the advisory that surfaces it proactively.
