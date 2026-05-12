# GitVersion Semantic Validator — Getting Started Guide

**Status:** Delivered  
**Drives:** `arguments.md` update, `HelpWriter` output, JSON schema, CI/CD docs  
**Constraint:** `/validate` must be additive — no changes to existing version computation paths

> **Commit directives never define a version.**  
> They only propose a minimum increment when no explicit version authority is present.
> This is invariant; it is not configurable. The validator teaches it; the CLI reflects it.

---

## The Foundational Distinction

> **Validation closes intention. Fixtures close expectation.**

`/validate` answers: *"Is my configuration semantically coherent?"*  
TDD fixtures answer: *"Does it produce exactly the lifecycle my team expects?"*

These are different questions. `/validate` is required; fixtures are optional.

**User journey:**
1. Start from an Exemplar — `CanonicalGitFlowYaml` or `CanonicalTrunkBasedYaml` (proven blueprints)
2. Modify to fit your workflow
3. Run `/validate` until no errors — physics are now sound
4. Optionally: write TDD fixtures if your configuration becomes an organizational contract

Fixtures are for **institutional memory**, not individual understanding. Platform teams write them; repo owners don't need to.

---

## Quick Start

```bash
# Validate configuration in current directory
gitversion /validate

# Validate a specific directory
gitversion /path/to/repo /validate

# Validate a specific config file
gitversion /validate /config GitVersion.custom.yml

# Validate and emit JSON (for CI tooling)
gitversion /validate /output json

# Use in CI — non-zero exit if any errors
gitversion /validate || exit 1
```

---

## Command Reference

### Synopsis

```
gitversion [path] /validate [options]
```

`/validate` is a standalone mode flag. **Mutually exclusive with normal version calculation —
when specified, no version variables are produced.**

When present, GitVersion:
1. Locates and loads the configuration file (see "Discovery" below)
2. Runs the Semantic Configuration Validator
3. Emits structured output describing violations
4. Exits with code 0 (valid) or 1 (errors found)

**Does not require a git repository.** Configuration validation is a static analysis
of `GitVersion.yml` — no branch traversal, no version computation, no git history access.

#### Discovery (implementation constraint)

GitVersion's normal configuration resolution uses the `.git` directory to establish
the project root before locating `GitVersion.yml`. The `/validate` path **must not throw
`RepositoryNotFoundException`** when no `.git` is present.

Implementation requirement: if `.git` is not found, fall back to searching for
`GitVersion.yml` / `GitVersion.yaml` / `.GitVersion.yml` / `.GitVersion.yaml` starting
from the specified path (or current directory). This enables `/validate` to work in:
- Standalone YAML editing (IDE extensions, config authoring tools)
- Pre-commit hooks (before a repo is initialised)
- CI validation steps that only have access to the config file

This is a deliberate degree of freedom to protect during implementation.

---

### Parameters

All existing path and config resolution parameters are honoured. `/validate`-specific
behaviour noted where it differs.

```
gitversion [path] /validate [options]

  path              The directory containing GitVersion.yml (or a parent).
                    If not specified, uses the current directory.
                    (Must be first argument if provided)

  /validate         Activates semantic configuration validation mode.
                    Mutually exclusive with normal version computation.

  /config           Path to a specific GitVersion configuration file.
                    Defaults to GitVersion.yml, GitVersion.yaml,
                    .GitVersion.yml, or .GitVersion.yaml in [path].

  /output           Output format for validation results.
                    text      Human-readable (default). Teaching-oriented
                              messages that explain the violated invariant.
                    json      Machine-readable JSON. Suitable for CI tooling,
                              dashboards, and programmatic consumption.

  /l                Path to logfile; 'console' emits to stdout.
                    Validation output itself always goes to stdout.

  /verbosity        Controls diagnostic log output (not violation output).
                    Quiet, Minimal, Normal (default), Verbose, Diagnostic.
```

**Note:** The following parameters are silently ignored in `/validate` mode
(they are git-repository or version-computation concerns):
`/nofetch`, `/nonormalize`, `/nocache`, `/allowshallow`, `/b`, `/u`, `/p`,
`/c`, `/dynamicRepoLocation`, `/url`, `/updateassemblyinfo`, `/updateprojectfiles`,
`/showvariable`, `/format`, `/outputfile`, `/overrideconfig`

---

### Help Emission (`/help` output — updated entry)

The following entry is added to the existing `/help` output block in `arguments.md`:

```
    /validate       Validates the GitVersion configuration file against
                    the semantic rule catalogue without computing a version.
                    Mutually exclusive with normal version calculation —
                    no version variables are produced when specified.
                    Does not require a git repository; works on a standalone
                    GitVersion.yml file.
                    Exit code 0 = valid (warnings and advisories do not block).
                    Exit code 1 = one or more errors found.
                    Use /output json for machine-readable output.
                    Use /config to specify a non-default config file path.
```

---

## Output Formats

### Text (default)

Teaching-oriented. Each violation names the rule ID, severity, affected branch,
the invariant that was violated, and one remediation action.

```
GitVersion Semantic Configuration Validator

❌  SEM-001  Error   branch 'master'
    Authority/Carrier Exclusivity violated.
    Branch 'master' declares is-release-branch: true but its regex '^master$'
    cannot match a version string (no version pattern or named capture group).
    GitVersion will attempt VersionInBranchName on 'master', fail to parse
    a version, and traverse the merge parent chain — commonly inheriting
    'alpha' from develop and the full develop commit count.

    Remediation: Set is-main-branch: true and is-release-branch: false.
    Primary branches derive their version from tags, not from their name.

⚠   SEM-006  Warning  (root)
    No strategies block declared. GitVersion's default strategy set has
    changed across versions and may silently change on upgrade.

    Remediation: Add strategies: [Fallback, ConfiguredNextVersion, MergeMessage,
    TaggedCommit, TrackReleaseBranches, VersionInBranchName]

─────────────────────────────────────────────────────────────────
  1 error · 1 warning
  Configuration is semantically invalid. Fix errors before relying on output.

Exit code: 1
```

### Text — advisory only

```
GitVersion Semantic Configuration Validator

ℹ   SEM-008  Advisory  branch 'release'
    Commit directives shadowed by version authority.
    Branch 'release' is a Version Authority (is-release-branch: true).
    Its version is determined by the branch name (e.g. release/1.2.0 → 1.2.0).
    Any +semver: commit directives on this branch are inert by design —
    directives propose an increment; they yield to an explicit authority.

    No action required.

─────────────────────────────────────────────────────────────────
  0 errors · 0 warnings · 1 advisory
  Configuration is semantically valid.

Exit code: 0
```

### Text — clean pass

```
GitVersion Semantic Configuration Validator

✅  All semantic invariants satisfied.

─────────────────────────────────────────────────────────────────
  0 errors · 0 warnings · 0 advisories
  Configuration is semantically valid.

Exit code: 0
```

### JSON (`/output json`)

Structured output for CI dashboards and programmatic consumption.

```json
{
  "valid": false,
  "summary": {
    "errors": 1,
    "warnings": 1,
    "advisories": 0
  },
  "violations": [
    {
      "ruleId": "SEM-001",
      "title": "Authority/Carrier Exclusivity",
      "severity": "Error",
      "branchName": "master",
      "message": "Branch 'master' declares is-release-branch: true but its regex '^master$' cannot match a version string (no version pattern or named capture group). VersionInBranchName strategy will fail to parse the branch name.",
      "remediation": "Set is-main-branch: true and is-release-branch: false. Primary branches derive their version from tags, not from their name.",
      "causalNote": null
    },
    {
      "ruleId": "SEM-006",
      "title": "Strategies Must Be Declared",
      "severity": "Warning",
      "branchName": null,
      "message": "No strategies block declared. GitVersion's default strategy set has changed across versions and may silently change on upgrade.",
      "remediation": "Add strategies: [Fallback, ConfiguredNextVersion, MergeMessage, TaggedCommit, TrackReleaseBranches, VersionInBranchName]",
      "causalNote": null
    }
  ]
}
```

Clean pass JSON:

```json
{
  "valid": true,
  "summary": {
    "errors": 0,
    "warnings": 0,
    "advisories": 0
  },
  "violations": []
}
```

---

## Input Schema

### What the validator reads

`/validate` operates on `IGitVersionConfiguration` — the same object GitVersion
uses internally after loading `GitVersion.yml`. It checks the following fields:

| Field | Rules checked |
|---|---|
| `mode` (root) | SEM-004 |
| `strategies` | SEM-006 |
| `branches[*].is-main-branch` | SEM-001, SEM-002 |
| `branches[*].is-release-branch` | SEM-001 |
| `branches[*].regex` | SEM-001, SEM-003 |
| `branches[*].label` | SEM-003, SEM-004 |
| `branches[*].mode` | SEM-004 |
| `branches[*].source-branches` | SEM-002, SEM-005, SEM-007 |
| `branches[*].increment` | SEM-007 |

### What the validator does NOT read

- Git repository state (branches, commits, tags)
- Commit messages (no `+semver:` directive inspection)
- Assembly info files
- Build server environment variables

The validator is a **pure function over configuration**. It requires only the
`GitVersion.yml` file and produces a deterministic result.

---

## Semantic Rules Reference (ratified, SEM-001–007)

| Rule | Name | Severity | Short Description |
|---|---|---|---|
| SEM-001 | Authority/Carrier Exclusivity | Error | A branch may not simultaneously claim version authority and calculate an increment |
| SEM-002 | Primary Branch Lineage Isolation | Error | An `is-main-branch: true` branch sourcing prerelease-labelled branches risks label inheritance |
| SEM-003 | Variable Capture Contract | Error | `{BranchName}` in label requires `(?<BranchName>)` in regex |
| SEM-004 | Deployment Mode Consistency | Warning | Root `ContinuousDeployment` + labelled branches without explicit mode override |
| SEM-005 | Source Branch Reference Integrity | Error | `source-branches` entry references an undefined branch key |
| SEM-006 | Strategies Must Be Declared | Warning | Absent `strategies` block — fragile across GitVersion upgrades |
| SEM-007 | Increment Strategy Totality | Warning | `increment: Inherit` with no `source-branches` |
| SEM-008 *(advisory, candidate)* | Shadowed Directive | Advisory | `+semver:` directives inert on Version Authority branches |

---

## CI/CD Integration

### GitHub Actions

```yaml
- name: Validate GitVersion configuration
  run: dotnet tool run gitversion /validate

# Or fail the build on errors:
- name: Validate GitVersion configuration
  run: |
    gitversion /validate
    if [ $? -ne 0 ]; then
      echo "GitVersion semantic validation failed — check output above"
      exit 1
    fi
```

### Azure Pipelines

```yaml
- script: gitversion /validate
  displayName: Validate GitVersion configuration
  failOnStderr: false

# JSON output for structured logging:
- script: gitversion /validate /output json | tee gitversion-validation.json
  displayName: Validate GitVersion configuration
```

### Exit Code Contract

| Exit code | Meaning | CI behaviour |
|---|---|---|
| 0 | No errors (may have warnings or advisories) | ✅ Pass |
| 1 | One or more errors | ❌ Fail |

Warnings and advisories never block CI. Only semantic errors (illegal configurations
that will produce non-deterministic versions) exit non-zero.

---

## Closure Model

| Level of Closure | Mechanism | Who needs it |
|---|---|---|
| **Logic & Physics** | `gitversion /validate` | Every GitVersion user |
| **Edge Case Verification** | TDD Fixtures (optional) | Platform teams / shared org configs |
| **Continuous Integrity** | CI Integration | Teams preventing config drift |

Once `/validate` returns green with no errors:
- The configuration's physics is coherent
- There is exactly one authority at every decision point  
- Any ignored signals (e.g. shadowed directives) are explained, not accidental

**No additional artefacts are required to reach semantic closure at the configuration level.**

---

## CI/CD Semantic Regression Detection

CI usage is intended to detect **semantic regressions in configuration** — cases where a
configuration change introduces an illegal branch topology that will cause non-deterministic
version output. It is not designed to validate repository topology or commit behaviour.

A CI failure on `/validate` always means: **the configuration's declared intent is now
internally inconsistent**. It points to a config change, not a developer mistake.

---

## Resolution Order (explanatory only)

When explaining why a version output surprised you, GitVersion resolves version inputs
in the following order. **This ordering is not enforced by the validator and is never
treated as a configuration error.**

```
1. Tag on current commit          → exact version, overrides everything
2. Version Authority branch name  → fixes base version; directives inert
3. Commit directive (+semver:)    → proposes a minimum increment
4. Branch configuration           → default increment; lowest precedence
```

The validator explains when items in this order conflict (e.g. SEM-008 advisory: a
commit directive on a Version Authority branch). It never flags the order itself as wrong.

---

## Relationship to `/showconfig`

| | `/showconfig` | `/validate` |
|---|---|---|
| What it does | Shows the resolved effective config as YAML | Checks the config against semantic invariants |
| Output | YAML serialisation of merged configuration | Structured violation report |
| Requires git repo | Yes (for project root discovery) | No |
| Exit code | Always 0 | 0 (valid) or 1 (errors) |
| Use case | "What config am I actually running?" | "Is my config semantically coherent?" |

These are complementary. Run `/showconfig` to see what GitVersion sees; run `/validate`
to verify it is semantically sound.
