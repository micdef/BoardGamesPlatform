/*
 *	@file	src/BoardGamesPlatform.Web/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the website: builds the Blazor Auto host (server and WebAssembly render modes) and runs it
 */

// Using
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.Web.Components;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Static using
// #N/A

// Named using
using Client = BoardGamesPlatform.Web.Client;

// Namespace
// #N/A (top-level statements)

// Building the host with both interactive render modes (Auto switches from server to WebAssembly)
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Sending the telemetry of the server part to the OpenTelemetry collector, when one is configured, including the incoming HTTP requests
builder.AddPlatformObservability(
    tracing => tracing.AddAspNetCoreInstrumentation(),
    metrics => metrics.AddAspNetCoreInstrumentation());

WebApplication app = builder.Build();

// Setting the environment pipeline: WebAssembly debugging in development, generic error page and HSTS otherwise
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Setting the request pipeline
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// Mapping the components of the server and of the WebAssembly parts
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

// Running the host until it is stopped
await app.RunAsync();
