/*
 *	@file	src/SocialGamesPlatform.Observability/OtlpTransport.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Protocols available to send telemetry to an OpenTelemetry collector
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace SocialGamesPlatform.Observability;

/// <summary>
/// Protocols available to send telemetry to an OpenTelemetry collector
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the configuration name the protocol without depending on the OpenTelemetry library types.</para>
/// </remarks>
public enum OtlpTransport
{
    /// <summary>
    /// gRPC, port 4317 by convention
    /// </summary>
    Grpc,

    /// <summary>
    /// HTTP with Protocol Buffers, port 4318 by convention
    /// </summary>
    HttpProtobuf
}
