# Registry bootstrap foundation

The human sysadmin creates the initial Keycloak principal and credentials. This increment implements the host-independent bootstrap lifecycle only. It does not expose routes, authenticate bootstrap passwords, implement OIDC, or call Keycloak yet.

Deployment explicitly initializes a private `FileRegistryBootstrapStore` with the expected HTTPS realm issuer, provisioner client ID, and provisioner admin role. Ordinary application startup must only read that record: missing or corrupt state fails closed. Initialization refuses to replace an existing record. Losing the directory requires deliberate operator recovery, never automatic bootstrap reactivation.

`RegistryBootstrap` requires authenticated bootstrap operator access before selecting an existing principal by immutable subject ID. The directory/staff adapter must check that principal in the configured realm. The selected subject is saved before granting authority; failures cannot redirect the bootstrap grant to another user. Retries must reconcile the same role and assignment through the runtime Registry Clerk. Authority here is provisioner administration, not unrestricted Keycloak administration.

The persisted sequence is `AwaitingPrincipal → PrincipalSelected → AuthorityAssigned → Completed`. Completion requires a host-verified SSO identity matching the exact issuer and selected subject, with the configured admin role. The host must validate the OIDC response, audience, session, and role mapping before constructing that identity; input fields are not evidence of authentication. `IRegistryBootstrapAccess` is a trusted, request-scoped host boundary, not a public request DTO.

Completion is durable and prevents further bootstrap assignments. The future host must also reject the deployment bootstrap login and invalidate bootstrap sessions once state is completed. A matching administrator can safely repeat completion after a lost HTTP response. State operations hold a local cross-process lease and use the existing private atomic file storage. This has the same local-filesystem and power-loss limitations as the run-state store.

`IRegistryBootstrapStaff` is an adapter seam for the runtime identity directory and `IRegistryClerk`; it intentionally has no principal-creation operation. It must ensure role/assignment compatibility and treat existing compatible authority as success, including when a provider operation succeeded before a checkpoint failure. Credentials and tokens never belong in this state file.

Next slice: implement the concrete runtime staff adapter and protected host authentication/SSO routes. The current Blazor simulation is unchanged and this foundation does not claim a live authentication boundary.
