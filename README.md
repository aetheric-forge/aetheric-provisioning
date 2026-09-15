# Aetheric Provisioning

A standalone provisioning engine and a Blazor single-page app for combining technology-independent institution requirements with deployment bindings and parent context to plan and provision infrastructure.

The intended flow is **load definition → configure bindings and parent context → plan → review → provision → export configuration**. The engine owns provisioning behavior; the UI is one consumer. Automated callers will be able to use the same engine without interactive prompts.

## Release planning

- [v0.1 release scope and success criteria](docs/v0.1-release.md)
- [Milestones and completion gates](docs/milestones.md)
- [Future roadmap](docs/roadmap.md)

M1 now provides a standalone engine, a Blazor simulation host, and a non-UI harness. Live provisioning and general definition loading remain planned work. Milestones have no committed dates.

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

## Build and run

Requires the .NET 10 SDK (the SDK policy is in `global.json`).

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project samples/Aetheric.Provisioning.Harness --configuration Release --no-build
dotnet run --project src/Aetheric.Provisioning.Web --configuration Release --no-build --urls http://localhost:5180
```

Open `http://localhost:5180` for the Blazor simulation. No provider accounts or credentials are needed. The checkbox injects one failure in the next owned action; run again to retry. After an owned action completes, later runs reuse its checkpoint, so failure injection no longer applies to that completed action.

The harness deliberately fails once, retries, and repeats the completed plan. It exits with code 0 only if the failure is observed, retry succeeds, the synthetic credential is reused, and one simulated resource is created.

## Solution layout

| Project | Responsibility |
| --- | --- |
| `src/Aetheric.Provisioning.Engine` | Host-independent contracts, validation, immutable planning, and execution |
| `src/Aetheric.Provisioning.Simulation` | Pinned Decisions fixture projection, simulated Workbench and parent, in-memory state and secrets |
| `src/Aetheric.Provisioning.Web` | Blazor Interactive Server host for the simulation |
| `samples/Aetheric.Provisioning.Harness` | Non-UI M1 acceptance demonstration; not the future CLI product |
| `tests/Aetheric.Provisioning.Tests` | Planning, ownership, failure/retry, state, cancellation, and secret-reference tests |

See [M1 architecture and validation evidence](docs/m1-foundation.md) for the extension points and limitations. The engine has no YAML, Blazor, hosting, or provider SDK dependencies.
