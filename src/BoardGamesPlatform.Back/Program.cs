/*
 *	@file	src/BoardGamesPlatform.Back/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the Back: builds the host and runs its services until it is stopped
 */

// Using
using BoardGamesPlatform.Back;
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.Observability;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building the host
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Registering the settings (checked at startup: fail closed), the client of the Very Back and the listener of the Gateway; the startup log
// comes after, so that "Back started" is written once the listener is bound
builder.AddPlatformBack();
builder.Services.AddHostedService<StartupService>();

// Sending the telemetry to the OpenTelemetry collector, when one is configured
builder.AddPlatformObservability();
IHost host = builder.Build();

// Keeping the hosted services, read before the host runs since it is disposed afterwards; building them reads the settings, so an invalid
// setting already stops the Back here, before anything listens
IHostedService[] hostedServices = host.Services.GetServices<IHostedService>().ToArray();

// Running the host until it is stopped, then telling the service manager whether a background service failed
await host.RunAsync();
Environment.ExitCode = HostExitCode.FromHostedServices(hostedServices);
