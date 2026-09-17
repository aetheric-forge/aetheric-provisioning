# Registry bootstrap foundation

The human sysadmin creates the initial Keycloak principal and credentials. This increment implements the host-independent bootstrap lifecycle only. It does not expose routes, authenticate bootstrap passwords, implement OIDC, or expose live Keycloak operations through the UI. The concrete staff adapter is documented below.

Deployment explicitly initializes a private `FileRegistryBootstrapStore` with the expected HTTPS realm issuer, provisioner client ID, and provisioner admin role. Ordinary application startup must only read that record: missing or corrupt state fails closed. Initialization refuses to replace an existing record. Losing the directory requires deliberate operator recovery, never automatic bootstrap reactivation.

`RegistryBootstrap` requires authenticated bootstrap operator access before selecting an existing principal by immutable subject ID. The directory/staff adapter must check that principal in the configured realm. The selected subject is saved before granting authority; failures cannot redirect the bootstrap grant to another user. Retries must reconcile the same role and assignment through the runtime Registry Clerk. Authority here is provisioner administration, not unrestricted Keycloak administration.

The persisted sequence is `AwaitingPrincipal → PrincipalSelected → AuthorityAssigned → Completed`. Completion requires a host-verified SSO identity matching the exact issuer and selected subject, with the configured admin role. The host must validate the OIDC response, audience, session, and role mapping before constructing that identity; input fields are not evidence of authentication. `IRegistryBootstrapAccess` is a trusted, request-scoped host boundary, not a public request DTO.

Completion is durable and prevents further bootstrap assignments. The future host must also reject the deployment bootstrap login and invalidate bootstrap sessions once state is completed. A matching administrator can safely repeat completion after a lost HTTP response. State operations hold a local cross-process lease and use the existing private atomic file storage. This has the same local-filesystem and power-loss limitations as the run-state store.

`IRegistryBootstrapStaff` is an adapter seam for the runtime identity directory and `IRegistryClerk`; it intentionally has no principal-creation operation. It must ensure role/assignment compatibility and treat existing compatible authority as success, including when a provider operation succeeded before a checkpoint failure. Credentials and tokens never belong in this state file.

## Keycloak staff adapter

`Aetheric.Provisioning.Registry.KeycloakRegistryBootstrapStaff` now implements the bootstrap staff seam using the runtime's actual `IExternalIdentityDirectory` and `IRegistryClerk` contracts and their Keycloak implementations. `external/runtime` is pinned to commit `e07976bf773bf31219f937157d720747be7cec99`. Only the identity provider, models, and supporting abstractions are referenced; no Campus implementation is referenced or deployed.

Deployment must supply an existing, dedicated realm role for provisioner administration, along with the service-account client and its permissions. The adapter binds the client ID and derived realm issuer to the bootstrap settings and only accepts that configured role. Before each assignment attempt, the adapter uses `IRegistryClerk.GetRoleAsync` to verify that the configured role exists and its returned name matches exactly. Missing, mismatched, unauthorized, or failed lookups block assignment with stable, redacted error codes. Role ownership, meaning, and any composites remain deployment responsibilities: the runtime lookup currently projects only the role name, and its empty `Permissions` collection is not evidence of absent privileges. This slice does not create or adopt arbitrary roles. A missing role fails rather than being created. The role must not confer unintended Keycloak administration privileges.

```csharp
using var staff = new KeycloakRegistryBootstrapStaff(settings, keycloakOptions);
var bootstrap = new RegistryBootstrap(settings, stateStore, authenticatedHostAccess, staff);
```

`keycloakOptions.Authority` is the HTTPS server base URL; the adapter derives and compares the exact realm issuer against the deployment record. Client secrets remain protected host configuration. Alternate Admin API endpoints are not supported by this first adapter. The production HTTP client disables redirects and has a 30-second timeout. Raw runtime failure reasons, HTTP response bodies, and transport exceptions are replaced with stable `RegistryBootstrapStaffException.Code` values. Caller cancellation remains cancellation.

Principal lookup is by immutable Keycloak subject ID, not username or email. Missing or disabled identities cannot receive authority; directory outages are errors, not “user missing.” The adapter rechecks the principal immediately before each assignment. It invokes the Clerk's existing-role assignment operation on retry; only success advances bootstrap, and a generic conflict response is not treated as success. No operation creates users, changes passwords, creates roles, or modifies client registrations. Confirmation of the administrator's usable authority still occurs during the verified SSO completion step.

The sysadmin must select a human principal. The current runtime identity projection does not expose Keycloak's service-account linkage, so this adapter cannot independently distinguish a service-account user from a human user; the SSO completion requirement remains essential.

Tests exercise the real runtime provider classes with controlled HTTP responses, including principal and role lookup, role mismatch and malformed responses, assignment, retries, missing/disabled/mismatched users, authorization failures, wrong deployment bindings, cancellation, and response redaction. These are HTTP contract tests, not evidence of a run against deployed Keycloak. Role mappings use the [Keycloak Admin REST API](https://www.keycloak.org/docs-api/latest/rest-api/index.html#_role_mapper_resource).

## Build dependency

Initialize the pinned submodule before restore (`git submodule update --init --recursive`). CI does this during checkout. Runtime dependency lockfiles live in `external/locks`, outside the unmodified submodule; `external/Directory.Build.props` preserves upstream build settings instead of inheriting provisioning's warnings-as-errors policy. The pinned runtime currently emits seven existing CS0108/CS1066 warnings in `IArchiveProvider`; those are not changed or suppressed here.

Next slice: protected host authentication/SSO routes and UI integration. The current Blazor simulation is unchanged and this adapter does not itself authorize browser requests.
