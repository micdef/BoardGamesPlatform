/*
 *	@file	src/BoardGamesPlatform.Web/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the website: builds the Blazor host (Interactive Server render mode only) and runs it
 */

// Using
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.Web.Components;
using BoardGamesPlatform.Web.GatewayLink;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Languages of the website: the first one is the default; the texts are in Resources/WebTexts.<culture>.resx
string[] supportedCultures = new string[] { "en", "fr" };

// Building the host with the Interactive Server render mode only (no WebAssembly, no static rendering of the pages)
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registering the texts of the website
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Registering the settings and the client of the Gateway (the website server is its only SignalR client)
builder.AddGatewayClient();

// Sending the telemetry of the server part to the OpenTelemetry collector, when one is configured, including the incoming HTTP requests
builder.AddPlatformObservability(
    tracing => tracing.AddAspNetCoreInstrumentation(),
    metrics => metrics.AddAspNetCoreInstrumentation());

WebApplication app = builder.Build();

// Setting the environment pipeline: generic error page and HSTS outside development
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Setting the request pipeline
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// Choosing the culture of each request among the supported languages (the circuit of the player keeps it)
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

// Mapping the components of the website
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Running the host until it is stopped
await app.RunAsync();
