# Failure UX Text Contracts — Grammar-Driven Synthesis

**Status:** Ratified (2026-05-12)  
**Scope:** Q&A Synthesis Protocol failure modes  
**Authority:** These contracts are non-negotiable. Emitted identically in CLI text and JSON output.

---

## Contract Rules

- Deterministic — same input always produces same message
- CI-safe — safe to appear in build logs
- Never blame the user
- Never expose internal rules or engine implementation details
- Never guess or emit partial YAML on failure

---

## F-001 — Increment Authority Ambiguity

**Trigger:** Examples imply version progression but do not reveal what causes it.

**CLI text:**
```
Cannot determine what causes version increments for branch 'develop'.

The examples provided show version changes, but do not specify whether those
changes occur due to:
  • commits on the branch,
  • merges from other branches, or
  • branch name / tag authority.

Please specify the increment source explicitly.
```

**JSON:**
```json
{
  "code": "F-001",
  "branch": "develop",
  "missing": "incrementAuthority",
  "candidates": ["Commits", "Merges", "Authority"],
  "action": "Require explicit selection"
}
```

---

## F-002 — Insufficient Example Signal

**Trigger:** Only one example exists where multiple degrees of freedom remain.

**CLI text:**
```
The provided example for 'feature/*' is valid, but insufficient to infer versioning rules.

At least one additional example or an explicit override is required to determine:
  • label origin
  • increment behavior

No configuration has been generated.
```

**JSON:**
```json
{
  "code": "F-002",
  "branch": "feature/*",
  "reason": "Underdetermined examples",
  "required": ["Additional example or explicit override"]
}
```

---

## F-003 — Conflicting Authority Signals (SEM-001 Surface)

**Trigger:** A branch appears to both define and carry version identity.

**CLI text:**
```
Branch 'release/*' appears to both define the base version and apply a label.

A branch must be either:
  • a version authority, or
  • a label carrier
but not both.

Please revise the examples or provide an explicit override.
```

**JSON:**
```json
{
  "code": "F-003",
  "branch": "release/*",
  "rule": "SEM-001",
  "conflict": ["Authority", "Carrier"]
}
```

---

## F-004 — Grammar Not Recognized (Pre-SEM-010)

**Trigger:** Placeholder or format cannot be validated against known grammar.

**CLI text:**
```
The placeholder '{VersionCoreX}' is not recognized by GitVersion.

Only known variables and supported format specifiers may be used.
No configuration has been generated.
```

**JSON:**
```json
{
  "code": "F-004",
  "placeholder": "{VersionCoreX}",
  "status": "UnknownGrammar"
}
```

---

## Language Discipline

UX text must consistently say:
- ✅ "based on exemplar defaults"
- ❌ "recommended"
- ❌ "standard"
- ❌ "best practice"

Exemplars are dictionaries, not prescriptions.
