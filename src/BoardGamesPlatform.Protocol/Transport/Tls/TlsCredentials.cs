/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsCredentials.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Certificate of a tier, the roots it trusts and the certificates it refuses, checked when loaded and reloaded when their files change
 */

// Using
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// What a tier needs for mutual TLS on its links: its certificate with its key, the roots of the platform, and the thumbprints it refuses
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier must never run with a certificate that its peers would refuse: it checks its own certificate when it loads it,
/// and a renewed certificate is taken without restarting the tier.</para>
/// <para><b>Behavior:</b></para>
/// <list type="bullet">
/// <item><description>Loading fails (fail closed) when the certificate has no private key, is not signed by a trusted root, is not valid now,
/// is not the one of the role of the tier or lacks one of its usages, or is refused.</description></item>
/// <item><description>Loaded from files (<see cref="FromFiles"/>), the credentials check before each new connection whether the PFX or the file
/// of the roots changed: if so, they are loaded and checked again, and replace the current ones; when the new ones fail the check, the
/// current ones are kept and the failure is logged. Established connections keep the certificate they started with.</description></item>
/// <item><description>A certificate that expires within the warning delay is logged as a warning at each load.</description></item>
/// </list>
/// <para>The logs go through the logger of the tier, with the event ids 9036 to 9039 (see the coding conventions). The password is never
/// logged.</para>
/// </remarks>
public sealed partial class TlsCredentials
{
    #region Members

    #region Constants

    /// <summary>
    /// Warning delay before the expiry of the certificate, when none is given
    /// </summary>
    public static readonly TimeSpan DefaultExpiryWarning = TimeSpan.FromDays(30);

    #endregion

    #region Variables

    /// <summary>
    /// Lock of the reload: one check of the files at a time
    /// </summary>
    private readonly Lock _reloadLock = new Lock();

    /// <summary>
    /// Thumbprints refused, whatever their validity
    /// </summary>
    private readonly IReadOnlySet<string> _refusedThumbprints;

    /// <summary>
    /// Clock of the validity checks
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Delay before the expiry under which a warning is logged
    /// </summary>
    private readonly TimeSpan _expiryWarning;

    /// <summary>
    /// Files the credentials were loaded from; null when they were given as certificates
    /// </summary>
    private readonly SourceFiles? _files;

    /// <summary>
    /// Current state: the certificate, its context and the roots; replaced as a whole by a reload
    /// </summary>
    private volatile State _state;

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
    /// Role of the tier, written in its certificate
    /// </summary>
    public PlatformRole Role { get; }

    /// <summary>
    /// Suffix of the deployment
    /// </summary>
    public string Suffix { get; }

    /// <summary>
    /// Certificate of the tier currently used for new connections, with its key
    /// </summary>
    public X509Certificate2 Certificate => _state.Certificate;

    /// <summary>
    /// Roots of the platform currently trusted
    /// </summary>
    public X509Certificate2Collection TrustedRoots => _state.Roots;

    /// <summary>
    /// Thumbprints refused (SHA-256, upper case)
    /// </summary>
    public IReadOnlySet<string> RefusedThumbprints => _refusedThumbprints;

    /// <summary>
    /// Clock of the validity checks
    /// </summary>
    public TimeProvider TimeProvider => _timeProvider;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">Suffix of the deployment, already checked</param>
    /// <param name="state">First state, already checked</param>
    /// <param name="refusedThumbprints">Thumbprints refused</param>
    /// <param name="expiryWarning">Delay before the expiry under which a warning is logged</param>
    /// <param name="files">Files of the credentials, or null</param>
    /// <param name="timeProvider">Clock of the checks</param>
    /// <param name="logger">Logger of the tier</param>
    private TlsCredentials(PlatformRole role, string suffix, State state, IReadOnlySet<string> refusedThumbprints, TimeSpan expiryWarning, SourceFiles? files,
        TimeProvider timeProvider, ILogger logger)
    {
        // Setting
        Role = role;
        Suffix = suffix;
        _state = state;
        _refusedThumbprints = refusedThumbprints;
        _expiryWarning = expiryWarning;
        _files = files;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a certificate that expires soon
    /// </summary>
    /// <param name="identity">DNS name of the certificate</param>
    /// <param name="thumbprint">Its thumbprint</param>
    /// <param name="notAfter">End of its validity</param>
    [LoggerMessage(EventId = 9036, Level = LogLevel.Warning, Message = "The certificate of {Identity} ({Thumbprint}) expires on {NotAfter:u}: renew it")]
    private partial void LogExpiresSoon(string identity, string thumbprint, DateTimeOffset notAfter);

    /// <summary>
    /// Method used to log a certificate loaded
    /// </summary>
    /// <param name="identity">DNS name of the certificate</param>
    /// <param name="thumbprint">Its thumbprint</param>
    /// <param name="notAfter">End of its validity</param>
    [LoggerMessage(EventId = 9037, Level = LogLevel.Information, Message = "Certificate of {Identity} loaded ({Thumbprint}), valid until {NotAfter:u}")]
    private partial void LogLoaded(string identity, string thumbprint, DateTimeOffset notAfter);

    /// <summary>
    /// Method used to log a reload that failed, the current certificate being kept
    /// </summary>
    /// <param name="exception">Why the new files were refused</param>
    [LoggerMessage(EventId = 9038, Level = LogLevel.Error, Message = "The changed certificate files were refused; the current certificate is kept")]
    private partial void LogReloadFailed(Exception exception);

    /// <summary>
    /// Method used to normalize the refused thumbprints
    /// </summary>
    /// <param name="refusedThumbprints">[Cannot be null] Thumbprints given</param>
    /// <returns>
    /// The set, in upper case
    /// </returns>
    /// <exception cref="ArgumentException">If a thumbprint is not 64 hexadecimal digits</exception>
    private static HashSet<string> ReadRefused(IEnumerable<string> refusedThumbprints)
    {
        // Checking each thumbprint, written in any case
        HashSet<string> refused = new HashSet<string>(StringComparer.Ordinal);
        foreach (string thumbprint in refusedThumbprints)
        {
            if (thumbprint is null || thumbprint.Length != 64 || !thumbprint.All(char.IsAsciiHexDigit))
                throw new ArgumentException("A refused thumbprint must be the 64 hexadecimal digits of a SHA-256 thumbprint", nameof(refusedThumbprints));
            refused.Add(thumbprint.ToUpperInvariant());
        }

        // Function's return
        return refused;
    }

    /// <summary>
    /// Method used to check the certificate of the tier and build the state that uses it
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">Suffix of the deployment</param>
    /// <param name="certificate">[Cannot be null] Certificate of the tier, with its key</param>
    /// <param name="roots">[Cannot be null] Roots trusted</param>
    /// <param name="refused">[Cannot be null] Thumbprints refused</param>
    /// <param name="now">Time of the check</param>
    /// <returns>
    /// The state
    /// </returns>
    /// <exception cref="InvalidOperationException">If the certificate cannot be used by this tier</exception>
    private static State CreateState(PlatformRole role, string suffix, X509Certificate2 certificate, X509Certificate2Collection roots, IReadOnlySet<string> refused, DateTimeOffset now)
    {
        // Checking the key, then the certificate for each side the role takes, as its peers will
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("The certificate of the tier has no private key");
        if (roots.Count == 0)
            throw new InvalidOperationException("No trusted root of the platform");
        foreach (string usage in new string?[] { PlatformIdentity.IsServer(role) ? PlatformIdentity.ServerAuthenticationOid : null, PlatformIdentity.IsClient(role) ? PlatformIdentity.ClientAuthenticationOid : null }.OfType<string>())
        {
            string? problem = PeerCertificateValidator.Validate(certificate, role, suffix, usage, roots, refused, now);
            if (problem is not null)
                throw new InvalidOperationException($"The certificate of the tier cannot be used: {problem}");
        }

        // Function's return
        return new State(certificate, SslStreamCertificateContext.Create(certificate, additionalCertificates: null, offline: true), roots);
    }

    /// <summary>
    /// Method used to read the files of the credentials
    /// </summary>
    /// <param name="files">[Cannot be null] Files to read</param>
    /// <returns>
    /// The certificate with its key and the roots
    /// </returns>
    /// <exception cref="InvalidOperationException">If a file is missing, or cannot be read</exception>
    private static (X509Certificate2 Certificate, X509Certificate2Collection Roots) ReadFiles(SourceFiles files)
    {
        // Reading the PFX and the roots; a wrong password or a damaged file says so, without the password
        if (!File.Exists(files.CertificatePath))
            throw new InvalidOperationException($"The certificate file {files.CertificatePath} does not exist");
        if (!File.Exists(files.TrustedRootsPath))
            throw new InvalidOperationException($"The file of the trusted roots {files.TrustedRootsPath} does not exist");
        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(files.CertificatePath, files.Password);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException($"The certificate file {files.CertificatePath} cannot be opened: wrong password, or not a PFX", exception);
        }
        X509Certificate2Collection roots = new X509Certificate2Collection();
        try
        {
            roots.ImportFromPemFile(files.TrustedRootsPath);
        }
        catch (CryptographicException exception)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"The file of the trusted roots {files.TrustedRootsPath} holds no valid PEM certificate", exception);
        }

        // Function's return
        return (certificate, roots);
    }

    /// <summary>
    /// Method used to log the certificate loaded, and warn when it expires soon
    /// </summary>
    /// <param name="state">[Cannot be null] State loaded</param>
    private void LogState(State state)
    {
        // Telling which certificate is used, and when it must be renewed
        string identity = PlatformIdentity.GetDnsName(Role, Suffix);
        string thumbprint = PeerCertificateValidator.GetThumbprint(state.Certificate);
        DateTimeOffset notAfter = new DateTimeOffset(state.Certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
        LogLoaded(identity, thumbprint, notAfter);
        if (notAfter - _timeProvider.GetUtcNow() <= _expiryWarning)
            LogExpiresSoon(identity, thumbprint, notAfter);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to build credentials from certificates already loaded (the tests); they are never reloaded
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment</param>
    /// <param name="certificate">[Cannot be null] Certificate of the tier, with its key; it stays owned by the caller</param>
    /// <param name="trustedRoots">[Cannot be null or empty] Roots of the platform</param>
    /// <param name="refusedThumbprints">[Cannot be null] SHA-256 thumbprints refused</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the checks</param>
    /// <returns>
    /// The credentials
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the suffix or a thumbprint is invalid</exception>
    /// <exception cref="InvalidOperationException">If the certificate cannot be used by this tier</exception>
    public static TlsCredentials FromCertificates(PlatformRole role, string suffix, X509Certificate2 certificate, X509Certificate2Collection trustedRoots,
        IEnumerable<string> refusedThumbprints, TimeProvider timeProvider)
    {
        // Checks
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate), $"The value of the parameter: {nameof(certificate)} cannot be null");
        if (trustedRoots is null)
            throw new ArgumentNullException(nameof(trustedRoots), $"The value of the parameter: {nameof(trustedRoots)} cannot be null");
        if (refusedThumbprints is null)
            throw new ArgumentNullException(nameof(refusedThumbprints), $"The value of the parameter: {nameof(refusedThumbprints)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (!PlatformIdentity.IsValidSuffix(suffix))
            throw new ArgumentException($"The value of the parameter: {nameof(suffix)} is not a valid suffix", nameof(suffix));

        // Checking the certificate, then building the credentials
        HashSet<string> refused = ReadRefused(refusedThumbprints);
        State state = CreateState(role, suffix, certificate, trustedRoots, refused, timeProvider.GetUtcNow());

        // Function's return
        return new TlsCredentials(role, suffix, state, refused, DefaultExpiryWarning, null, timeProvider, NullLogger.Instance);
    }

    /// <summary>
    /// Method used to load credentials from their files, reloaded when the files change
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment</param>
    /// <param name="certificatePath">[Cannot be null or blank] PFX of the tier, with its key</param>
    /// <param name="password">[Cannot be null] Password of the PFX</param>
    /// <param name="trustedRootsPath">[Cannot be null or blank] PEM file of the roots of the platform (several roots during a change of root)</param>
    /// <param name="refusedThumbprints">[Cannot be null] SHA-256 thumbprints refused</param>
    /// <param name="expiryWarning">Delay before the expiry under which a warning is logged; zero or more</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the checks</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <returns>
    /// The credentials
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the suffix, a path or a thumbprint is invalid</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the warning delay is negative</exception>
    /// <exception cref="InvalidOperationException">If a file is missing or unreadable, or the certificate cannot be used by this tier</exception>
    public static TlsCredentials FromFiles(PlatformRole role, string suffix, string certificatePath, string password, string trustedRootsPath, IEnumerable<string> refusedThumbprints,
        TimeSpan expiryWarning, TimeProvider timeProvider, ILogger logger)
    {
        // Checks
        if (password is null)
            throw new ArgumentNullException(nameof(password), $"The value of the parameter: {nameof(password)} cannot be null");
        if (refusedThumbprints is null)
            throw new ArgumentNullException(nameof(refusedThumbprints), $"The value of the parameter: {nameof(refusedThumbprints)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (string.IsNullOrWhiteSpace(certificatePath))
            throw new ArgumentException($"The value of the parameter: {nameof(certificatePath)} cannot be null or blank", nameof(certificatePath));
        if (string.IsNullOrWhiteSpace(trustedRootsPath))
            throw new ArgumentException($"The value of the parameter: {nameof(trustedRootsPath)} cannot be null or blank", nameof(trustedRootsPath));
        if (!PlatformIdentity.IsValidSuffix(suffix))
            throw new ArgumentException($"The value of the parameter: {nameof(suffix)} is not a valid suffix", nameof(suffix));
        if (expiryWarning < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiryWarning), $"The value of the parameter: {nameof(expiryWarning)} cannot be negative");

        // Reading and checking the files, their change times being kept for the reload
        HashSet<string> refused = ReadRefused(refusedThumbprints);
        SourceFiles files = new SourceFiles(Path.GetFullPath(certificatePath), password, Path.GetFullPath(trustedRootsPath));
        DateTime certificateTime = File.GetLastWriteTimeUtc(files.CertificatePath);
        DateTime rootsTime = File.GetLastWriteTimeUtc(files.TrustedRootsPath);
        (X509Certificate2 certificate, X509Certificate2Collection roots) = ReadFiles(files);
        State state;
        try
        {
            state = CreateState(role, suffix, certificate, roots, refused, timeProvider.GetUtcNow());
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
        files.Seen(certificateTime, rootsTime);
        TlsCredentials credentials = new TlsCredentials(role, suffix, state, refused, expiryWarning, files, timeProvider, logger);
        credentials.LogState(state);

        // Function's return
        return credentials;
    }

    /// <summary>
    /// Method used to load the credentials of a tier from its "Tls" settings, already checked by <see cref="TlsOptionsValidator"/>
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="options">[Cannot be null] Settings of the tier</param>
    /// <param name="contentRoot">[Cannot be null or blank] Folder of the tier, from which a relative path is taken</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the checks</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <returns>
    /// The credentials, reloaded when their files change
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If a setting is invalid</exception>
    /// <exception cref="InvalidOperationException">If a file is missing or unreadable, or the certificate cannot be used by this tier</exception>
    public static TlsCredentials FromOptions(PlatformRole role, TlsOptions options, string contentRoot, TimeProvider timeProvider, ILogger logger)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (string.IsNullOrWhiteSpace(contentRoot))
            throw new ArgumentException($"The value of the parameter: {nameof(contentRoot)} cannot be null or blank", nameof(contentRoot));

        // Function's return: the paths taken from the folder of the tier when they are relative
        return FromFiles(role, options.IdentitySuffix, Path.Combine(contentRoot, options.CertificatePath), options.CertificatePassword,
            Path.Combine(contentRoot, options.TrustedRootsPath), options.RefusedThumbprints, options.ExpiryWarning, timeProvider, logger);
    }

    /// <summary>
    /// Method used to take the certificate files again when they changed, before a new connection
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: new files that fail the check are logged and the current certificate is kept, so that a mistake
    /// while renewing never stops a tier that runs.</para>
    /// </remarks>
    public void ReloadIfChanged()
    {
        // Nothing to reload for credentials given as certificates
        if (_files is null)
            return;

        // Comparing the change times, one check at a time
        lock (_reloadLock)
        {
            DateTime certificateTime;
            DateTime rootsTime;
            try
            {
                certificateTime = File.GetLastWriteTimeUtc(_files.CertificatePath);
                rootsTime = File.GetLastWriteTimeUtc(_files.TrustedRootsPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogReloadFailed(exception);
                return;
            }
            if (!_files.HasChanged(certificateTime, rootsTime))
                return;

            // Loading and checking the new files; the current state stays when they are refused, and the change is not tried again until
            // the files change once more
            _files.Seen(certificateTime, rootsTime);
            try
            {
                (X509Certificate2 certificate, X509Certificate2Collection roots) = ReadFiles(_files);
                try
                {
                    State state = CreateState(Role, Suffix, certificate, roots, _refusedThumbprints, _timeProvider.GetUtcNow());
                    _state = state;
                    LogState(state);
                }
                catch
                {
                    certificate.Dispose();
                    throw;
                }
            }
            catch (InvalidOperationException exception)
            {
                LogReloadFailed(exception);
            }
        }
    }

    /// <summary>
    /// Method used to get the certificate context for a new connection, after taking changed files
    /// </summary>
    /// <returns>
    /// The context of the current certificate
    /// </returns>
    internal SslStreamCertificateContext GetContextForNewConnection()
    {
        // Reloading first, then reading the current state
        ReloadIfChanged();

        // Function's return
        return _state.Context;
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

    /// <summary>
    /// Certificate in use, its context and the roots trusted, replaced together
    /// </summary>
    /// <param name="Certificate">Certificate of the tier, with its key</param>
    /// <param name="Context">Context built once for the TLS handshakes</param>
    /// <param name="Roots">Roots trusted</param>
    private sealed record State(X509Certificate2 Certificate, SslStreamCertificateContext Context, X509Certificate2Collection Roots);

    /// <summary>
    /// Files of the credentials and the change times last taken
    /// </summary>
    /// <param name="CertificatePath">PFX of the tier</param>
    /// <param name="Password">Password of the PFX</param>
    /// <param name="TrustedRootsPath">PEM file of the roots</param>
    private sealed record SourceFiles(string CertificatePath, string Password, string TrustedRootsPath)
    {
        /// <summary>
        /// Change time of the PFX last taken
        /// </summary>
        private DateTime _certificateTime;

        /// <summary>
        /// Change time of the roots last taken
        /// </summary>
        private DateTime _rootsTime;

        /// <summary>
        /// Method used to record the change times taken
        /// </summary>
        /// <param name="certificateTime">Change time of the PFX</param>
        /// <param name="rootsTime">Change time of the roots</param>
        public void Seen(DateTime certificateTime, DateTime rootsTime)
        {
            // Setting
            _certificateTime = certificateTime;
            _rootsTime = rootsTime;
        }

        /// <summary>
        /// Method used to tell whether a file changed since it was last taken
        /// </summary>
        /// <param name="certificateTime">Change time of the PFX now</param>
        /// <param name="rootsTime">Change time of the roots now</param>
        /// <returns>
        /// True when one of them changed
        /// </returns>
        public bool HasChanged(DateTime certificateTime, DateTime rootsTime)
        {
            // Function's return
            return certificateTime != _certificateTime || rootsTime != _rootsTime;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // Function's return: never the password
            return $"{nameof(SourceFiles)} {{ {nameof(CertificatePath)} = {CertificatePath}, {nameof(TrustedRootsPath)} = {TrustedRootsPath} }}";
        }
    }
}
