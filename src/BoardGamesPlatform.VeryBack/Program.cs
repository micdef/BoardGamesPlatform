/*
 *	@file	src/BoardGamesPlatform.VeryBack/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the Very Back: builds the host and runs its services until it is stopped
 */

// Using
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.VeryBack;
using BoardGamesPlatform.VeryBack.Services;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building the host
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Registering the settings (checked at startup: fail closed), the call of the database and the listener of the Back; the startup log comes
// after, so that "Very Back started" is written once the listener is bound
builder.AddPlatformVeryBack();
builder.Services.AddHostedService<StartupService>();

// Sending the telemetry to the OpenTelemetry collector, when one is configured
builder.AddPlatformObservability();
IHost host = builder.Build();

// Keeping the hosted services, read before the host runs since it is disposed afterwards; building them reads the settings, so an invalid
// setting already stops the Very Back here, before anything listens
IHostedService[] hostedServices = host.Services.GetServices<IHostedService>().ToArray();

// Running the host until it is stopped, then telling the service manager whether a background service failed
await host.RunAsync();
Environment.ExitCode = HostExitCode.FromHostedServices(hostedServices);
