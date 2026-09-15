using Aetheric.Provisioning.Simulation;
using Aetheric.Provisioning.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Each browser circuit gets an isolated, in-memory simulation.
builder.Services.AddScoped<SimulationSession>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
