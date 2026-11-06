/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SigningKeys.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Keys of the secure channel of a tier, loaded from their files, checked, and taken again cold and hot
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Signing key of a tier and the pinned keys of its peer, as files
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier must never run with keys its peer would refuse, nor with a signing key that is also its TLS key; a key is
/// replaced or revoked without restarting the tier.</para>
/// <para><b>Behavior:</b></para>
/// <list type="bullet">
/// <item><description>Loading fails (fail closed) when the signing key cannot be read with its password or is not a private key of P-256,
/// when its password is the one of the PFX, when the file of the peer does not hold 1 to 4 distinct valid keys other than the key of the
/// tier, or when the signing key is the key of the TLS certificate of the tier.</description></item>
/// <item><description>Before each new connection and every <see cref="CheckInterval"/>, the files are compared with the last ones taken, by
/// their time of the last write and the SHA-256 of their content. Changed files are loaded and checked again, and replace the current keys
/// for the new connections; files refused are logged and the current keys kept, until the files change once more; files that cannot be read
/// now are tried again at the next check. Each connection takes one snapshot (<see cref="GetForNewConnection"/>).</description></item>
/// <item><description>A key removed from the list raises <see cref="PeerKeysRemoved"/>, so that the connections opened with it are closed;
/// <see cref="IsPinned"/> tells a handshake, as its last step, whether the key of its peer is still in the current list.</description></item>
/// <item><description>The password is read at startup only: a new signing key taken hot must have the same password.</description></item>
/// </list>
/// <para>Disposing the keys stops the periodic check. The logs use the event ids 9057 to 9060 (see the coding conventions). The password is
/// never logged.</para>
/// </remarks>
public sealed partial class SigningKeys : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Interval of the periodic check of the files: short, since a removed key must close its connections soon and the files are small
    /// </summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    #endregion

    #region Variables

    /// <summary>
    /// Lock of the reload: one check of the files at a time
    /// </summary>
    private readonly Lock _reloadLock = new Lock();

    /// <summary>
    /// Files of the keys and what was last taken of them
    /// </summary>
    private readonly SourceFiles _files;

    /// <summary>
    /// Current TLS certificate of the tier, read again at each check, since it is reloaded too
    /// </summary>
    private readonly Func<X509Certificate2> _tlsCertificate;

    /// <summary>
    /// Clock of the checks
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Current keys, replaced as a whole by a reload
    /// </summary>
    private volatile SigningKeySet _current;

    /// <summary>
    /// TLS certificate last compared with the signing key, and whether they hold the same key; under <see cref="_reloadLock"/>
    /// </summary>
    private (X509Certificate2 Certificate, SigningKeySet Keys, bool IsSameKey)? _certificateCheck;

    /// <summary>
    /// Timer of the periodic check
    /// </summary>
    private ITimer? _timer;

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
    /// Current keys
    /// </summary>
    public SigningKeySet Current => _current;

    #endregion

    #region Delegates

    /// <summary>
    /// Raised after a reload that removed keys of the peer, outside the lock of the reload
    /// </summary>
    public event EventHandler<PeerKeysRemovedEventArgs>? PeerKeysRemoved;

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="files">Files of the keys, already taken</param>
    /// <param name="current">First keys, checked</param>
    /// <param name="tlsCertificate">Current TLS certificate of the tier</param>
    /// <param name="timeProvider">Clock of the checks</param>
    /// <param name="logger">Logger of the tier</param>
    private SigningKeys(SourceFiles files, SigningKeySet current, Func<X509Certificate2> tlsCertificate, TimeProvider timeProvider, ILogger logger)
    {
        // Setting
        _files = files;
        _current = current;
        _tlsCertificate = tlsCertificate;
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
    /// Method used to log the keys loaded at startup
    /// </summary>
    /// <param name="ownKeyId">Key id of the signing key</param>
    /// <param name="peerKeyIds">Key ids of the peer</param>
    [LoggerMessage(EventId = 9057, Level = LogLevel.Information, Message = "Keys of the secure channel loaded: signing key {OwnKeyId}, keys of the peer {PeerKeyIds}")]
    private partial void LogLoaded(string ownKeyId, string peerKeyIds);

    /// <summary>
    /// Method used to log a change of the list of the keys of the peer
    /// </summary>
    /// <param name="added">Key ids added</param>
    /// <param name="removed">Key ids removed</param>
    [LoggerMessage(EventId = 9058, Level = LogLevel.Warning, Message = "The public keys of the peer changed: added {Added}, removed {Removed}")]
    private partial void LogPeerKeysChanged(string added, string removed);

    /// <summary>
    /// Method used to log a new signing key taken
    /// </summary>
    /// <param name="ownKeyId">Key id of the new signing key</param>
    [LoggerMessage(EventId = 9059, Level = LogLevel.Information, Message = "Signing key {OwnKeyId} taken for the new connections")]
    private partial void LogSigningKeyTaken(string ownKeyId);

    /// <summary>
    /// Method used to log changed files refused, the current keys being kept
    /// </summary>
    /// <param name="exception">Why the files were refused</param>
    [LoggerMessage(EventId = 9060, Level = LogLevel.Error, Message = "The changed key files of the secure channel were refused; the current keys are kept")]
    private partial void LogReloadFailed(Exception exception);

    /// <summary>
    /// Method used to read the content of a key file
    /// </summary>
    /// <param name="path">[Cannot be null] Path of the file</param>
    /// <param name="what">[Cannot be null] What the file is, for the messages</param>
    /// <returns>
    /// The content
    /// </returns>
    /// <exception cref="InvalidOperationException">If the file does not exist</exception>
    /// <exception cref="IOException">If the file cannot be read now</exception>
    /// <exception cref="UnauthorizedAccessException">If the tier may not read the file</exception>
    private static byte[] ReadFile(string path, string what)
    {
        // Check
        if (!File.Exists(path))
            throw new InvalidOperationException($"The {what} {path} does not exist");

        // Function's return
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Method used to read the signing key
    /// </summary>
    /// <param name="content">Content of the PEM file</param>
    /// <param name="path">[Cannot be null] Path of the file, for the messages</param>
    /// <param name="password">[Cannot be null] Password of the key</param>
    /// <returns>
    /// The key, with its private part
    /// </returns>
    /// <exception cref="InvalidOperationException">If the file is not an encrypted private key, or the password is wrong</exception>
    private static ECDsa ReadSigningKey(byte[] content, string path, string password)
    {
        // Importing the encrypted PKCS#8 key; a wrong password or a damaged file says so, without the password
        ECDsa key = ECDsa.Create();
        try
        {
            key.ImportFromEncryptedPem(Encoding.ASCII.GetString(content), password);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            key.Dispose();
            throw new InvalidOperationException($"The signing key {path} cannot be opened: wrong password, or not an encrypted PKCS#8 key in PEM", exception);
        }

        // Function's return
        return key;
    }

    /// <summary>
    /// Method used to read the file of the public keys of the peer
    /// </summary>
    /// <param name="content">Content of the file</param>
    /// <param name="path">[Cannot be null] Path of the file, for the messages</param>
    /// <returns>
    /// The keys, normalized, in the order of the file
    /// </returns>
    /// <exception cref="InvalidOperationException">If a line is not a public key of P-256</exception>
    internal static List<SigningPublicKey> ReadPeerKeys(byte[] content, string path)
    {
        // Reading each line, the blank ones and the comments skipped
        List<SigningPublicKey> keys = new List<SigningPublicKey>();
        string[] lines = Encoding.ASCII.GetString(content).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            try
            {
                keys.Add(SigningPublicKey.FromBase64(line));
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                throw new InvalidOperationException($"Line {index + 1} of the public keys of the peer {path} is not a public key of the curve P-256 in Base64", exception);
            }
        }

        // Function's return
        return keys;
    }

    /// <summary>
    /// Method used to read and check both files
    /// </summary>
    /// <param name="files">[Cannot be null] Files of the keys</param>
    /// <param name="signingKeyContent">Content of the signing key</param>
    /// <param name="peerKeysContent">Content of the file of the peer</param>
    /// <param name="generation">Generation of the new keys</param>
    /// <returns>
    /// The keys
    /// </returns>
    /// <exception cref="InvalidOperationException">If a file is not what it must be</exception>
    private static SigningKeySet Load(SourceFiles files, byte[] signingKeyContent, byte[] peerKeysContent, long generation)
    {
        // Reading the signing key, then the keys of the peer, and checking them together
        List<SigningPublicKey> peerKeys = ReadPeerKeys(peerKeysContent, files.PeerKeysPath);
        ECDsa signingKey = ReadSigningKey(signingKeyContent, files.SigningKeyPath, files.Password);
        try
        {
            // Function's return
            return SigningKeySet.Create(signingKey, peerKeys, generation);
        }
        catch
        {
            signingKey.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Method used to tell whether a signing key is the key of a TLS certificate
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys of the tier</param>
    /// <param name="certificate">[Cannot be null] TLS certificate of the tier</param>
    /// <returns>
    /// True when they hold the same key
    /// </returns>
    private static bool IsCertificateKey(SigningKeySet keys, X509Certificate2 certificate)
    {
        // Comparing the normalized forms; a certificate whose key is not of P-256 cannot be the same key
        using ECDsa? certificateKey = certificate.GetECDsaPublicKey();
        if (certificateKey is null)
            return false;
        try
        {
            // Function's return
            return SigningPublicKey.FromKey(certificateKey).Equals(keys.OwnKey);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Method used to refuse a signing key that is the key of the current TLS certificate, compared once per pair of key and certificate
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys to use</param>
    /// <exception cref="InvalidOperationException">If the signing key is the key of the TLS certificate</exception>
    private void RefuseCertificateKey(SigningKeySet keys)
    {
        // Comparing again when the key or the certificate changed since the last comparison
        X509Certificate2 certificate = _tlsCertificate();
        bool isSameKey;
        lock (_reloadLock)
        {
            if (_certificateCheck is not { } check || !ReferenceEquals(check.Certificate, certificate) || !ReferenceEquals(check.Keys, keys))
            {
                check = (certificate, keys, IsCertificateKey(keys, certificate));
                _certificateCheck = check;
            }
            isSameKey = check.IsSameKey;
        }
        if (isSameKey)
            throw new InvalidOperationException("The signing key of the secure channel is the key of the TLS certificate of the tier: they must be distinct");
    }

    /// <summary>
    /// Method used to run the periodic check
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws, since it runs on a timer: a failure would be lost there.</para>
    /// </remarks>
    private void CheckPeriodically()
    {
        // Taking changed files
        ReloadIfChanged();
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to load the keys from their files, taken again when the files change
    /// </summary>
    /// <param name="signingKeyPath">[Cannot be null or blank] PEM file of the signing key</param>
    /// <param name="password">[Cannot be null] Password of the signing key</param>
    /// <param name="peerKeysPath">[Cannot be null or blank] File of the public keys of the peer</param>
    /// <param name="certificatePassword">[Cannot be null] Password of the PFX of the tier, which must differ</param>
    /// <param name="tlsCertificate">[Cannot be null] Current TLS certificate of the tier</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the checks</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <returns>
    /// The keys
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If a path is blank, or the password is the one of the PFX</exception>
    /// <exception cref="InvalidOperationException">If a file is missing or is not what it must be, or the signing key is the TLS key</exception>
    /// <exception cref="IOException">If a file cannot be read</exception>
    /// <exception cref="UnauthorizedAccessException">If the tier may not read a file</exception>
    public static SigningKeys FromFiles(string signingKeyPath, string password, string peerKeysPath, string certificatePassword, Func<X509Certificate2> tlsCertificate,
        TimeProvider timeProvider, ILogger logger)
    {
        // Checks
        if (password is null)
            throw new ArgumentNullException(nameof(password), $"The value of the parameter: {nameof(password)} cannot be null");
        if (certificatePassword is null)
            throw new ArgumentNullException(nameof(certificatePassword), $"The value of the parameter: {nameof(certificatePassword)} cannot be null");
        if (tlsCertificate is null)
            throw new ArgumentNullException(nameof(tlsCertificate), $"The value of the parameter: {nameof(tlsCertificate)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (string.IsNullOrWhiteSpace(signingKeyPath))
            throw new ArgumentException($"The value of the parameter: {nameof(signingKeyPath)} cannot be null or blank", nameof(signingKeyPath));
        if (string.IsNullOrWhiteSpace(peerKeysPath))
            throw new ArgumentException($"The value of the parameter: {nameof(peerKeysPath)} cannot be null or blank", nameof(peerKeysPath));
        if (password == certificatePassword)
            throw new ArgumentException("The password of the signing key must differ from the password of the PFX", nameof(password));

        // Reading and checking the files, what was taken of them being kept for the reload
        SourceFiles files = new SourceFiles(Path.GetFullPath(signingKeyPath), password, Path.GetFullPath(peerKeysPath));
        DateTime signingKeyTime = File.GetLastWriteTimeUtc(files.SigningKeyPath);
        DateTime peerKeysTime = File.GetLastWriteTimeUtc(files.PeerKeysPath);
        byte[] signingKeyContent = ReadFile(files.SigningKeyPath, "signing key");
        byte[] peerKeysContent = ReadFile(files.PeerKeysPath, "file of the public keys of the peer");
        SigningKeySet current = Load(files, signingKeyContent, peerKeysContent, generation: 0);
        files.Seen(signingKeyTime, SHA256.HashData(signingKeyContent), peerKeysTime, SHA256.HashData(peerKeysContent));
        SigningKeys keys = new SigningKeys(files, current, tlsCertificate, timeProvider, logger);
        keys.RefuseCertificateKey(current);
        string peerKeyIds = string.Join(", ", current.PeerKeys.Select(key => key.KeyIdHex));
        keys.LogLoaded(current.OwnKey.KeyIdHex, peerKeyIds);

        // Starting the periodic check, so that a removed key closes its connections even when no connection opens
        keys._timer = timeProvider.CreateTimer(_ => keys.CheckPeriodically(), null, CheckInterval, CheckInterval);

        // Function's return
        return keys;
    }

    /// <summary>
    /// Method used to load the keys of a tier from its "Handshake" settings, already checked by <see cref="HandshakeOptionsValidator"/>
    /// </summary>
    /// <param name="options">[Cannot be null] Settings of the secure channel</param>
    /// <param name="tlsOptions">[Cannot be null] TLS settings of the tier, for the password of the PFX</param>
    /// <param name="credentials">[Cannot be null] TLS credentials of the tier, for its current certificate</param>
    /// <param name="contentRoot">[Cannot be null or blank] Folder of the tier, from which a relative path is taken</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the checks</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <returns>
    /// The keys, taken again when their files change
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If a setting is invalid, or the password is the one of the PFX</exception>
    /// <exception cref="InvalidOperationException">If a file is missing or is not what it must be, or the signing key is the TLS key</exception>
    /// <exception cref="IOException">If a file cannot be read</exception>
    /// <exception cref="UnauthorizedAccessException">If the tier may not read a file</exception>
    public static SigningKeys FromOptions(HandshakeOptions options, TlsOptions tlsOptions, TlsCredentials credentials, string contentRoot, TimeProvider timeProvider,
        ILogger logger)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (tlsOptions is null)
            throw new ArgumentNullException(nameof(tlsOptions), $"The value of the parameter: {nameof(tlsOptions)} cannot be null");
        if (credentials is null)
            throw new ArgumentNullException(nameof(credentials), $"The value of the parameter: {nameof(credentials)} cannot be null");
        if (string.IsNullOrWhiteSpace(contentRoot))
            throw new ArgumentException($"The value of the parameter: {nameof(contentRoot)} cannot be null or blank", nameof(contentRoot));

        // Function's return: the paths taken from the folder of the tier when they are relative
        return FromFiles(Path.Combine(contentRoot, options.SigningKeyPath), options.SigningKeyPassword, Path.Combine(contentRoot, options.PeerPublicKeysPath),
            tlsOptions.CertificatePassword, () => credentials.Certificate, timeProvider, logger);
    }

    /// <summary>
    /// Method used to build keys from keys already loaded (the tests); they are never reloaded
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys</param>
    /// <param name="tlsCertificate">[Cannot be null] Current TLS certificate of the tier</param>
    /// <returns>
    /// The keys
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="InvalidOperationException">If the signing key is the TLS key</exception>
    public static SigningKeys FromKeys(SigningKeySet keys, Func<X509Certificate2> tlsCertificate)
    {
        // Checks
        if (keys is null)
            throw new ArgumentNullException(nameof(keys), $"The value of the parameter: {nameof(keys)} cannot be null");
        if (tlsCertificate is null)
            throw new ArgumentNullException(nameof(tlsCertificate), $"The value of the parameter: {nameof(tlsCertificate)} cannot be null");

        // Building them without files
        SigningKeys signingKeys = new SigningKeys(SourceFiles.None, keys, tlsCertificate, TimeProvider.System, NullLogger.Instance);
        signingKeys.RefuseCertificateKey(keys);

        // Function's return
        return signingKeys;
    }

    /// <summary>
    /// Method used to take the key files again when they changed, before a new connection and at each periodic check
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: new files that fail the checks are logged and the current keys kept, so that a mistake while
    /// rotating never stops a tier that runs. Files refused are tried again once they change once more; files that could not be read are
    /// tried again at the next call. A removed key of the peer raises <see cref="PeerKeysRemoved"/> once the new keys are in use.</para>
    /// </remarks>
    public void ReloadIfChanged()
    {
        // Nothing to reload for keys given as keys
        if (_files.IsNone)
            return;

        // Comparing the files with the last ones taken, one check at a time
        List<SigningPublicKey> removed;
        lock (_reloadLock)
        {
            DateTime signingKeyTime;
            DateTime peerKeysTime;
            byte[] signingKeyContent;
            byte[] peerKeysContent;
            try
            {
                signingKeyTime = File.GetLastWriteTimeUtc(_files.SigningKeyPath);
                peerKeysTime = File.GetLastWriteTimeUtc(_files.PeerKeysPath);
                signingKeyContent = ReadFile(_files.SigningKeyPath, "signing key");
                peerKeysContent = ReadFile(_files.PeerKeysPath, "file of the public keys of the peer");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                LogReloadFailed(exception);
                return;
            }
            byte[] signingKeyHash = SHA256.HashData(signingKeyContent);
            byte[] peerKeysHash = SHA256.HashData(peerKeysContent);
            if (!_files.HasChanged(signingKeyTime, signingKeyHash, peerKeysTime, peerKeysHash))
                return;

            // Loading and checking them; the current keys stay when they are refused, until the files change once more
            _files.Seen(signingKeyTime, signingKeyHash, peerKeysTime, peerKeysHash);
            SigningKeySet previous = _current;
            SigningKeySet next;
            try
            {
                next = Load(_files, signingKeyContent, peerKeysContent, previous.Generation + 1);
                if (IsCertificateKey(next, _tlsCertificate()))
                    throw new InvalidOperationException("The signing key of the secure channel is the key of the TLS certificate of the tier: they must be distinct");
            }
            catch (Exception exception)
            {
                LogReloadFailed(exception);
                return;
            }

            // Using them for the next connections, then telling what changed
            _current = next;
            List<SigningPublicKey> added = next.PeerKeys.Where(key => !previous.HasPeerKey(key)).ToList();
            removed = previous.PeerKeys.Where(key => !next.HasPeerKey(key)).ToList();
            if (added.Count > 0 || removed.Count > 0)
            {
                string addedIds = string.Join(", ", added.Select(key => key.KeyIdHex));
                string removedIds = string.Join(", ", removed.Select(key => key.KeyIdHex));
                LogPeerKeysChanged(addedIds, removedIds);
            }
            if (!next.OwnKey.Equals(previous.OwnKey))
                LogSigningKeyTaken(next.OwnKey.KeyIdHex);
        }

        // Telling the connections of the removed keys, outside the lock
        if (removed.Count > 0)
            PeerKeysRemoved?.Invoke(this, new PeerKeysRemovedEventArgs(removed.AsReadOnly()));
    }

    /// <summary>
    /// Method used to get the keys for a new connection, after taking changed files
    /// </summary>
    /// <returns>
    /// The snapshot of the current keys, which the connection keeps
    /// </returns>
    /// <exception cref="InvalidOperationException">If the signing key is the key of the current TLS certificate (a certificate reloaded with
    /// it): the connection is refused, fail closed</exception>
    public SigningKeySet GetForNewConnection()
    {
        // Reloading first, then checking the keys against the TLS certificate, which may have been reloaded meanwhile
        ReloadIfChanged();
        SigningKeySet keys = _current;
        RefuseCertificateKey(keys);

        // Function's return
        return keys;
    }

    /// <summary>
    /// Method used to tell whether a key of the peer is in the current list
    /// </summary>
    /// <param name="key">[Cannot be null] Key of the peer</param>
    /// <returns>
    /// True when it is pinned now
    /// </returns>
    public bool IsPinned(SigningPublicKey key)
    {
        // Function's return
        return _current.HasPeerKey(key);
    }

    /// <summary>
    /// Method used to stop the periodic check
    /// </summary>
    public void Dispose()
    {
        // Stopping the timer; the keys stay usable by the connections that hold them
        _timer?.Dispose();
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
    /// Files of the keys and what was last taken of them
    /// </summary>
    /// <param name="SigningKeyPath">PEM file of the signing key</param>
    /// <param name="Password">Password of the signing key</param>
    /// <param name="PeerKeysPath">File of the public keys of the peer</param>
    private sealed record SourceFiles(string SigningKeyPath, string Password, string PeerKeysPath)
    {
        /// <summary>
        /// No file: keys given as keys
        /// </summary>
        public static readonly SourceFiles None = new SourceFiles(string.Empty, string.Empty, string.Empty);

        /// <summary>
        /// Time of the last write of the signing key last taken
        /// </summary>
        private DateTime _signingKeyTime;

        /// <summary>
        /// SHA-256 of the signing key last taken
        /// </summary>
        private byte[] _signingKeyHash = [];

        /// <summary>
        /// Time of the last write of the keys of the peer last taken
        /// </summary>
        private DateTime _peerKeysTime;

        /// <summary>
        /// SHA-256 of the keys of the peer last taken
        /// </summary>
        private byte[] _peerKeysHash = [];

        /// <summary>
        /// Whether there is no file
        /// </summary>
        public bool IsNone => SigningKeyPath.Length == 0;

        /// <summary>
        /// Method used to record what was taken
        /// </summary>
        /// <param name="signingKeyTime">Time of the signing key</param>
        /// <param name="signingKeyHash">Hash of the signing key</param>
        /// <param name="peerKeysTime">Time of the keys of the peer</param>
        /// <param name="peerKeysHash">Hash of the keys of the peer</param>
        public void Seen(DateTime signingKeyTime, byte[] signingKeyHash, DateTime peerKeysTime, byte[] peerKeysHash)
        {
            // Setting
            _signingKeyTime = signingKeyTime;
            _signingKeyHash = signingKeyHash;
            _peerKeysTime = peerKeysTime;
            _peerKeysHash = peerKeysHash;
        }

        /// <summary>
        /// Method used to tell whether a file changed since it was last taken
        /// </summary>
        /// <param name="signingKeyTime">Time of the signing key now</param>
        /// <param name="signingKeyHash">Hash of the signing key now</param>
        /// <param name="peerKeysTime">Time of the keys of the peer now</param>
        /// <param name="peerKeysHash">Hash of the keys of the peer now</param>
        /// <returns>
        /// True when the time or the content of one of them changed
        /// </returns>
        public bool HasChanged(DateTime signingKeyTime, byte[] signingKeyHash, DateTime peerKeysTime, byte[] peerKeysHash)
        {
            // Function's return
            return signingKeyTime != _signingKeyTime || peerKeysTime != _peerKeysTime || !signingKeyHash.AsSpan().SequenceEqual(_signingKeyHash)
                || !peerKeysHash.AsSpan().SequenceEqual(_peerKeysHash);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // Function's return: never the password
            return $"{nameof(SourceFiles)} {{ {nameof(SigningKeyPath)} = {SigningKeyPath}, {nameof(PeerKeysPath)} = {PeerKeysPath} }}";
        }
    }
}
