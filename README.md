# Aetheric Provisioning

A standalone provisioning engine and a Blazor single-page app for turning an institution definition into the infrastructure it requires.

The intended flow is **load → plan → configure → review → provision → export configuration**. The engine owns provisioning behavior; the UI is one consumer. Automated callers will be able to use the same engine without interactive prompts.

## Release planning

- [v0.1 release scope and success criteria](docs/v0.1-release.md)
- [Milestones and completion gates](docs/milestones.md)
- [Future roadmap](docs/roadmap.md)

These documents describe planned work, not implemented capabilities. The repository currently contains planning documentation only. Milestones have no committed dates.

## Architectural commitments

- Keep the .NET engine independent of Blazor and HTTP hosting.
- Treat institution definitions as the source of resource requirements.
- Separate definition loading, planning, execution, provider adapters, and state/secret storage.
- Generate credentials by default and allow explicit overrides.
- Preserve generated credentials across retries; rotation is a separate operation.
- Keep plans inspectable and execution results usable by both people and automation.
- Start with public Git; leave a clear extension point for authenticated sources.

The concrete institution and its provisioning contract are still in development in `aetheric-runtime`. The loader format and exact provider operations will be agreed against that contract before live provisioning is implemented.
