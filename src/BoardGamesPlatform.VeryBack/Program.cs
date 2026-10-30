/*
 *	@file	src/BoardGamesPlatform.VeryBack/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the Very Back: builds the host and runs its services until it is stopped
 */

// Using
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Services;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building the host
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<StartupService>();

// Settings, checked when the host starts: an invalid address or port, or a missing connection string, stops it (fail closed)
builder.Services.AddSingleton<IValidateOptions<VeryBackOptions>, VeryBackOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ConnectionStringsOptions>, ConnectionStringsOptionsValidator>();
builder.Services.AddOptions<VeryBackOptions>().Bind(builder.Configuration.GetSection(VeryBackOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<ConnectionStringsOptions>().Bind(builder.Configuration.GetSection(ConnectionStringsOptions.SectionName)).ValidateOnStart();

// Listening for the Back and calling the database
builder.Services.AddSingleton<IPingProcedure, SqlPingProcedure>();
builder.Services.AddHostedService<VeryBackListenerService>();

// Sending the telemetry to the OpenTelemetry collector, when one is configured
builder.AddPlatformObservability();
IHost host = builder.Build();

// Running the host until it is stopped
await host.RunAsync();
