# Aetheric Provisioning

A standalone provisioning engine and a Blazor single-page app for combining technology-independent institution requirements with deployment bindings and parent context to plan and provision infrastructure.

The intended flow is **load definition → configure bindings and parent context → plan → review → provision → export configuration**. The engine owns provisioning behavior; the UI is one consumer. Automated callers will be able to use the same engine without interactive prompts.

## Release planning

- [v0.1 release scope and success criteria](docs/v0.1-release.md)
- [Milestones and completion gates](docs/milestones.md)
- [Future roadmap](docs/roadmap.md)

These documents describe planned work, not implemented capabilities. The repository currently contains planning documentation only. Milestones have no committed dates.

## Architectural commitments

- Keep the .NET engine independent of Blazor and HTTP hosting.
- Treat institution definitions as the source of capabilities, resource categories, ownership, and dependencies. Do not bind an Institution or Organization directly to a technology.
- Select technologies through deployment bindings and provider adapters.
- Resolve and validate inherited capabilities against parent context; create only resources owned by the institution being provisioned.
- Separate definition loading, planning, execution, provider adapters, and state/secret storage.
- Generate credentials by default and allow explicit overrides.
- Preserve generated credentials across retries; rotation is a separate operation.
- Keep plans inspectable and execution results usable by both people and automation.
- Start with public Git; leave a clear extension point for authenticated sources.

The first concrete input is ADR Campus’s declarative Decisions Office definition and its separate deployment bindings. See [the input model and source references](docs/input-model.md) for the ownership boundary, known resource requirements, and remaining integration decisions.
