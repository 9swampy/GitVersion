# Stack 4 — `/validate --explain` design pass

## Scope

The experience writeup (`docs/feedback/yaml-generation-experience.md`)
recommended:

> Have `/validate` accept `--explain` to show *which preset fields* pulled
> the branch into a contradictory state. SEM-001 against `hotfix` was
> clear once I knew the preset, but a first-time user wouldn't know
> `hotfix` inherits `is-release-branch: true`.

The user-facing gap: when the validator flags a contradiction on a
branch family, the user has no surfaced way to see *why* each
contributing field has its current value. Today the diagnostic gives
the field's value and a remediation; the *provenance* (preset vs file
override vs internal default) is invisible.

## CJE-V3 application

**§C.3.1 premise validation:** does this critique require a concrete
additional consumer to be valid?
- **Yes** — the experience-writeup author had to read
  `TrunkBasedConfigurationBuilder.cs` to discover that `hotfix`
  inherits `is-release-branch: true`. The consumer is real and
  documented.

**§C.3.1a classification vs disposition:**
- **Classification:** ALIVE — the workflow gap is evidence-based.
- **Disposition:** IMPLEMENT — the workflow exists and the cost is
  bounded once the right option is selected.

**§C.7 prefer silence when uncertain:** uncertainty here is about
implementation shape, not whether to act. The design pass converts
implementation uncertainty into a specific choice; once made, silence
is no longer the answer.

## Configuration loading architecture (relevant slice)

From `src/GitVersion.Configuration/ConfigurationProvider.cs:23-73`:

```
overrideConfigurationFromFile   ← user's GitVersion.yml (raw dict)
overrideConfigurationFromWorkflow ← preset contribution (raw dict)
overrideConfiguration           ← /overrideconfig params (raw dict)
                ↓
        ConfigurationBuilder.AddOverride() x3
                ↓
        IGitVersionConfiguration (the merged, typed result)
```

The merge order is `workflow → file → params` so later items override
earlier. After `Build()` the typed result loses provenance — there is
no per-field record of which source supplied each value.

**Key facts for the design:**

1. The raw override dictionaries are available *inside* `ConfigurationProvider`
   but are not exposed on `IConfigurationProvider`.
2. `WorkflowManager.GetOverrideConfiguration(workflow)` returns the
   preset's contribution as a dictionary keyed by branch family.
3. The validator currently consumes only the merged `IGitVersionConfiguration`
   — it has no access to the override dictionaries.

## Options considered

### Option A — Track provenance during config build

Modify `ConfigurationBuilder.AddOverride` to record per-field source
attribution. Expose a parallel `IGitVersionConfigurationProvenance` on
the build output that maps `(branchName, fieldName) → source`.

| Why fired | Comprehensive — every field carries its provenance through the rest of the pipeline. |
| Why suppressed | Touches the builder internals invasively. Every override path in the codebase has to thread provenance. Wide blast radius for a single `/validate --explain` consumer. |
| Confidence | High that it would work; high that it is too large. |

**Verdict:** rejected — disproportionate scope.

### Option B — Shadow re-resolve (preset-only build)

For each violation, build a second configuration with *only* the
workflow override (no file, no params). Compare field-by-field with
the user's effective configuration. Difference = user override; match
= preset-inherited.

| Why fired | Doesn't modify the builder. Self-contained inside the validator. |
| Why suppressed | Requires resolving a second full configuration. Each `--explain` invocation pays the cost. Implies the validator pulls in `ConfigurationProvider` (which currently it does not — it is a pure function of `IGitVersionConfiguration`). |
| Confidence | High that it would work; medium that it is the right shape. |

**Verdict:** rejected — couples validator to provider, costlier than
necessary.

### Option C — Dictionary lookup on raw overrides

Expose a thin new method on `IConfigurationProvider`:

```csharp
ConfigurationProvenance ResolveProvenance(string? overrideFromCli);
```

where `ConfigurationProvenance` carries the raw override dictionaries
(`fromFile`, `fromWorkflow`, `fromCliOverride`) and the workflow name.
The `/validate --explain` executor calls this once, then for each
violation does up to three dictionary lookups to determine where the
field came from. No re-merge, no shadow build, no provider invocation
inside the validator.

| Why fired | Smallest change. Validator stays pure; provider grows one method. The provenance dictionaries already exist — only their exposure is new. Dictionary lookups are O(1) per field. |
| Why suppressed | Adds `ConfigurationProvenance` to the public surface of `GitVersion.Core` / `GitVersion.Configuration`. |
| Confidence | High that it works; high that it is the right shape. |

**Verdict:** **selected.**

## Recommended implementation

### Public-surface additions

```csharp
// GitVersion.Configuration
public sealed record ConfigurationProvenance(
    string? Workflow,
    IReadOnlyDictionary<object, object?>? FromFile,
    IReadOnlyDictionary<object, object?>? FromWorkflow,
    IReadOnlyDictionary<object, object?>? FromCliOverride);

public interface IConfigurationProvider
{
    // (existing) IGitVersionConfiguration Provide(...);
    ConfigurationProvenance ResolveProvenance(IReadOnlyDictionary<object, object?>? overrideConfiguration);
}
```

### Executor wiring

`/explain` is a boolean modifier on `/validate`:

```bash
gitversion /validate /explain        # text mode
gitversion /validate /explain -output json  # JSON envelope
```

Parser adds `Arguments.ExplainProvenance`, ConfigurationInfo adds
`ExplainProvenance`, ToOptions wires it.

In `RunValidation`, when `gitVersionOptions.ConfigurationInfo.ExplainProvenance`
is true:
1. Call `configurationProvider.ResolveProvenance(overrideConfiguration)`
   to get the raw dictionaries.
2. For each `SemanticViolation`, attach an inferred provenance for the
   branch family using `LookupBranchFieldSource(provenance, violation.BranchName)`:
   - field present in `FromCliOverride.branches.<name>` → "set by /overrideconfig"
   - field present in `FromFile.branches.<name>` → "set in your GitVersion.yml"
   - field present in `FromWorkflow.branches.<name>` → "inherited from workflow: <name>"
   - otherwise → "from internal defaults"
3. Emit the provenance string alongside the existing remediation in
   the text/JSON output.

### Output shape

Text mode adds an indented `Source:` line per violation:

```
❌  SEM-001  Error  branch 'hotfix'
    Authority/Carrier Exclusivity
    Branch 'hotfix' declares is-release-branch: true but its regex
    '^hotfix(es)?[\/-](?<BranchName>.+)' has no version pattern...

    Source:      is-release-branch inherited from workflow: TrunkBased/preview1
    Remediation: ...
```

JSON envelope adds an optional `source` field per violation, present
only when `--explain` was supplied. Backward-compatible: callers that
don't pass `--explain` see the existing schema verbatim.

## Test surface

`ValidateCommand.WhenExplainRequested` scenario fixture:

- Setup writes a GitVersion.yml with `workflow: TrunkBased/preview1`
  and a deliberately contradictory branch (e.g. `hotfix` with regex
  overridden but `is-release-branch` inherited from preset).
- Asserts:
  - Exit code per existing validator contract
  - Output contains `Source: ... inherited from workflow:` for the
    inherited field
  - Output contains `Source: ... set in your GitVersion.yml` for the
    user-overridden regex
  - JSON envelope contains the `source` field per violation when
    `-output json` is supplied

## Scope estimate

| Sub-commit | Surface | Risk |
|---|---|---|
| 4a | Parser flag + ConfigurationInfo wiring + Arguments | Trivial; same shape as 003a |
| 4b | `IConfigurationProvider.ResolveProvenance` + `ConfigurationProvenance` record + PublicAPI declarations | Moderate; new public surface |
| 4c | `RunValidation` consumes provenance; output formatting; in-process tests; help text + AllArgsAreInHelp + subprocess test | Moderate |

Each leaves the build green if landed sequentially under the same
discipline as Stack 3.

## Open questions

1. Should `--explain` imply `/validate`, or be a no-op without it? The
   plan assumes the latter (modifier only).
2. Where does the "from internal defaults" attribution come from? The
   default-resolution path is in `ConfigurationBuilder.Build` — does
   the merge dictionary capture this, or do we leave that case as a
   bare "(default)" string? Recommend the latter for v1.
3. Does the existing `/overrideconfig` parameter shape match the
   dictionary structure the executor passes? Need to verify before 4b
   wiring lands.

## Decision required from maintainer

Before implementation begins:
- (a) Approve Option C and the three-sub-commit shape; proceed to 4a.
- (b) Reject Option C and request reconsideration of A or B.
- (c) Defer Stack 4 entirely (keep this design note as the durable
      record; mark in the plan checklist that the design pass concluded
      with a deferral).
