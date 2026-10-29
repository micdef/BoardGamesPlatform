/*
 *	@file	src/SocialGamesPlatform.Gateway/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the Gateway: builds the web host and runs it until it is stopped
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building the host
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

// Exposing a health endpoint, so that a monitoring tool can check that the Gateway answers
app.MapGet("/health", () => Results.Ok());

// Running the host until it is stopped
await app.RunAsync();
