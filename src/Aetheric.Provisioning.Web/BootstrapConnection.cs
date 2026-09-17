using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Registry;
using AethericForge.Runtime.Providers.Identity.Keycloak;

namespace Aetheric.Provisioning.Web;

// The destination is deployment-owned; browser input can never redirect client credentials.
public sealed class BootstrapConnectionConfiguration
{
    public string Authority { get; init; } = "";
    public string Realm { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string AdminRole { get; init; } = "provisioner-admin";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority)
        && !string.IsNullOrWhiteSpace(Realm) && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(AdminRole);

    public RegistryBootstrapSettings Settings => new(
        Authority.TrimEnd('/') + "/realms/" + Uri.EscapeDataString(Realm), ClientId, AdminRole);
}

public sealed class BootstrapConnection(BootstrapConnectionConfiguration configuration)
{
    public async Task CheckAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (!configuration.IsConfigured)
            throw new RegistryBootstrapStaffException("registry.connection_not_configured");
        using var staff = new KeycloakRegistryBootstrapStaff(configuration.Settings, new KeycloakOptions
        {
            Authority = configuration.Authority,
            Realm = configuration.Realm,
            ClientId = clientId,
            ClientSecret = clientSecret
        });
        await staff.CheckConnectionAsync(ct);
    }
}
