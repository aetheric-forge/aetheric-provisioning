using Microsoft.AspNetCore.HttpOverrides;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Simulation;
using Aetheric.Provisioning.Web.Components;
using Aetheric.Provisioning.Web;

var initializeBootstrap = args.Contains("--initialize-bootstrap", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--initialize-bootstrap").ToArray());
// Trust only the framework's default loopback proxies. Vulcan's nginx connects locally.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
var connectionConfiguration = builder.Configuration.GetSection("BootstrapConnection")
    .Get<BootstrapConnectionConfiguration>() ?? new();
if (initializeBootstrap)
{
    using var validation = new Aetheric.Provisioning.Registry.KeycloakAdministratorCreator(
        connectionConfiguration.Options(connectionConfiguration.ClientId, "configuration-validation"), connectionConfiguration.AdminRole);
    await new Aetheric.Provisioning.Persistence.FileRegistryBootstrapStore(connectionConfiguration.StateDirectory)
        .InitializeAsync(connectionConfiguration.Settings);
    Console.WriteLine("Initialized bootstrap deployment state. Existing state is never replaced.");
    return;
}
builder.Services.AddSingleton<IRegistryBootstrapStore>(_ =>
    new Aetheric.Provisioning.Persistence.FileRegistryBootstrapStore(connectionConfiguration.StateDirectory));
builder.Services.AddSingleton<ISetupRegistryClients, SetupRegistryClients>();
builder.Services.AddScoped<SetupBootstrap>();
var administratorSignIn = new AdministratorSignInConfiguration(connectionConfiguration, builder.Environment.IsDevelopment());
builder.Services.AddSingleton(connectionConfiguration);
builder.AddSetupAuthentication(connectionConfiguration, administratorSignIn);
builder.Services.AddScoped<BootstrapConnection>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton(_ => PublicGitHubSource.CreateHttpClient());
builder.Services.AddScoped<IDefinitionSource, PublicGitHubSource>();
builder.Services.AddSingleton<InstitutionYamlReader>();
builder.Services.AddScoped<IResourceProvider, SimulatedWorkbenchProvider>();
builder.Services.AddScoped<IParentCapabilityResolver, SimulatedCatalogParentResolver>();
builder.Services.AddScoped<IRunStateStore, InMemoryRunStateStore>();
builder.Services.AddScoped<ISecretStore, InMemorySecretStore>();
builder.Services.AddScoped<ProvisioningPlanner>();
builder.Services.AddScoped<ProvisioningEngine>();
builder.Services.AddScoped<ProvisioningReview>();
var app = builder.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapSetupAuthentication(administratorSignIn);
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
