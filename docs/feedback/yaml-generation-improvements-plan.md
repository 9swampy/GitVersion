# Yaml-generation improvements — plan and tracking

Companion to [`yaml-generation-experience.md`](./yaml-generation-experience.md).
This file tracks the work derived from that writeup that is being implemented
on branch `feat/canonical-gitflow-adr001`.

This file lives on branch `docs/yaml-generation-feedback`; it is not part of
the implementation branch. The implementation branch carries only the code
and test changes themselves.

## Scope filter

Each finding in the experience writeup was classified against two filters:

1. **Implementation locus** — does the fix live in the same code areas as the
   synthesis tooling on `feat/canonical-gitflow-adr001`, or elsewhere?
2. **CJE-V3 §C.3.1a** — is the critique evidence-based with a concrete consumer,
   or a cleanliness preference?

Items below the line "Out of branch scope" are not actioned here; they are
recorded for future capture as separate issues.

## In-scope items (ordered for stacking)

Order favours small, independent commits that can be reviewed or dropped
independently if the upstream PR is later split.

### Stack 1 — `global.json` rollForward

- [x] **GV-IMP-001**: Add `"rollForward": "latestFeature"` to `global.json`. _Landed `62020da56` on `feat/canonical-gitflow-adr001`._

**Why:** the experience writeup hit the exact-pin failure on first invocation
(SDK 10.0.203 not installed; only 10.0.201 present at the time; the system
state now pins 10.0.300 and the same class of failure persists for anyone
without that exact patch). The pin states *intent* (10.0.300 family);
`rollForward` states *tolerance* (same feature band acceptable).

**Why this stack first:** one-line change, no test impact, no behaviour change
on machines that already have 10.0.300. Easy to drop or land independently.

**Scope:** `global.json` only.

### Stack 2 — Document the never-match regex idiom for preset suppression

- [x] **GV-IMP-002**: Add a documented idiom for removing a preset-shipped
  branch family from an effective config, including the matching
  `is-release-branch: false` + `label: ''` consistency requirements that the
  validator enforces. _Landed `e52edc852` on `feat/canonical-gitflow-adr001`._

**Why:** the experience writeup demonstrated that `branchName: null` NREs the
loader and that contributors reach for `(?!x)x` folklore. The loader-NRE fix
is out of scope (see below); documenting the working idiom inside the
already-shipped configuration docs is in scope and additive.

**Why stack 2:** docs-only change to `docs/input/docs/reference/configuration.md`
(or adjacent). Independent of code stacks. Reviewable as a docs PR if split.

**Scope:** `docs/input/docs/reference/*` markdown only.

### Stack 3 — `synthesise` CLI verb

_The original six-sub-item split (003a–f) over-fragmented atomicity: a
parser switch with no executor wiring leaves the binary in a state where
the field exists but does nothing user-visible. Consolidated to three
sub-commits, each one a complete logical step that leaves the build green:_

- [x] **GV-IMP-003a**: Wire `/synthesise` and `/intake` switches through the
  CLI parser into `ConfigurationInfo`. Parser tests (both spellings + value
  capture). `HelpWriterTests` temporarily ignores the new fields — help
  docs land in 003c. _Landed `98af3b964` on `feat/canonical-gitflow-adr001`._
- [x] **GV-IMP-003b**: Wire `GitVersionExecutor.RunSynthesis` reading the
  intake JSON, invoking `DetectionOnlySynthesis` + `SemanticMapper` +
  `YamlEmitter`, emitting YAML or `{ yaml, diagnostics }` with exit code
  0/1. Add in-process integration tests (class-per-scenario,
  binary-falsifiable predicates) for clean intake, ambiguity diagnostic
  (F-001..F-005), and JSON output shape. _Landed `f5f63acea` on
  `feat/canonical-gitflow-adr001`; 22 new scenario tests pass._
- [x] **GV-IMP-003c**: Document the CLI surface in
  `docs/input/docs/usage/cli/arguments.md`, add the `HelpWriterTests`
  lookup entries (removing the temporary `ignored` entries from 003a),
  and add subprocess wire tests (golden path + ambiguous-intake exit 1).
  _Landed `2bec2c488` on `feat/canonical-gitflow-adr001`. Also fixed a
  parser-extensions bug discovered by the subprocess tests:
  `synthesise`/`synthesize` were missing from the
  `ArgumentRequiresValue` boolean-arguments list, causing the parser to
  greedily consume `/intake` as the value of `/synthesise`. The
  in-process tests passed because they constructed `GitVersionOptions`
  directly, bypassing the parser. App.Tests at 274/274._

**Why:** headline ask from the experience writeup. The synthesis library on
`feat/canonical-gitflow-adr001` is currently in-process C# with no public
entry point — its consumer (the agent / CI user wanting declarative-intent
config) is concrete and demonstrated.

**Why stack 3:** non-trivial scope; split into three commits so each can be
reviewed as an atomic unit and the stack as a whole can be cherry-picked or
dropped without disturbing Stacks 1 or 2.

**Scope:** `src/GitVersion.App/{Arguments,ArgumentParser,GitVersionExecutor}.cs`,
`src/GitVersion.Core/Options/ConfigurationInfo.cs`,
`src/GitVersion.App.Tests/SynthesiseCommand*.cs`,
`docs/input/docs/usage/cli/arguments.md`.

### Stack 4 — `/validate --explain` (deferred decision)

- [ ] **GV-IMP-004**: TBC — pending design pass. The experience writeup asked
  for `/validate --explain` to show *which preset fields* pulled a branch
  into a contradictory state. Implementing this requires correlating the
  resolved configuration against the preset's contributions, which is a
  larger surface than the current validator's `IGitVersionConfiguration`
  input. Decision: classify and stage in a separate plan once Stack 3 is
  landed.

**Why stack 4 last:** scope unclear; risks bloating the PR if pulled in
without a clean abstraction.

## Out of branch scope

Recorded here for capture as separate upstream issues, not actioned on this
branch:

- **Loader NRE on `branchName: null`** — pre-existing GitVersion bug. The
  experience writeup's repro is direct; the fix lives in the loader, which
  is unrelated to the synthesis pipeline. File against upstream when the
  branch lands.
- **`new-cli/`'s `config init` stub** — pre-existing stub. Either implement
  it (large) or remove it (smaller). Either way, scope sits in `new-cli/`,
  not in this branch's surface area.
- **`gitversion --describe` AI-first manifest** — out of scope. Worth
  considering as a separate feature.
- **`gitversion lint` (style/clarity findings beyond correctness)** —
  out of scope. The current validator covers correctness; lint is a
  separate axis.

## Process

- Each stack lands as one or more atomic commits on
  `feat/canonical-gitflow-adr001`.
- Commit history is squashed-and-cleaned at each stack boundary so the
  final history is reviewable. Backup branches are created before any
  history rewrite (`backup/<short-name>-<unix-ts>`).
- Pushes go to the `fork` remote only (`9swampy/GitVersion`); never to
  `origin` (upstream `GitTools/GitVersion`).
- Phase 0 semantic observations encountered during analysis or implementation
  are appended to `.ai/observations/yaml-generation-2026-05-15.jsonl` on this
  branch (`docs/yaml-generation-feedback`), as JSONL records per the Phase 0
  protocol. The observation log is non-prescriptive — it records facts and
  gaps, not fixes.
- This file is updated to reflect status (`[ ]` → `[x]`) as items land on
  the implementation branch.
