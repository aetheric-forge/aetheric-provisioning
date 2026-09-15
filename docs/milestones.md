# Milestones

Status: planned. No completion dates are committed. Each milestone closes when its evidence is recorded, not merely when code has been written.

## M0 — Scope and contract agreement

Deliverables:

- v0.1 scope, acceptance criteria, and future roadmap.
- A concrete institution fixture from `aetheric-runtime` and an agreed way to obtain its resource requirements.
- Decisions on definition trust/loading, provider targets, ownership, secret storage, and configuration output.

Exit gate: the first institution's complete resource list can be explained and mapped to supported provider operations. Remaining unknowns do not require inventing the institution contract.

Current state: planning documents drafted; the concrete institution contract is an external dependency.

## M1 — Engine and repository foundation

Deliverables:

- .NET solution with a standalone engine, provider adapter boundaries, Blazor host, and tests.
- Contracts for source provenance, validation, plans, progress, outcomes, state, and secret references.
- A simulated provider and a provisional example for local development.
- Build/test commands and initial CI.

Exit gate: a non-UI test host can plan and execute the example, observe failures, and retry. Engine dependencies contain no Blazor components or interactive prompts.

Dependencies: architectural scope from M0. The simulation may proceed while the concrete institution is pending; it must not be treated as the final runtime integration.

## M2 — Public Git loading and plan review

Deliverables:

- Public Git source loading with commit pinning and definition validation.
- Mapping from the agreed runtime contract to the engine's plan.
- Blazor source/configuration/review workflow, including defaults and overrides.
- Actionable missing-input, unsupported-resource, and invalid-definition results.

Exit gate: SC-01, SC-02, and SC-03 pass against the actual institution. Reviewing a plan does not mutate provider infrastructure.

Dependencies: M1 and the completed contract/trust decisions from M0.

## M3 — Real provisioning and credential handling

Deliverables:

- MongoDB, S3, and Keycloak adapters for the first institution's required operations.
- Existing-resource inspection and conflict handling.
- Secure credential generation, explicit overrides, and protected secret persistence.
- Live per-resource progress and results in the SPA.

Exit gate: SC-04, SC-05, and SC-08 pass against documented provider targets. A simulated run cannot satisfy this gate.

Dependencies: M2 and test infrastructure with suitable operator credentials.

## M4 — Resume and usable outputs

Deliverables:

- Durable run state, duplicate-run protection, cancellation, and restart/resume behavior.
- Retry behavior that preserves resource identity and credentials.
- Runtime configuration output validated with the actual institution.

Exit gate: SC-06, SC-07, SC-09, SC-10, and SC-11 pass. Record at least one partial-failure/restart/resume demonstration using real providers.

Dependencies: M3. State and secret abstractions are designed in M1; persistence should be implemented alongside M3 where required for credential handling.

## M5 — v0.1 release verification

Deliverables:

- Operator setup guide, supported provider matrix, permissions requirements, troubleshooting, and known limitations.
- Clean-checkout build/test verification and recorded integration evidence.
- Release notes and a versioned release candidate.

Exit gate: all SC-01 through SC-12 pass; the concrete institution can use the resulting configuration. Tag v0.1 only after the evidence is reviewed.

Dependencies: M4.

## Tracking convention

When implementation begins, link issues or pull requests to a milestone and applicable success-criteria IDs. Record evidence alongside each completed gate, including the institution commit and provider versions. Update this document when scope changes; do not silently redefine a gate to match partial implementation.
