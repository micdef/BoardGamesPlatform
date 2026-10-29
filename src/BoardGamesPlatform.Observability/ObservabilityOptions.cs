/*
 *	@file	src/BoardGamesPlatform.Observability/ObservabilityOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "Observability" configuration section: where and how telemetry is sent
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Observability;

/// <summary>
/// Settings used to send the telemetry of an executable to an OpenTelemetry collector
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator plug a collector into any executable through its configuration only, without changing the code.</para>
/// <para><b>Behavior:</b> read from the "Observability" section of the configuration (appsettings.json or environment variables such as
/// <c>Observability__OtlpEndpoint</c>). When <see cref="OtlpEndpoint"/> is empty, nothing is sent and the executable keeps its local logs only.</para>
/// </remarks>
public sealed class ObservabilityOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "Observability";

    #endregion

    #region Variables

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Enumerations

    // #N/A

    #endregion

    #region Services

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members & Properties

    /// <summary>
    /// Address of the OpenTelemetry collector (for example http://collector:4317); empty to send nothing
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Protocol used to reach the collector: gRPC (port 4317 by convention) or HTTP with Protocol Buffers (port 4318)
    /// </summary>
    public OtlpTransport OtlpProtocol { get; set; } = OtlpTransport.Grpc;

    /// <summary>
    /// Name under which the executable appears in the collector and as its Windows Event Log source; empty to use the application name of its host (the project name by default)
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Whether the logging scopes are sent with the logs (for example the request path and the connection of ASP.NET Core); off by default
    /// </summary>
    public bool IncludeScopes { get; set; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    // #N/A

    #endregion

    #region Publics

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    // #N/A

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
