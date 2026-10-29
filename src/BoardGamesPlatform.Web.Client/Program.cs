/*
 *	@file	src/BoardGamesPlatform.Web.Client/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the WebAssembly part of the website, run in the player's browser
 */

// Using
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Building and running the WebAssembly host
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
await builder.Build().RunAsync();
