/*
 *	@file	src/BoardGamesPlatform.CertificateAuthority/Program.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Entry point of the offline certificate authority tool: runs one command and ends
 */

// Using
using BoardGamesPlatform.CertificateAuthority;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
// #N/A (top-level statements)

// Running the command, the passwords being read from the environment or the console, and telling the shell how it went
AuthorityCommands commands = new AuthorityCommands(new ConsolePasswordSource(), Console.Out, Console.Error, TimeProvider.System);
return commands.Run(args);
