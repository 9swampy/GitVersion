# Generating `GitVersion.yml` — experience writeup

Context: the instruction was "use the tooling on `/git/gitversion` (currently on
`feat/canonical-gitflow-adr001`) to build a trunk-based `gitversion.yml` with
only master, feature, and bugfix branches" (the user later misremembered this as
"hotfix" — see the *Friction* section below; the original instruction said
*bugfix*).

This document records what I actually did, what worked, what didn't, and what
would have made it trivial. It's feedback aimed primarily at the GitVersion
tooling authors, with a secondary audience of the next person who has to do
this in the ps-ssh repo.

---

## TL;DR

| Aspect | Reality |
|---|---|
| What I used | Legacy CLI: `/showconfig` and `/validate` |
| What I should have used | Same — those are the right tools for this job |
| What I expected to exist | `config init --workflow TrunkBased --branches master,feature,bugfix` |
| What broke | SDK version pin in `global.json` (10.0.203 not installed) |
| Validator output | Caught two real semantic defects (SEM-001, SEM-003) on first attempt |
| Total time | ~10 minutes; ~6 of those were SDK plumbing and dead-end exploration |

---

## What I did, in order

1. **Looked for a `config init` command in `new-cli/`.** Found
   `ConfigInitCommand.cs` — a stub that prints log lines and returns. No
   functionality. Dead end.

2. **Tried `dotnet run --project new-cli/GitVersion.Cli`.** Failed: `global.json`
   pins SDK `10.0.203`, only `10.0.201` was on the box. I worked around by
   editing `global.json` to `10.0.201` and building the *legacy* CLI from
   `src/GitVersion.App`. This worked.

3. **Read `TrunkBasedConfigurationBuilder.cs`** to see the canonical
   trunk-based defaults. This gave me the right mental model: trunk-based
   preset ships `main` + `feature` + `hotfix` + `pull-request` + `unknown`.
   For "only master, feature, bugfix" I'd need to: narrow `main`'s regex,
   keep `feature`, add a new `bugfix` family, and disable the other three.

4. **Ran the legacy CLI's `/showconfig`** against an empty repo with
   `workflow: TrunkBased/preview1` — got the fully-expanded effective config.
   This is the single most useful artifact in the whole exercise: it shows
   exactly what fields the preset sets, so my delta YAML can be minimal.

5. **Wrote `GitVersion.yml` by delta.** Set `workflow: TrunkBased/preview1`,
   overrode `main.regex` to `^master$`, added an explicit `bugfix:` block,
   and tried to suppress `hotfix`/`pull-request`/`unknown` with
   `branchName: null`.

6. **Null suppression NPE'd the loader.** `dotnet ... /showconfig` raised
   `Object reference not set to an instance of an object.` Switched to the
   unsatisfiable-regex trick `(?!x)x` — i.e. keep the branch but make
   it unreachable. Loader was happy.

7. **Ran `/validate`.** Got two errors: SEM-001 (Authority/Carrier
   Exclusivity) and SEM-003 (Variable Capture Contract), both against
   `hotfix`. The validator correctly observed that `hotfix` had inherited
   `is-release-branch: true` and `label: "{BranchName}"` from the preset, but
   my unsatisfiable regex captured no `BranchName` group and contained no
   version pattern — internally inconsistent.

8. **Fixed by setting `is-release-branch: false` and `label: ''` on
   `hotfix`,** and `label: ''` on `pull-request` for the same reason
   (`{Number}` template, no `Number` capture). Re-ran `/validate`: clean.

9. **Sanity-checked against the live repo.** `dotnet ... ` on
   `feature/DeborahWolCheck` produced `0.10.0-DeborahWolCheck.2`. Matches
   trunk-based expectation (Minor bump for `feature/*`, branch name as label).

---

## What worked

- **`/showconfig` with `workflow: TrunkBased/preview1` declared in a stub
  GitVersion.yml.** This is the canonical way to discover the preset's
  shape. Far better than reading `TrunkBasedConfigurationBuilder.cs` because
  it shows the *resolved* shape, including how multi-source inheritance
  collapses to final values.

- **`/validate`.** Excellent. Caught real defects, JSON output is
  machine-readable, the diagnostic messages name both the rule ID and a
  concrete remediation. The `(?!x)x` trick I reached for would have been a
  ticking time bomb without it — practically inert today, but a future
  reader (or a future linter) would see a contradictory declaration. The
  validator forced me to make my intent explicit.

- **The TrunkBased preset itself.** Minimal, correctly opinionated. The
  delta I had to write was tiny.

## What didn't work

- **`new-cli/GitVersion.Cli` was unrunnable** on a box that didn't already
  have SDK 10.0.203 installed. `global.json`'s `rollForward` isn't set —
  even a `10.0.201` SDK was rejected. I patched `global.json` locally to
  unblock; that's invasive and would be unacceptable in a real workflow.

- **`new-cli/`'s `config init` is a stub.** If I'd believed the CLI
  surface, I'd have concluded the tool can't do this. The legacy CLI was
  the right answer.

- **`branchName: null` to suppress a preset branch raised an NRE in the
  loader.** No diagnostic, no error message — just `Object reference not set
  to an instance of an object.` This is the single worst experience in the
  whole exercise: a natural way to express "remove this preset entry"
  silently crashes. (See *What I wish existed* below.)

- **Synthesis tooling has no public entry point.**
  `src/GitVersion.Configuration/Synthesis/` (the headline addition of the
  `feat/canonical-gitflow-adr001` branch) is purely in-process C#. No CLI
  verb, no JSON intake, no way to invoke it without hosting it yourself.
  Its tests construct `(BranchPattern, VersionExample)[]` inline. For a user
  who wants to *use* the synthesis pipeline to generate a config from
  declared intent, there's no path.

- **No reverse synthesis.** I asked: given an existing `GitVersion.yml`, can
  the tooling extract the `(branch, version-example)` intake that would
  re-synthesise it? Answer: no. Forward-only. That's a reasonable design
  decision, but it means the synthesis pipeline can't be used to *validate*
  or *canonicalise* an existing config — only to generate a fresh one.

## What I used

- `git log` / file reads on `/git/gitversion` to map the project structure.
- `cat /git/gitversion/global.json` → discovered the SDK pin.
- `dotnet build src/GitVersion.App/GitVersion.App.csproj -c Release` →
  bootstrapped the legacy CLI binary.
- Stub repo at `/tmp/gv-trunk` with a one-line `GitVersion.yml` → invoked
  `/showconfig` to see the resolved preset.
- `/showconfig` again after each edit to confirm the loader still accepted
  the file.
- `/validate` to catch semantic defects.
- Live `gitversion` run on `feature/DeborahWolCheck` to confirm the version
  shape on a real branch.

## What I should have used (but couldn't)

- **A `config init --workflow TrunkBased --branches master,feature,bugfix`
  command** that wrote a minimal `GitVersion.yml` directly. The stub
  exists; the work doesn't.

- **A `config suppress hotfix pull-request unknown` switch** or a
  documented "remove a preset branch" idiom. The `(?!x)x` workaround
  works but is folklore.

- **The synthesis pipeline via CLI.** A verb like
  `gitversion synthesise --intake intake.json --out GitVersion.yml` would
  have let me declare `(master, 1.0.0)`, `(feature/X, 1.1.0-X.1)`,
  `(bugfix/Y, 1.0.1-Y.1)` and have the tooling figure out the YAML. That
  is in fact exactly what the synthesis library does — it just isn't
  exposed.

## What I wish I'd known

These are the things that would have collapsed the task to ~2 minutes:

1. **`branchName: null` does NOT suppress a preset branch.** It NREs the
   loader. Use a never-matching regex like `(?!x)x` and pair it with
   `is-release-branch: false` + `label: ''` to keep the declaration
   internally consistent. Document this idiom in
   `docs/input/docs/reference/configuration.md`.

2. **The validator's rule catalogue is workflow-agnostic.** It will
   complain about contradictions even on unreachable branches. This is
   correct behaviour but surprising — I expected unreachable code to be
   silently fine. The validator's perspective is that the declaration is
   the contract, regardless of reachability.

3. **`/showconfig` with a workflow declaration is the discovery tool.** Not
   `TrunkBasedConfigurationBuilder.cs`. Not the docs. Not the schema. Use
   the CLI's own resolver and read its output.

4. **The legacy CLI is the production tool.** The new CLI is interesting
   architecture but unusable for this task. The "what should I run?"
   answer is `dotnet src/GitVersion.App/bin/.../gitversion.dll`.

5. **SDK rollForward.** If `global.json` had `"rollForward": "latestMajor"`
   or `"latestFeature"`, none of the SDK plumbing would have happened.
   Recommend that be added.

## Friction the user introduced

The user said "trunkbased with only master feature and hotfix branches iirc"
when asking for this writeup. The original instruction (one message earlier
in the same session) said "master feature and bugfix branches". The
distinction matters:

- `hotfix` is in the trunk-based preset already, with `is-release-branch:
  true` — it's a version-authority family.
- `bugfix` is *not* in the preset and had to be added as a label-carrier
  family.

Had the original instruction said "hotfix", the config would have been
*shorter* — just keep `hotfix`, disable `pull-request` and `unknown`. No
`bugfix:` block needed. This is a good example of why intake-driven
synthesis would help: declaring "(bugfix/Y, 1.0.1-Y.1)" as intent unambiguously
selects label-carrier; declaring "(hotfix/Y, 1.0.1)" without a label segment
unambiguously selects version-authority. YAML-by-hand requires the author
to know these conventions already.

---

## Recommendations to the GitVersion authors

1. **Make `new-cli/`'s `config init` real,** or remove the stub. A stub
   command in the CLI surface is worse than no command at all — it implies
   capability that doesn't exist.

2. **Expose synthesis as a CLI verb.** Even minimally — accept a JSON file
   of `[{branch: "...", example: "..."}]`, output YAML. The library is
   already injective-mapping clean; surfacing it would let users opt into
   declarative-intent configuration.

3. **Fix `branch: null` suppression** — either make it work or raise a
   readable diagnostic. Don't NRE.

4. **Set `rollForward` in `global.json`** so contributors aren't blocked by
   a 10.0.201-vs-10.0.203 pin.

5. **Document the never-match regex idiom** for preset suppression, with
   the matching `is-release-branch: false` / `label: ''` requirement.

6. **Have `/validate` accept `--explain`** to show *which* preset fields
   pulled the branch into a contradictory state. SEM-001 against `hotfix`
   was clear once I knew the preset, but a first-time user wouldn't know
   `hotfix` inherits `is-release-branch: true`.

---

## AI-first considerations

I am an LLM agent. Some of the friction above is general; some is
LLM-specific. Making GitVersion AI-friendly is largely about closing the
gap between *intent* and *artifact*.

### Capability discovery
Today I had to read source files to discover that `TrunkBased/preview1`
is a valid workflow string. An AI-first tool exposes
`gitversion --describe` returning a JSON manifest: verbs, flags, presets,
workflow names, rule IDs. One call replaces a dozen greps.

### Stubs flagged as stubs
`new-cli/`'s `config init` is a stub. I read its source to find out.
Stubs should self-identify (`"status": "not-implemented"` in their
`--help` JSON), or not ship.

### No implicit git repo
`/showconfig` wanted a repo. I `git init`'d a tmp dir to use it. An
AI-first verb takes `--config <path> --no-repo` and works from any cwd.

### Structured diagnostics for *all* errors
`/validate` JSON output is exemplary. `branch: null` NREs raw — no rule
ID, no remediation. Every error path should emit the same shape.

### Round-trip introspection
LLMs benefit from being able to ask "what does this *mean*?" and get
structured derived state back. `/showconfig` returns YAML; an AI-first
variant returns JSON with derived role per branch family, classified
topology, computed increment, etc. The synthesis library has all this
(`SynthesisConfig`); the CLI doesn't expose it.

### Forward-from-intent verb
Highest-leverage AI-first feature: a `synthesise` verb taking declared
intent and emitting validated YAML.

```bash
gitversion synthesise --json <<'JSON'
{
  "topology": "TrunkBased",
  "primaryBranch": "master",
  "branches": [
    {"pattern": "master",    "example": "1.0.0"},
    {"pattern": "feature/X", "example": "1.1.0-X.1"},
    {"pattern": "bugfix/Y",  "example": "1.0.1-Y.1"}
  ]
}
JSON
```

stdout: `{"yaml": "...", "diagnostics": [...]}`. The agent declares
intent in a tiny structured shape; the tool produces the canonical YAML
or explicit diagnostics. No preset knowledge required, no `(?!x)x`
folklore, no stub-repo dance, no hallucinated knobs. The library
underneath is already there.

### Lint-then-apply
After generating a config the agent wants to ask: "is this minimal?"
`/validate` is correctness-only. A `gitversion lint --json` returning
style/clarity findings ("this override matches the preset default —
remove it") closes the loop to a minimal config, not just a valid one.

### SDK rollForward
`global.json` pinned to an exact patch with no rollForward broke me on
first invocation. One line (`"rollForward": "latestFeature"`) removes
the failure mode for any agent dropped into a fresh environment.

### The pattern
Every AI-first item above shares one shape: **the artifact the agent
produces should be the output of a tool, not a guess.** Hand-writing
YAML against an under-documented inheritance model is exactly what LLMs
hallucinate around. Forward synthesis with structured intent → structured
output is exactly what LLMs compose well. The synthesis library is
already that shape internally — exposing it would make GitVersion
meaningfully AI-first with a small amount of boundary code.
