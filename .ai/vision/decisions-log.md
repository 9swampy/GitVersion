# Decisions Log

Captures key design decisions, corrections, and intellectual pivots made during implementation sessions.  
Each entry records what was decided, why, and what it replaced or superseded.

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

## DEC-008: SocratiCode Indexing as Enabling Infrastructure (2026-05-12)

**Decision:** SocratiCode semantic indexing of /git/gitversion is being pursued to enable semantic search across the codebase for validator implementation.

**Status:** In progress — intermittent failures due to Ollama connection timeouts. Approximately 33% indexed at time of logging. Not blocking current work (fixture and catalogue work can proceed without it).
