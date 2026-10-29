/*
 *	@file	src/SocialGamesPlatform.Observability/ObservabilityExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Plugs OpenTelemetry (logs, traces, metrics) into a host when a collector is configured
 */

// Using
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace SocialGamesPlatform.Observability;

/// <summary>
/// Extensions used to send the telemetry of an executable to an OpenTelemetry collector
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one single place that decides how every executable of the platform (Back, Very Back, Gateway, website) reports
/// its logs, traces and metrics, so that a collector can be plugged in by configuration only.</para>
/// <para><b>Behavior:</b> reads the "Observability" configuration section. The logs always go to the system log of the machine (Windows Event Log
/// or systemd journal). With a collector address, it also adds the OpenTelemetry providers, describes the executable (name, version, environment,
/// machine) and exports the three signals to the collector.</para>
/// </remarks>
public static class ObservabilityExtensions
{
    #region Members

    #region Constants

    /// <summary>
    /// Namespace under which every executable of the platform is grouped in the collector
    /// </summary>
    private const string ServiceNamespace = "SocialGamesPlatform";

    /// <summary>
    /// Pattern matching the trace sources and meters created by the platform's own code
    /// </summary>
    private const string PlatformSourcesPattern = "SocialGamesPlatform.*";

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

    // #N/A

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

    /// <summary>
    /// Method used to write the logs to the system log of the machine
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> keeps a local record of every executable in the tool the administrator already uses (Event Viewer, journalctl),
    /// also when no collector is configured or when the collector is down.</para>
    /// <para><b>Behavior:</b> on Windows, the logs go to the Application log of the Event Log, under one source per executable (named after the
    /// service); only an administrator can create that source, which the installer does. On Linux, when the executable runs as a systemd unit,
    /// the console output is formatted for the systemd journal, so that journalctl knows the level of each entry. The levels written are set by
    /// the "Logging:EventLog:LogLevel" and "Logging:Console:LogLevel" settings.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host</param>
    /// <param name="serviceName">[Cannot be null] Name of the executable, used as the Event Log source</param>
    private static void ConfigureSystemLogs(IHostApplicationBuilder builder, string serviceName)
    {
        // Windows: Application log of the Event Log, one source per executable
        if (OperatingSystem.IsWindows())
            AddWindowsEventLog(builder, serviceName);

        // Linux: console formatted for the systemd journal (only when the executable runs as a systemd unit)
        if (OperatingSystem.IsLinux())
            builder.Services.AddSystemd();
    }

    /// <summary>
    /// Method used to write the logs to the Application log of the Windows Event Log, under the source of the executable
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> isolates the Windows-only Event Log settings, so that the compiler checks they are never reached on another system.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host</param>
    /// <param name="serviceName">[Cannot be null] Name of the executable, used as the Event Log source</param>
    [SupportedOSPlatform("windows")]
    private static void AddWindowsEventLog(IHostApplicationBuilder builder, string serviceName)
    {
        // Setting the log and the source
        builder.Logging.AddEventLog(settings =>
        {
            settings.LogName = "Application";
            settings.SourceName = serviceName;
        });
    }

    /// <summary>
    /// Method used to compute the address of the collector for one signal
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> with HTTP, each signal has its own path on the collector (v1/logs, v1/traces, v1/metrics);
    /// with gRPC, the same address serves the three signals.</para>
    /// <para><b>Behavior:</b> the signal path is appended to the configured address, which keeps a path such as http://host/otel
    /// (a collector behind a reverse proxy).</para>
    /// </remarks>
    /// <param name="endpoint">[Cannot be null] Address of the collector, as configured</param>
    /// <param name="transport">Protocol used to reach the collector</param>
    /// <param name="signalPath">[Cannot be null] Path of the signal on an HTTP collector, without a leading slash</param>
    /// <returns>
    /// The address to which the signal is exported
    /// </returns>
    private static Uri GetSignalEndpoint(Uri endpoint, OtlpTransport transport, string signalPath)
    {
        // Keeping the same address for gRPC
        if (transport != OtlpTransport.HttpProtobuf)
        {
            // Function's return
            return endpoint;
        }

        // Making the address end with a slash, so that the signal path is appended to it instead of replacing its last segment
        string baseAddress = endpoint.AbsoluteUri.EndsWith('/') ? endpoint.AbsoluteUri : endpoint.AbsoluteUri + "/";

        // Function's return
        return new Uri(new Uri(baseAddress), signalPath);
    }

    /// <summary>
    /// Method used to point an exporter at the collector
    /// </summary>
    /// <param name="exporter">[Cannot be null] Settings of the exporter to fill in</param>
    /// <param name="endpoint">[Cannot be null] Address of the collector, as configured</param>
    /// <param name="transport">Protocol used to reach the collector</param>
    /// <param name="signalPath">[Cannot be null] Path of the signal on an HTTP collector</param>
    private static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint, OtlpTransport transport, string signalPath)
    {
        // Setting the address and the protocol
        exporter.Endpoint = GetSignalEndpoint(endpoint, transport, signalPath);
        exporter.Protocol = transport == OtlpTransport.HttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to write the logs to the system log of the machine, and to send the logs, traces and metrics to the configured OpenTelemetry collector
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> called once by every executable while it builds its host.</para>
    /// <para><b>Behavior:</b> the logs always go to the system log of the machine (Windows Event Log or systemd journal), in addition to the console.
    /// Without "Observability:OtlpEndpoint", nothing else is added. With it, the executable is
    /// described by its name (setting "Observability:ServiceName", else its application name), its version, its environment and its machine; HTTP
    /// client calls, the .NET runtime and the platform's own sources are instrumented, plus whatever the caller adds (for example ASP.NET Core).</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host to instrument</param>
    /// <param name="configureTracing">[Can be null] Extra trace instrumentation of the caller</param>
    /// <param name="configureMetrics">[Can be null] Extra metric instrumentation of the caller</param>
    /// <returns>
    /// True  ==> A collector is configured and the telemetry is sent to it<br/>
    /// False ==> No collector is configured; only the system log was added
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    /// <exception cref="InvalidOperationException">If the configured collector address is not an absolute http or https address</exception>
    public static bool AddPlatformObservability(this IHostApplicationBuilder builder, Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Reading the settings
        ObservabilityOptions options = new ObservabilityOptions();
        builder.Configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);

        // Naming the executable, for the system log and for the collector
        string serviceName = string.IsNullOrWhiteSpace(options.ServiceName) ? builder.Environment.ApplicationName : options.ServiceName;

        // Writing the logs to the system log of the machine, whether a collector is configured or not
        ConfigureSystemLogs(builder, serviceName);

        // Doing nothing more when no collector is configured
        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            // Function's return
            return false;
        }

        // Checking the collector address: an absolute http or https address (rejects "localhost:4317", file paths…)
        if (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"The setting {ObservabilityOptions.SectionName}:OtlpEndpoint must be an absolute http or https address, for example http://collector:4317");
        }

        // Describing the executable: one unique identifier per running instance, the machine in host.name
        string serviceVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        Dictionary<string, object> attributes = new Dictionary<string, object>
        {
            { "deployment.environment.name", builder.Environment.EnvironmentName },
            { "host.name", Environment.MachineName }
        };
        OtlpTransport transport = options.OtlpProtocol;
        bool includeScopes = options.IncludeScopes;

        // Sending the three signals to the collector
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, ServiceNamespace, serviceVersion, autoGenerateServiceInstanceId: true)
                .AddAttributes(attributes))
            .WithTracing(tracing =>
            {
                tracing.AddSource(PlatformSourcesPattern);
                tracing.AddHttpClientInstrumentation();
                configureTracing?.Invoke(tracing);
                tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint, transport, "v1/traces"));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(PlatformSourcesPattern);
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                configureMetrics?.Invoke(metrics);
                metrics.AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint, transport, "v1/metrics"));
            })
            .WithLogging(logging => logging.AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint, transport, "v1/logs")),
                logOptions =>
                {
                    // The formatted message only repeats the values already sent as attributes; the scopes (request path, connection…) are opt-in
                    logOptions.IncludeFormattedMessage = true;
                    logOptions.IncludeScopes = includeScopes;
                });

        // Function's return
        return true;
    }

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
