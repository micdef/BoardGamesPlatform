/*
 *	@file	src/SocialGamesPlatform.VeryBack/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the Very Back: builds the host and runs its services until it is stopped
 */

// Using
using SocialGamesPlatform.VeryBack.Services;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building the host
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<StartupService>();
IHost host = builder.Build();

// Running the host until it is stopped
await host.RunAsync();
