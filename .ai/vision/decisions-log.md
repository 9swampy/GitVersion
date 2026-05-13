# Decisions Log

Captures key design decisions, corrections, and intellectual pivots made during implementation sessions.  
Each entry records what was decided, why, and what it replaced or superseded.

---

## DEC-018: Emission Key Space Must Be Injective With Respect to Branch Families (2026-05-13)

**Invariant (new, non-negotiable):**

> For every set of `SynthesisBranchConfig` records produced by `SemanticMapper`, the family keys derived via `BranchFamilyKey.Derive` MUST be pairwise distinct.

**Why:** GitVersion's `branches:` YAML is a *map* keyed by family (`feature`, `release`, `develop`, ...), not by branch instance. Two examples that collapse to the same family key cannot coexist in valid output — at best you get invalid YAML; at worst, YamlDotNet's last-write-wins parsing silently drops one of the configurations.

**Layering — A is authoritative, B is invariant enforcement:**

| Layer | Role | Output | Classification |
|---|---|---|---|
| A — `AmbiguityDetector` F-005 | User-facing validation; rejects malformed intake before mapping | Diagnostic (blocking), `IsSuccessful=false` | User error |
| B — `YamlEmitter` guard | System invariant protection; only triggers if A is bypassed or regresses | `InvalidOperationException` with "synthesis invariants" framing | Internal failure |

The emitter MUST NOT recover — no merging, no deduplication, no last-write-wins, no silent override. Synthesis aborts loudly because the validator-as-oracle separation depends on it.

**Orthogonality with §B.9a:**

§B.9a injectivity is about **inference-to-rule mapping** — each `VersionExampleInference` produces exactly one `SynthesisBranchConfig`. DEC-018 is about **emission-key uniqueness** — across the *set* of `SynthesisBranchConfig` records, family keys must not collide. The two invariants are orthogonal; both must hold.

**Mechanism — single source of truth:**

`BranchFamilyKey.Derive(branchPattern)` is the canonical family-key function. Both `AmbiguityDetector.CheckBranchFamiliesAreUnique` and `YamlEmitter.EnsureUniqueFamilyKeys` consult it. Divergence between detector and emitter would let one pattern through while colliding on the other — locating the rule in one place prevents this drift.

**Framework note (§C.3.1a):** Classification of the original critique was DEFECT (objective: emitter produces invalid YAML for an unrejected intake shape). Disposition was FIX (A + B) — both because the defect is real (silent corruption, not aesthetic) and because the proper layer is upstream of the emitter (where the reviewer originally pointed). The reviewer's two proposed solutions — "emit per-family" and "generate unique keys" — were each rejected: the first violates §B.9a injectivity; the second violates the consumer reality of GitVersion's per-family YAML map.

---

## DEC-017: IncrementSource Retained as Intake-Captured Scaffold (2026-05-13)

**Decision:** `IncrementSource` is classified as a **dead field** in the current synthesis pipeline (no upstream reader, no downstream emitter), with disposition **RETAIN** on the basis of documented intent.

**Evidence (consumer-evidence audit under CJE-V3 §C.3.1):**
- Downstream: `YamlEmitter` contains zero references to `IncrementSource`; emitted YAML never carries the value.
- Upstream: `SemanticMapper.Map(detection, incrementSource)` stores the parameter on `SynthesisConfig` and otherwise ignores it. Strategy selection keys off `Topology.Kind`, not `IncrementSource`.

**Documented intent (bounded search of `.ai/handoffs/`):**
- `GV-SEM-VAL-appendix-b.md` Layer 2 names "increment sources" as one of four semantic axes synthesis derives.
- `GV-SEM-VAL-appendix-b.md` Layer 3 explicit-overrides lists "Increment source is commits, not merges" as a structured override.
- `GV-SEM-VAL-appendix-b.md` §B.9 Risk-1 prescribes F-002 when fewer than two signals support an inferred increment source.
- `FAILURE-UX-CONTRACTS.md` F-001/F-002 ask the user to specify increment source explicitly, with candidate values mirroring the four `IncrementSource` enum members.

**Action:** XML doc on `SynthesisConfig.IncrementSource` and `SemanticMapper.Map`'s `incrementSource` parameter rewritten to record the intentional intake-vs-emission asymmetry, so the field cannot be mistaken for dead state by future audits.

**Framework note (§C.3.1a — classification vs disposition):** Classification is evidence-based and definite (DEAD). Disposition is intent-aware and decided per session (RETAIN, pending emission iteration). Future revisits should re-run the consumer-evidence audit and update only the disposition.

**Why:** Prevents two failure modes — (1) silent retention of inert API surface under "might be used later," and (2) premature removal of intentional scaffolding that breaks an in-flight design contract.

---

## DEC-016: Advisory Rule Contract ARC-001 — Structural Constraints on Advisory Text (2026-05-13)

**An advisory MUST:**
1. State validity explicitly: "This configuration is valid."
2. Avoid normative language: no "should", "recommended", "best practice"
3. Describe alternative behaviour, not preference: "Some workflows also..." only where it explains engine behaviour
4. Not reference any workflow (GitFlow, TrunkBased) as authority
5. Only exist where it adds interpretive clarity about engine behaviour — not to acknowledge workflow diversity

**An advisory MUST NOT:**
- Be implemented solely to acknowledge that alternative workflows exist
- Imply that a change is recommended
- Encode workflow ideology under the guise of observation

**Rule admission classification refined:**

| Situation | Action |
|---|---|
| Violates semantic invariant | Error |
| Creates risk or ambiguity in version output | Warning |
| Explains surprising but legal engine behaviour | Advisory |
| Prevents misinterpretation of engine output | Advisory |
| Only acknowledges alternative workflows | No rule |
| Purely workflow or style choice | No rule |

**Why:** Every additional rule increases cognitive load and risks policy creep. Absence of a rule is a deliberate feature, not an omission.

---

## DEC-015: Counterfactual Validity Test — Admission Gate for All Rules (2026-05-13)

**The test (mandatory before any rule is admitted):**
> If the user explicitly intended this configuration, would the system still consider it valid?

- **YES** → rule cannot be Error or Warning; at most Advisory (if it explains engine behaviour); preferably no rule at all
- **NO** → rule may be Error or Warning

**Applied to the bugfix narrowing case:**
`bugfix.source-branches: [develop]` — could a user intend this? Yes (confirmed by in-production usage). Therefore: no rule. Not even an advisory — it would only acknowledge an alternative workflow, not explain engine behaviour.

**Why:** Without this test, rules drift into policy enforcement. The counterfactual makes the physics/policy boundary operational rather than philosophical.

**How to apply:** Before writing any new rule, answer the counterfactual. If YES, document the explicit decision NOT to add a rule. Absence of a rule is a conscious decision that belongs in the catalogue.

---

## DEC-014: Step 2 Tightened Invariants — Synthesis Cannot Create Intent (2026-05-12)

**The primary invariant:**
> Synthesis is not allowed to create intent; it may only encode intent that already exists and has been proven sufficient.

**Three implementation constraints (non-negotiable):**

1. **Injective mapping:** Each `VersionExampleInference` maps to exactly one branch rule. Partial population of multiple rules → internal failure, not user error.

2. **Empty section capability:** The YAML emitter must be capable of omitting any section the mapper doesn't explicitly populate. Canonical YAML constants are scaffolding, not policy. Exemplars must not silently become defaults.

3. **Explicit fields only:** Only fields explicitly set by SemanticMapping may differ from GitVersion engine defaults. Anything else must be omitted. Prevents silent default leakage from canonical templates.

**Three risks requiring explicit guards:**

- **Risk A (Default leakage):** Fields like `increment: Patch` from canonical YAML that were not explicitly chosen by the mapper must not appear in synthesis output.

- **Risk B (Single-example sequencing):** A single `release/1.62.0 → 1.62.0-beta1244` example appears complete but doesn't confirm sequencing source. F-001 must still fire until sequencing is explicit.

- **Risk C (Grammar evolution):** Synthesis recognises grammar tentatively until SEM-010 is ratified. Validator remains authoritative on grammar.

**Why:** These constraints were identified by technical review after Step 1 completion. Without them, synthesis could silently inherit defaults from exemplar templates, creating configuration that "works" but doesn't reflect the user's stated intent.

---

## DEC-013: Grammar-Driven Synthesis — Example-First, Not Schema-First (2026-05-12)

**Decision:** The Q&A Synthesis Protocol uses output examples (what versions look like) as
primary input, not a JSON schema mirroring YAML fields. The JSON schema is a derived artifact,
not the intake form.

**What it replaced:** An earlier proposal where users filled in a JSON schema with fields like
`role`, `label`, `increment`, `mode` — isomorphic to YAML but in JSON. User feedback: "that
doesn't make configuration that much easier."

**The correct intake:**
1. Branch naming conventions with variables: `feature/Branch`, `release/1.2.3`
2. Desired version output examples: `develop → 1.62.0-alpha{N}`
3. One forced-choice: how does the version number advance?

**Derivation:** From examples alone, the system infers label, deployment mode, authority/carrier
role, and capture group requirements. The grammar dictionary is extractable from the GitVersion
codebase (`GitVersionVariables.AvailableVariables` + formatter chain).

**Sparse-to-full expansion:** Sparse input (just branch names) → topology detection →
exemplar defaults infilled → full JSON → YAML → `/validate` confirms.

**Why:** Reduces cognitive load to near-zero for standard workflows. Users describe what they
see (version strings), not how GitVersion computes them.

**Implementation gate:** Blocked on SEM-010 ratification. Detection-only first (no YAML emission
until ambiguity handling is proven).

---

## DEC-001: Semantic Contract Before Validator (2026-05-12)

**Decision:** The semantic rule catalogue must exist and be stable before any validator code is written.

**What it replaced:** The impulse to jump from gap analysis directly to `ConfigurationSemanticValidator` implementation, treating ADR-001 COLE ACs as validator rules.

**Why:** COLE ACs and Phase0 observations are negative examples, not rules. Rules must be workflow-agnostic invariants phrased as violated semantics. Without the catalogue, a validator built on COLE ACs would encode GitFlow assumptions and fail to generalise to TrunkBased, release-train, or any other workflow.

**Consequence:** Any implementation that names a GitFlow workflow in a rule ID or rule statement is wrong. Rules reference the semantic model only.

---

## DEC-002: ADR-001 Is Exemplar #1, Not the Model (2026-05-12)

**Decision:** ADR-001 (GitFlow canonical configuration) is explicitly declared as Exemplar #1 — an instantiation of the semantic model, not the source of the model.

**What it replaced:** The framing where ADR-001 was the root of the validation hierarchy, with rules derived from its COLE ACs.

**Why:** Treating ADR-001 as normative would make the validator a GitFlow compliance checker. The intended product is a semantic contract that any workflow can instantiate. GitFlow tripping a rule is evidence the rule is correct; it is not the definition of the rule.

**Consequence:** Rule IDs use `SEM-NNN` not `GITVER-SNNN`. Rule statements mention no workflows by name.

---

## DEC-003: The Physics vs Policy Distinction (2026-05-12)

**Decision:** GitVersion semantics are the "physics" (invariants that cannot be violated without breaking version calculation). Workflow configuration (GitFlow, TrunkBased) is the "policy" (choices within the physics).

**Why:** This framing, identified by the user during the semantic rule catalogue co-authoring session, is the conceptual basis for the distinction between the semantic model and its instantiations. It also explains why validation should be at the physics level — policies differ, physics does not.

**Canonical formulation (user-stated):**
> "You have successfully decoupled the Workflow (the policy) from the Semantics (the physics)."

**Consequence:** The validator is a physics enforcer, not a policy prescriber. It says "this configuration cannot work correctly" not "this configuration is not GitFlow."

---

## DEC-004: Version Authority vs Label Carrier as Core Taxonomy (2026-05-12)

**Decision:** The primary conceptual distinction in the semantic model is between version authorities and label carriers.

**Definitions (co-authored with user):**
- **Version Authority:** A branch whose NextVersion is derived from an external artifact — a Tag or the Branch Name itself. The branch does not calculate an increment.
- **Label Carrier:** A branch whose NextVersion is derived from a parent branch. The branch contributes only a metadata string (the label).

**Why:** This taxonomy maps directly to GitVersion's internal calculation engine. The authority/carrier distinction determines which version strategy applies and whether the result is deterministic. It is the conceptual foundation for SEM-RULE-001 and is workflow-agnostic.

**Consequence:** SEM-RULE-001 is the "crown jewel" rule. Its violation check uses `increment` as the discriminator: an authority branch that specifies a non-None increment is making a contradictory claim.

---

## DEC-005: Validator Is a Formal Verification Tool, Not a Linter (2026-05-12)

**Decision:** The ConfigurationSemanticValidator is positioned as a formal verification tool — it checks whether a configuration satisfies the semantic model — not as a style linter.

**Why (user-stated):**
> "By treating GitFlow as an instance of these rules rather than the source of them, you have moved the project from a linter to a formal verification tool."

**Consequence:** Rules must be proven against exemplar test fixtures before they are implemented. A rule with no fixture evidence is not yet ratified. The exemplar tests (CanonicalGitFlowScenarios, CanonicalTrunkBasedScenarios) are the ground truth against which rules are calibrated.

---

## DEC-006: Upstream Intent for GitVersion Project (2026-05-12)

**Decision:** The semantic contract and validator are intended as upstream contributions to the GitVersion project, not as PRIMS-specific tooling.

**Rationale:** GitVersion's JSON schema validates structure; nothing validates semantic coherence. Every GitVersion user whose config silently misbehaved is the intended beneficiary. The PRIMS estate problem is the motivating use case, not the scope.

**Consequence:** Code must meet GitVersion's contribution standards (see CLAUDE.md). The PRIMS estate configs become the test corpus for the negative case suite, but the API surface is generic.

---

## DEC-007: Deferred — PRIMS Estate Config Fixes (2026-05-12)

**Decision:** Fixing the individual PRIMS estate configs (foundation, strata, git-check, prims/.github) is deferred. The correct fix is known (apply ADR-001 canonical YAML) but is not the goal of this work stream.

**Why:** The goal is building the tooling that makes the fix self-evident and verifiable across the entire estate, not manually applying the fix repo by repo. Fixing one repo while the estate has no validator would leave all other repos broken and create a new maintenance burden.

**Status:** Blocked on validator delivery. Unblock condition: validator can be run against any GitVersion.yml and report SEM-RULE violations.

---

## DEC-009: Lint Checklist as Bridge Artifact — Not Code Yet (2026-05-12)

**Decision:** The Lint Checklist is the mandatory bridge artifact between the semantic rule catalogue and validator implementation. Code is not written until the checklist exists and is stable.

**Why:** The checklist proves the rules are human-executable before they are machine-executable. A rule that cannot be applied manually is not yet well-enough defined to be coded. The checklist also serves as the validator's specification — it defines input, output, and remediation for each rule.

**Consequence:** `ConfigurationSemanticValidator` implementation is blocked on checklist ratification. Checklist ratification requires at least two independent exemplars having applied the rules (done: GitFlow trips SEM-001/002/003/004; TrunkBased is clean against all rules).

---

## DEC-010: SEM-004 Is Legitimate, Not Political (2026-05-12)

**Decision:** SEM-004 (Deployment Mode Consistency) is a legitimate semantic rule, not a style preference.

**Why (user-stated reasoning):** `ContinuousDeployment` collapses prerelease identity. Labels assert identity. Combining both without explicit scoping loses information silently. This is the Stability Paradox expressed mechanically. The rule forces users to choose where information disappears — that is honest, not prescriptive.

**Consequence:** SEM-004 is included in the ratified rule set with Warning severity (Error when combined with SEM-001/002 violations).

---

## DEC-011: Validator Public Output Schema (2026-05-12)

**Decision:** The validator's output type must be designed before implementation begins. The Lint Checklist defines it implicitly.

**Schema (agreed):**
```
SemanticViolation {
  RuleId:      string (e.g. "SEM-001")
  Severity:    Error | Warning
  BranchName:  string (which branch triggered the rule)
  Message:     string (human-readable invariant statement)
  Remediation: string (what to change)
}

f(IGitVersionConfiguration) → IReadOnlyList<SemanticViolation>
```

The validator is a pure function over configuration — no GitVersion calculation engine required. This makes it testable at configuration-load time and workflow-agnostic.

---

## DEC-012: Validation Closes Intention; Fixtures Close Expectation (2026-05-12)

**Decision:** TDD fixtures are not required for ordinary users to achieve semantic closure. `/validate` fully closes the "physics" question; fixtures address the "policy" question — optional, for platform teams and organizational contracts only.

**The rule of thumb (canonical, use in documentation and guidance):**
> Validation closes intention. Fixtures close expectation.

**Two distinct closure types:**
- Semantic coherence ("Is my config contradictory?") → `/validate`, mandatory for all users
- Policy correctness ("Does it express what my team actually wants?") → fixtures, optional for platform teams

**Fixture responsibility boundary:** Fixtures are for institutional memory, not individual understanding. ADR-001 exists to *discover and prove* semantics; users *consuming* ADR-001 do not re-prove it. Platform teams write fixtures when config becomes a cross-repo contract or when behavioral guarantees are needed across GitVersion upgrades.

**Consequence:** The getting started guide must lead with this distinction. `/validate` green = well-defined behavior. Fixtures = regression-protected lifecycle assertions. Never conflate the two.

**Why:** Closing this boundary explicitly prevents two failure modes: (1) users feeling inadequate because they didn't write C# tests, (2) the validator growing toward policy prescription to fill perceived gaps.

---

## DEC-008: SocratiCode Indexing as Enabling Infrastructure (2026-05-12)

**Decision:** SocratiCode semantic indexing of /git/gitversion is being pursued to enable semantic search across the codebase for validator implementation.

**Status:** In progress — intermittent failures due to Ollama connection timeouts. Approximately 33% indexed at time of logging. Not blocking current work (fixture and catalogue work can proceed without it).
