using System.Collections.Immutable;

namespace Aetheric.Provisioning.Application;

public sealed record RegistryBootstrapSettings(string Issuer, string ClientId, string AdminRole);
public enum RegistryBootstrapPhase { AwaitingPrincipal, PrincipalSelected, AuthorityAssigned, Completed }
public sealed record RegistryBootstrapState(RegistryBootstrapSettings Settings, RegistryBootstrapPhase Phase, string? SubjectId);
public sealed record BootstrapSignIn(string Issuer, string SubjectId, ImmutableHashSet<string> Roles);

// Host-scoped authentication boundary. Implement from validated sessions, never form fields.
public interface IRegistryBootstrapAccess
{
    Task RequireBootstrapOperatorAsync(CancellationToken ct);
    Task<BootstrapSignIn> GetVerifiedSignInAsync(CancellationToken ct);
}

// Adapter seam for the runtime directory and IRegistryClerk. No principal-creation operation.
public interface IRegistryBootstrapStaff
{
    Task<bool> PrincipalExistsAsync(string subjectId, CancellationToken ct);
    // Must reconcile an existing compatible role/assignment and never rotate credentials.
    Task EnsureAdminAuthorityAsync(string subjectId, string role, CancellationToken ct);
}

public interface IRegistryBootstrapStore
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct);
    Task<RegistryBootstrapState> ReadAsync(CancellationToken ct);
    Task SaveAsync(RegistryBootstrapState state, CancellationToken ct);
}

public sealed class RegistryBootstrap(RegistryBootstrapSettings settings, IRegistryBootstrapStore store,
    IRegistryBootstrapAccess access, IRegistryBootstrapStaff staff)
{
    public async Task<RegistryBootstrapState> AssignAdministratorAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        await access.RequireBootstrapOperatorAsync(ct);
        await using var lease = await store.AcquireAsync(ct);
        var state = await ReadAsync(ct);
        if (state.Phase == RegistryBootstrapPhase.Completed)
            throw new InvalidOperationException("Bootstrap is complete.");
        if (state.SubjectId is not null && state.SubjectId != subjectId)
            throw new InvalidOperationException("Bootstrap is already bound to another principal.");
        if (!await staff.PrincipalExistsAsync(subjectId, ct))
            throw new InvalidOperationException("The sysadmin must create this principal before bootstrap.");
        if (state.Phase == RegistryBootstrapPhase.AwaitingPrincipal)
        {
            state = state with { SubjectId = subjectId, Phase = RegistryBootstrapPhase.PrincipalSelected };
            // Commit the chosen identity before any external authority assignment.
            await store.SaveAsync(state, ct);
        }
        await staff.EnsureAdminAuthorityAsync(subjectId, settings.AdminRole, ct);
        state = state with { Phase = RegistryBootstrapPhase.AuthorityAssigned };
        await store.SaveAsync(state, CancellationToken.None);
        return state;
    }

    public async Task<RegistryBootstrapState> CompleteAfterSignInAsync(CancellationToken ct = default)
    {
        var signIn = await access.GetVerifiedSignInAsync(ct);
        await using var lease = await store.AcquireAsync(ct);
        var state = await ReadAsync(ct);
        if (state.Phase is not (RegistryBootstrapPhase.AuthorityAssigned or RegistryBootstrapPhase.Completed)
            || signIn.Issuer != settings.Issuer || signIn.SubjectId != state.SubjectId
            || !signIn.Roles.Contains(settings.AdminRole))
            throw new InvalidOperationException("Sign in as the selected provisioner administrator to complete bootstrap.");
        if (state.Phase == RegistryBootstrapPhase.Completed) return state;
        state = state with { Phase = RegistryBootstrapPhase.Completed };
        await store.SaveAsync(state, ct);
        return state;
    }

    private async Task<RegistryBootstrapState> ReadAsync(CancellationToken ct)
    {
        var state = await store.ReadAsync(ct);
        if (state.Settings != settings)
            throw new InvalidOperationException("Bootstrap configuration differs from its deployment record.");
        return state;
    }
}
