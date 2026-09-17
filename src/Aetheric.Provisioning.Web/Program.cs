using Microsoft.AspNetCore.HttpOverrides;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Simulation;
using Aetheric.Provisioning.Web.Components;
using Aetheric.Provisioning.Web;

var builder = WebApplication.CreateBuilder(args);
// Trust only the framework's default loopback proxies. Vulcan's nginx connects locally.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddSingleton(builder.Configuration.GetSection("BootstrapConnection")
    .Get<BootstrapConnectionConfiguration>() ?? new());
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
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
