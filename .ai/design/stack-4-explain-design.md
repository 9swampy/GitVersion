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

_Refined post-maintainer-review (2026-05-16). Five MUST-adjust corrections
applied; rationales preserved at the end of this section per §C.5._

### Public-surface additions

```csharp
// GitVersion.Configuration
public sealed record ConfigurationProvenance(
    string? Workflow,
    IReadOnlyDictionary<string, object?>? FromFile,
    IReadOnlyDictionary<string, object?>? FromWorkflow,
    IReadOnlyDictionary<string, object?>? FromCliOverride);

public interface IConfigurationProvider
{
    // (existing) IGitVersionConfiguration Provide(...);
    ConfigurationProvenance ResolveProvenance();
}
```

Two corrections from the initial draft:

1. **`ResolveProvenance` takes no parameter.** The provider already owns
   the CLI override via `IOptions<GitVersionOptions>`; passing it again
   would duplicate responsibility and let callers diverge.
2. **Dictionary keys are `string`, not `object`.** YAML-keyed dictionaries
   in this codebase always use string keys at runtime; the `object` key
   widens the type unnecessarily and would force runtime casts in the
   executor's lookup path.

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
1. Call `configurationProvider.ResolveProvenance()` to obtain the raw
   dictionaries (no parameter — see correction above).
2. For each `SemanticViolation`, derive the offending field name from
   the violation (rule-specific mapping; SEM-001 → `is-release-branch`,
   SEM-003 → `label`/`regex`, etc.). Look up the source of that
   `(branchName, fieldName)` pair using the explicit precedence chain:

   ```text
   if CLI override has (branchName, fieldName)      → "set by /overrideconfig"
   else if file has (branchName, fieldName)          → "set in your GitVersion.yml"
   else if workflow has (branchName, fieldName)      → "inherited from workflow: <name>"
   else                                              → "from internal defaults"
   ```

   The precedence chain is encoded as a single `LookupFieldSource`
   helper internal to the executor — not inferred at each call site.
3. Emit the provenance string alongside the existing remediation in
   the text/JSON output. Only the final source is shown in v1; "X
   overridden by Y" multi-layer attribution is deferred to a future
   iteration if a consumer demand surfaces.

Three corrections from the initial draft:

3. **Field-level lookup, not branch-level.** Different violations on the
   same branch can have different provenances (e.g. SEM-001 fires
   because `is-release-branch` was inherited from the workflow but
   `regex` was overridden in the user's file). Lookups must take the
   `(branchName, fieldName)` pair.
4. **Precedence is explicit in code.** The chain CLI → file → workflow
   → default is encoded in a single helper rather than inferred at
   each call site. Same chain, same answer, every time.
5. **"Internal defaults" is a fixed string label.** No deep
   introspection of `ConfigurationBuilder`'s defaults; that path would
   accidentally re-create Option A. The default case is a literal
   string the user reads as "this is the engine default for the field."

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

## Resolved questions (post-review)

1. **Does `--explain` imply `/validate`?** No — modifier only.
   Preserves CLI consistency with `-output json` etc. and avoids
   hidden behaviour.
2. **Internal-defaults attribution?** Fixed string label "from
   internal defaults". No introspection of `ConfigurationBuilder`
   defaults (that path would re-create Option A).
3. **`/overrideconfig` shape compatibility?** Validate during 4b
   provider wiring, not pre-blocking. Confirmed correct sequencing.

## Deferred to v2

- **Multi-layer attribution** ("inherited from X, overridden by Y").
  v1 emits only the final source. A v2 enhancement is conceivable if a
  consumer demand surfaces, but is explicitly out of scope here.

## Decision

**Maintainer approved Option C with five required refinements**
(2026-05-16). Refinements applied to this design note above; ready to
proceed to 4a (parser flag).
